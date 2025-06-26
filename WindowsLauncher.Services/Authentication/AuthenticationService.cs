// WindowsLauncher.Services/Authentication/AuthenticationService.cs
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Services.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;
        private User? _currentUser;

        public bool IsAuthenticated => _currentUser != null;
        public event EventHandler<User>? UserAuthenticated;
        public event EventHandler? UserLoggedOut;

        public AuthenticationService(
            ILogger<AuthenticationService> logger,
            IUserRepository userRepository,
            IAuditService auditService)
        {
            _logger = logger;
            _userRepository = userRepository;
            _auditService = auditService;
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string? username = null, string? password = null)
        {
            try
            {
                User user;

                // Если username не указан - используем текущего пользователя Windows
                if (string.IsNullOrEmpty(username))
                {
                    user = await AuthenticateCurrentWindowsUserAsync();
                }
                else
                {
                    user = await AuthenticateUserWithCredentialsAsync(username, password);
                }

                if (user == null)
                {
                    await _auditService.LogLoginAsync(username ?? "Unknown", false, "Authentication failed");
                    return AuthenticationResult.Failure("Authentication failed");
                }

                // Получаем группы пользователя из AD
                user.Groups = await GetUserGroupsAsync(user.Username);
                user.Role = await DetermineUserRoleAsync(user.Groups);
                user.LastLogin = DateTime.Now;

                // Сохраняем в кэш базы данных
                await _userRepository.UpsertUserAsync(user);

                _currentUser = user;

                // Логируем успешный вход
                await _auditService.LogLoginAsync(user.Username, true);

                _logger.LogInformation("User {Username} authenticated successfully with role {Role}",
                    user.Username, user.Role);

                // Уведомляем подписчиков
                UserAuthenticated?.Invoke(this, user);

                return AuthenticationResult.Success(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error for user {Username}", username);
                await _auditService.LogLoginAsync(username ?? "Unknown", false, ex.Message);
                return AuthenticationResult.Failure(ex.Message);
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            return _currentUser;
        }

        public async Task<List<string>> GetUserGroupsAsync(string username)
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.FindByIdentity(context, username);

                if (user == null)
                {
                    _logger.LogWarning("User {Username} not found in AD", username);
                    return new List<string>();
                }

                var groups = new List<string>();
                var memberOf = user.GetAuthorizationGroups();

                foreach (var group in memberOf)
                {
                    if (group is GroupPrincipal groupPrincipal)
                    {
                        groups.Add(groupPrincipal.Name);
                    }
                }

                _logger.LogDebug("User {Username} is member of {GroupCount} groups: {Groups}",
                    username, groups.Count, string.Join(", ", groups));

                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get groups for user {Username}", username);
                return new List<string>();
            }
        }

        public async Task<UserRole> DetermineUserRoleAsync(List<string> groups)
        {
            // Конфигурируемые группы для ролей
            var adminGroups = new[] { "LauncherAdmins", "Domain Admins", "Enterprise Admins", "Administrators" };
            var powerUserGroups = new[] { "LauncherPowerUsers", "Power Users" };

            if (groups.Any(g => adminGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
            {
                return UserRole.Administrator;
            }

            if (groups.Any(g => powerUserGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
            {
                return UserRole.PowerUser;
            }

            return UserRole.Standard;
        }

        public async Task<bool> IsUserInGroupAsync(string username, string groupName)
        {
            try
            {
                var groups = await GetUserGroupsAsync(username);
                return groups.Contains(groupName, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {Username} is in group {GroupName}", username, groupName);
                return false;
            }
        }

        public void Logout()
        {
            if (_currentUser != null)
            {
                var username = _currentUser.Username;
                _auditService.LogLogoutAsync(username);
                _logger.LogInformation("User {Username} logged out", username);

                _currentUser = null;
                UserLoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task<User?> AuthenticateCurrentWindowsUserAsync()
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Domain);
                var principal = UserPrincipal.Current;

                if (principal == null)
                {
                    _logger.LogWarning("Current Windows user principal not found");
                    return null;
                }

                var user = new User
                {
                    Username = principal.SamAccountName,
                    DisplayName = principal.DisplayName ?? principal.Name,
                    Email = principal.EmailAddress ?? "",
                    IsActive = true
                };

                _logger.LogDebug("Windows user authenticated: {Username} ({DisplayName})",
                    user.Username, user.DisplayName);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate current Windows user");
                return null;
            }
        }

        private async Task<User?> AuthenticateUserWithCredentialsAsync(string username, string? password)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("Password not provided for user {Username}", username);
                    return null;
                }

                using var context = new PrincipalContext(ContextType.Domain);

                // Проверяем учетные данные
                if (!context.ValidateCredentials(username, password))
                {
                    _logger.LogWarning("Invalid credentials for user {Username}", username);
                    return null;
                }

                // Получаем информацию о пользователе
                var userPrincipal = UserPrincipal.FindByIdentity(context, username);
                if (userPrincipal == null)
                {
                    _logger.LogWarning("User {Username} not found in AD after successful credential validation", username);
                    return null;
                }

                var user = new User
                {
                    Username = userPrincipal.SamAccountName,
                    DisplayName = userPrincipal.DisplayName ?? userPrincipal.Name,
                    Email = userPrincipal.EmailAddress ?? "",
                    IsActive = userPrincipal.Enabled ?? false
                };

                _logger.LogDebug("User authenticated with credentials: {Username} ({DisplayName})",
                    user.Username, user.DisplayName);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate user {Username} with credentials", username);
                return null;
            }
        }
    }
}
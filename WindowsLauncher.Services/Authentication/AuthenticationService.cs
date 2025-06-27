// ===== WindowsLauncher.Services/Authentication/AuthenticationService.cs - ИСПРАВЛЕНИЕ ИСКЛЮЧЕНИЙ =====
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Services.Authentication
{
    /// <summary>
    /// Исправленный сервис аутентификации
    /// </summary>
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
                _logger.LogInformation("Starting authentication process for user: {Username}", username ?? "CurrentUser");

                User user;

                // Определяем метод аутентификации
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
                    var errorMessage = "Authentication failed - user is null";
                    _logger.LogWarning(errorMessage);
                    await _auditService.LogLoginAsync(username ?? "Unknown", false, errorMessage);
                    return AuthenticationResult.Failure(errorMessage);
                }

                // Проверяем активность пользователя
                if (!user.IsActive)
                {
                    var errorMessage = $"User account {user.Username} is disabled";
                    _logger.LogWarning(errorMessage);
                    await _auditService.LogLoginAsync(user.Username, false, errorMessage);
                    return AuthenticationResult.Failure("User account is disabled");
                }

                // Получаем дополнительную информацию о пользователе
                await EnrichUserInformationAsync(user);

                // Сохраняем пользователя в базе данных
                user = await _userRepository.UpsertUserAsync(user);

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
                _logger.LogError(ex, "Authentication error for user {Username}", username ?? "CurrentUser");
                await _auditService.LogLoginAsync(username ?? "Unknown", false, ex.Message);
                return AuthenticationResult.Failure(GetUserFriendlyErrorMessage(ex));
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
                _logger.LogDebug("Getting groups for user: {Username}", username);

                // Проверяем домен
                if (!IsDomainEnvironment())
                {
                    _logger.LogInformation("Not in domain environment, returning default groups for {Username}", username);
                    return GetDefaultGroupsForLocalUser(username);
                }

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
                    if (group is GroupPrincipal groupPrincipal && !string.IsNullOrEmpty(groupPrincipal.Name))
                    {
                        groups.Add(groupPrincipal.Name);
                    }
                }

                _logger.LogDebug("User {Username} is member of {GroupCount} groups: {Groups}",
                    username, groups.Count, string.Join(", ", groups.Take(10))); // Ограничиваем для логов

                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get groups for user {Username}", username);
                // Возвращаем базовые группы в случае ошибки
                return GetDefaultGroupsForLocalUser(username);
            }
        }

        public async Task<UserRole> DetermineUserRoleAsync(List<string> groups)
        {
            try
            {
                // Конфигурируемые группы для ролей (можно вынести в настройки)
                var adminGroups = new[] {
                    "LauncherAdmins",
                    "Domain Admins",
                    "Enterprise Admins",
                    "Administrators",
                    "Administrator" // Локальная группа
                };

                var powerUserGroups = new[] {
                    "LauncherPowerUsers",
                    "Power Users",
                    "PowerUsers"
                };

                _logger.LogDebug("Determining role for groups: {Groups}", string.Join(", ", groups));

                if (groups.Any(g => adminGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("User assigned Administrator role");
                    return UserRole.Administrator;
                }

                if (groups.Any(g => powerUserGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("User assigned PowerUser role");
                    return UserRole.PowerUser;
                }

                _logger.LogDebug("User assigned Standard role");
                return UserRole.Standard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining user role, defaulting to Standard");
                return UserRole.Standard;
            }
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
                _logger.LogInformation("User {Username} logging out", username);

                try
                {
                    _auditService.LogLogoutAsync(username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error logging logout event for user {Username}", username);
                }

                _currentUser = null;
                UserLoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }

        #region Private Methods

        private async Task<User?> AuthenticateCurrentWindowsUserAsync()
        {
            try
            {
                _logger.LogDebug("Authenticating current Windows user");

                if (!IsDomainEnvironment())
                {
                    return await AuthenticateLocalUserAsync();
                }

                using var context = new PrincipalContext(ContextType.Domain);
                var principal = UserPrincipal.Current;

                if (principal == null)
                {
                    _logger.LogWarning("Current Windows user principal not found");
                    return null;
                }

                var user = new User
                {
                    Username = principal.SamAccountName ?? principal.Name ?? Environment.UserName,
                    DisplayName = principal.DisplayName ?? principal.Name ?? Environment.UserName,
                    Email = principal.EmailAddress ?? "",
                    IsActive = principal.Enabled ?? true
                };

                _logger.LogDebug("Windows user authenticated: {Username} ({DisplayName})",
                    user.Username, user.DisplayName);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate current Windows user");

                // Fallback to local user authentication
                return await AuthenticateLocalUserAsync();
            }
        }

        private async Task<User?> AuthenticateLocalUserAsync()
        {
            try
            {
                _logger.LogInformation("Authenticating as local user (not in domain)");

                var currentUser = Environment.UserName;
                var displayName = Environment.UserDomainName != Environment.MachineName
                    ? $"{Environment.UserDomainName}\\{currentUser}"
                    : currentUser;

                var user = new User
                {
                    Username = currentUser,
                    DisplayName = displayName,
                    Email = "",
                    IsActive = true
                };

                _logger.LogInformation("Local user authenticated: {Username}", user.Username);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate local user");
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

                if (!IsDomainEnvironment())
                {
                    _logger.LogWarning("Credential authentication not supported in non-domain environment");
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
                    _logger.LogWarning("User {Username} not found in AD after credential validation", username);
                    return null;
                }

                var user = new User
                {
                    Username = userPrincipal.SamAccountName ?? username,
                    DisplayName = userPrincipal.DisplayName ?? userPrincipal.Name ?? username,
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

        private async Task EnrichUserInformationAsync(User user)
        {
            try
            {
                // Получаем группы пользователя
                user.Groups = await GetUserGroupsAsync(user.Username);

                // Определяем роль на основе групп
                user.Role = await DetermineUserRoleAsync(user.Groups);

                // Обновляем время последнего входа
                user.LastLogin = DateTime.Now;

                _logger.LogDebug("User information enriched: {Username} has {GroupCount} groups and role {Role}",
                    user.Username, user.Groups.Count, user.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enrich user information for {Username}", user.Username);

                // Устанавливаем минимальные значения по умолчанию
                user.Groups ??= new List<string>();
                user.Role = UserRole.Standard;
                user.LastLogin = DateTime.Now;
            }
        }

        private bool IsDomainEnvironment()
        {
            try
            {
                return Environment.UserDomainName != Environment.MachineName;
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetDefaultGroupsForLocalUser(string username)
        {
            try
            {
                _logger.LogDebug("Getting default groups for local user: {Username}", username);

                var groups = new List<string> { "Users" };

                // Проверяем локальные группы если возможно
                try
                {
                    using var context = new PrincipalContext(ContextType.Machine);
                    var user = UserPrincipal.FindByIdentity(context, username);

                    if (user != null)
                    {
                        var memberOf = user.GetAuthorizationGroups();
                        foreach (var group in memberOf)
                        {
                            if (group is GroupPrincipal groupPrincipal && !string.IsNullOrEmpty(groupPrincipal.Name))
                            {
                                groups.Add(groupPrincipal.Name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not get local groups for user {Username}", username);
                }

                // Добавляем дефолтные группы для тестирования
                if (username.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                    username.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    groups.Add("Administrators");
                    groups.Add("LauncherAdmins");
                }

                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default groups for user {Username}", username);
                return new List<string> { "Users" };
            }
        }

        // 🆕 ИСПРАВЛЕННАЯ функция обработки ошибок
        private string GetUserFriendlyErrorMessage(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => "Access denied. Please check your credentials.",
                //DirectoryServiceException => "Domain service is unavailable. Please try again later.",
                PrincipalServerDownException => "Domain controller is unreachable. Please contact your administrator.",
                TimeoutException => "Authentication timed out. Please try again.",
                SystemException sysEx when sysEx.Message.Contains("server") => "Domain service is unavailable. Please try again later.",
                _ => $"Authentication failed: {exception.Message}"
            };
        }

        #endregion
    }
}
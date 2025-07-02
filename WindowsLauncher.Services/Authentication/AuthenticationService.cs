// WindowsLauncher.Services/Authentication/AuthenticationService.cs - ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.Authentication
{
    /// <summary>
    /// Унифицированный сервис аутентификации с поддержкой Windows SSO, AD и сервисного администратора
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IActiveDirectoryService _adService;
        private readonly IAuthenticationConfigurationService _configService;
        private readonly IAuditService _auditService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            IActiveDirectoryService adService,
            IAuthenticationConfigurationService configService,
            IAuditService auditService,
            IUserRepository userRepository,
            ILogger<AuthenticationService> logger)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Аутентифицировать пользователя Windows (без параметров - для интерфейса)
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync()
        {
            try
            {
                var user = await AuthenticateWindowsUserInternalAsync();
                return AuthenticationResult.Success(user, AuthenticationType.WindowsSSO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating current Windows user");
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Аутентифицировать с учетными данными (для интерфейса)
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationCredentials credentials)
        {
            try
            {
                if (credentials == null)
                    throw new ArgumentNullException(nameof(credentials));

                if (credentials.IsServiceAccount)
                {
                    return await AuthenticateServiceAdminInternalAsync(credentials.Username, credentials.Password);
                }
                else
                {
                    return await AuthenticateAsync(credentials.Username, credentials.Password);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating with credentials");
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Аутентифицировать пользователя по логину/паролю - ИСПРАВЛЕНО: возвращает AuthenticationResult
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                _logger.LogDebug("Authenticating user: {Username}", username);

                // Проверяем доступность домена
                var isDomainAvailable = await IsDomainAvailableAsync();
                if (!isDomainAvailable)
                {
                    _logger.LogWarning("Domain is not available, checking local authentication");
                    return await AuthenticateLocalUserInternal(username, password);
                }

                // Аутентификация через AD
                var authResult = await _adService.AuthenticateAsync(username, password);
                if (!authResult.IsSuccessful)
                {
                    await _auditService.LogLoginAsync(username, false, authResult.ErrorMessage);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, authResult.ErrorMessage);
                }

                // Получаем или создаем пользователя в локальной базе
                var userInfo = await _adService.GetUserInfoAsync(username);
                if (userInfo == null)
                {
                    await _auditService.LogLoginAsync(username, false, "User not found in AD");
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound, "Пользователь не найден в Active Directory");
                }

                var user = await GetOrCreateUserFromAD(userInfo);

                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                await _auditService.LogLoginAsync(user.Username, true);
                _logger.LogInformation("User {Username} authenticated successfully", username);

                return AuthenticationResult.Success(user, AuthenticationType.DomainLDAP, authResult.Domain);
            }
            catch (Exception ex)
            {
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Аутентифицировать пользователя Windows - ДОБАВЛЕН: для обратной совместимости
        /// </summary>
        public async Task<User> AuthenticateWindowsUserAsync()
        {
            var result = await AuthenticateAsync();
            if (result.IsSuccess)
                return result.User;

            throw new UnauthorizedAccessException(result.ErrorMessage);
        }

        /// <summary>
        /// Аутентифицировать сервисного администратора - ДОБАВЛЕН: для обратной совместимости
        /// </summary>
        public async Task<User> AuthenticateServiceAdminAsync(string username, string password)
        {
            var result = await AuthenticateServiceAdminInternalAsync(username, password);
            if (result.IsSuccess)
                return result.User;

            throw new UnauthorizedAccessException(result.ErrorMessage);
        }

        /// <summary>
        /// Создать сервисного администратора
        /// </summary>
        public async Task CreateServiceAdminAsync(string username, string password)
        {
            try
            {
                // Проверяем, не существует ли уже такой пользователь
                var existingUser = await _userRepository.GetByUsernameAsync(username);
                if (existingUser != null)
                {
                    _logger.LogWarning("Service admin {Username} already exists", username);
                    throw new InvalidOperationException($"Пользователь с именем '{username}' уже существует");
                }

                // Генерируем соль и хэш пароля
                var (salt, passwordHash) = GeneratePasswordHash(password);

                // Создаем нового пользователя
                var serviceAdmin = new User
                {
                    Username = username,
                    DisplayName = "Сервисный администратор",
                    Email = string.Empty,
                    Role = UserRole.Administrator,
                    IsActive = true,
                    IsServiceAccount = true,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = null,
                    FailedLoginAttempts = 0,
                    IsLocked = false,
                    LockoutEnd = null
                };

                // Сохраняем в базе данных
                await _userRepository.AddAsync(serviceAdmin);

                // Обновляем конфигурацию
                var config = _configService.GetConfiguration();
                config.ServiceAdmin.Username = username;
                config.ServiceAdmin.PasswordHash = passwordHash;
                config.ServiceAdmin.Salt = salt;
                config.ServiceAdmin.RequirePasswordChange = true;
                config.ServiceAdmin.CreatedAt = DateTime.UtcNow;

                await _configService.SaveConfigurationAsync(config);

                _logger.LogInformation("Service admin {Username} created successfully", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service admin {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// Изменить пароль сервисного администратора
        /// </summary>
        public async Task ChangeServiceAdminPasswordAsync(string username, string oldPassword, string newPassword)
        {
            try
            {
                var config = _configService.GetConfiguration();

                if (config.ServiceAdmin.Username != username)
                {
                    throw new UnauthorizedAccessException("Неверные учетные данные");
                }

                // Проверяем старый пароль
                var isOldPasswordValid = VerifyPassword(oldPassword, config.ServiceAdmin.PasswordHash, config.ServiceAdmin.Salt);
                if (!isOldPasswordValid)
                {
                    throw new UnauthorizedAccessException("Неверный текущий пароль");
                }

                // Генерируем новый хэш пароля
                var (salt, passwordHash) = GeneratePasswordHash(newPassword);

                // Обновляем конфигурацию
                config.ServiceAdmin.PasswordHash = passwordHash;
                config.ServiceAdmin.Salt = salt;
                config.ServiceAdmin.LastPasswordChange = DateTime.UtcNow;
                config.ServiceAdmin.RequirePasswordChange = false;

                await _configService.SaveConfigurationAsync(config);

                _logger.LogInformation("Service admin {Username} password changed successfully", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing service admin password for {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// Проверить права пользователя
        /// </summary>
        public async Task<bool> HasPermissionAsync(int userId, string permission)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return false;
                }

                // Логика проверки прав на основе роли пользователя
                return user.Role switch
                {
                    UserRole.Administrator => true, // Администратор имеет все права
                    UserRole.PowerUser => IsPermissionAllowedForPowerUser(permission),
                    UserRole.Standard => IsPermissionAllowedForStandardUser(permission),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
                return false;
            }
        }

        /// <summary>
        /// Получить роль пользователя
        /// </summary>
        public async Task<UserRole> GetUserRoleAsync(string username)
        {
            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user != null)
                {
                    return user.Role;
                }

                // Если пользователя нет в локальной базе, проверяем AD
                var userInfo = await _adService.GetUserInfoAsync(username);
                if (userInfo != null)
                {
                    var config = _configService.GetConfiguration();
                    return DetermineUserRoleFromGroups(userInfo.Groups, config);
                }

                return UserRole.Standard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role for {Username}", username);
                return UserRole.Standard;
            }
        }

        /// <summary>
        /// Выход из системы (по ID)
        /// </summary>
        public async Task LogoutAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    await _auditService.LogLogoutAsync(user.Username);
                }

                _logger.LogInformation("User {UserId} logged out", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Выход из системы (простой)
        /// </summary>
        public void Logout()
        {
            try
            {
                _logger.LogInformation("User logged out (simple logout)");
                // Простая реализация - в WPF приложении обычно просто закрываем сессию
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simple logout");
            }
        }

        /// <summary>
        /// Обновить сессию пользователя
        /// </summary>
        public async Task RefreshSessionAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    user.LastActivityAt = DateTime.UtcNow;
                    await _userRepository.UpdateAsync(user);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing session for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Проверить валидность сессии
        /// </summary>
        public async Task<bool> IsSessionValidAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return false;
                }

                var config = _configService.GetConfiguration();
                var sessionTimeout = TimeSpan.FromMinutes(config.ServiceAdmin.SessionTimeoutMinutes);

                if (user.LastActivityAt.HasValue)
                {
                    return DateTime.UtcNow - user.LastActivityAt.Value < sessionTimeout;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session validity for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Проверить доступность домена
        /// </summary>
        public async Task<bool> IsDomainAvailableAsync(string domain = null)
        {
            try
            {
                if (string.IsNullOrEmpty(domain))
                {
                    // Используем домен из конфигурации
                    return await _adService.IsDomainAvailableAsync();
                }
                else
                {
                    // Проверяем конкретный домен
                    var config = _configService.GetConfiguration();
                    return await _adService.TestConnectionAsync(domain, config.Port);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking domain availability");
                return false;
            }
        }

        /// <summary>
        /// Проверить, настроен ли сервисный администратор
        /// </summary>
        public bool IsServiceAdminConfigured()
        {
            try
            {
                var config = _configService.GetConfiguration();
                return !string.IsNullOrEmpty(config.ServiceAdmin.Username) &&
                       !string.IsNullOrEmpty(config.ServiceAdmin.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service admin configuration");
                return false;
            }
        }

        #region Private Methods

        /// <summary>
        /// Внутренний метод аутентификации Windows пользователя
        /// </summary>
        private async Task<User> AuthenticateWindowsUserInternalAsync()
        {
            try
            {
                var windowsIdentity = WindowsIdentity.GetCurrent();
                var username = windowsIdentity.Name;

                if (string.IsNullOrEmpty(username))
                {
                    throw new UnauthorizedAccessException("Не удалось получить данные пользователя Windows");
                }

                _logger.LogDebug("Authenticating Windows user: {Username}", username);

                // Проверяем доступность домена
                var isDomainAvailable = await _adService.IsDomainAvailableAsync();
                if (!isDomainAvailable)
                {
                    _logger.LogWarning("Domain is not available, using fallback authentication");
                    return await AuthenticateWithFallback(username);
                }

                // Извлекаем имя пользователя из доменного формата
                var cleanUsername = ExtractUsernameFromDomain(username);

                // Получаем информацию пользователя из AD
                var adUserInfo = await _adService.GetUserInfoAsync(cleanUsername);
                if (adUserInfo == null || !adUserInfo.IsEnabled)
                {
                    throw new UnauthorizedAccessException("Пользователь не найден или отключен в Active Directory");
                }

                // Получаем или создаем пользователя в локальной базе
                var user = await GetOrCreateUserFromAD(adUserInfo);

                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                await _auditService.LogLoginAsync(user.Username, true);
                _logger.LogInformation("Windows user {Username} authenticated successfully", username);

                return user;
            }
            catch (Exception ex)
            {
                var username = WindowsIdentity.GetCurrent()?.Name ?? "Unknown";
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating Windows user");
                throw;
            }
        }

        /// <summary>
        /// Внутренний метод аутентификации сервисного администратора
        /// </summary>
        private async Task<AuthenticationResult> AuthenticateServiceAdminInternalAsync(string username, string password)
        {
            try
            {
                var config = _configService.GetConfiguration();

                // Проверяем блокировку
                if (config.ServiceAdmin.IsLocked &&
                    config.ServiceAdmin.UnlockTime.HasValue &&
                    DateTime.UtcNow < config.ServiceAdmin.UnlockTime.Value)
                {
                    var remainingTime = config.ServiceAdmin.UnlockTime.Value - DateTime.UtcNow;
                    var errorMsg = $"Учетная запись заблокирована. Разблокировка через {remainingTime.Minutes} минут";
                    return AuthenticationResult.Failure(AuthenticationStatus.AccountLocked, errorMsg);
                }

                // Сбрасываем блокировку если время истекло
                if (config.ServiceAdmin.IsLocked &&
                    config.ServiceAdmin.UnlockTime.HasValue &&
                    DateTime.UtcNow >= config.ServiceAdmin.UnlockTime.Value)
                {
                    config.ServiceAdmin.IsLocked = false;
                    config.ServiceAdmin.FailedLoginAttempts = 0;
                    config.ServiceAdmin.UnlockTime = null;
                    await _configService.SaveConfigurationAsync(config);
                }

                // Проверяем учетные данные
                if (config.ServiceAdmin.Username != username)
                {
                    await RecordFailedServiceAdminLogin(config);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
                }

                // Проверяем пароль
                var isPasswordValid = VerifyPassword(password, config.ServiceAdmin.PasswordHash, config.ServiceAdmin.Salt);
                if (!isPasswordValid)
                {
                    await RecordFailedServiceAdminLogin(config);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
                }

                // Успешная аутентификация
                config.ServiceAdmin.LastSuccessfulLogin = DateTime.UtcNow;
                config.ServiceAdmin.FailedLoginAttempts = 0;
                await _configService.SaveConfigurationAsync(config);

                // Получаем или создаем пользователя в базе данных
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user == null)
                {
                    user = new User
                    {
                        Username = username,
                        DisplayName = "Сервисный администратор",
                        Email = string.Empty,
                        Role = UserRole.Administrator,
                        IsActive = true,
                        IsServiceAccount = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _userRepository.AddAsync(user);
                }

                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                await _auditService.LogLoginAsync(username, true);
                _logger.LogInformation("Service admin {Username} authenticated successfully", username);

                return AuthenticationResult.Success(user, AuthenticationType.LocalService);
            }
            catch (Exception ex)
            {
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating service admin {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Внутренний метод аутентификации локального пользователя
        /// </summary>
        private async Task<AuthenticationResult> AuthenticateLocalUserInternal(string username, string password)
        {
            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user == null || !user.IsServiceAccount)
                {
                    return AuthenticationResult.Failure(AuthenticationStatus.ServiceModeRequired,
                        "Домен недоступен. Используйте режим сервисного администратора.");
                }

                if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    user.FailedLoginAttempts++;

                    if (user.FailedLoginAttempts >= 5)
                    {
                        user.IsLocked = true;
                        user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    }

                    await _userRepository.UpdateAsync(user);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
                }

                // Проверяем блокировку
                if (user.IsLocked && user.LockoutEnd.HasValue && DateTime.UtcNow < user.LockoutEnd.Value)
                {
                    var remainingTime = user.LockoutEnd.Value - DateTime.UtcNow;
                    var errorMsg = $"Учетная запись заблокирована. Разблокировка через {remainingTime.Minutes} минут";
                    return AuthenticationResult.Failure(AuthenticationStatus.AccountLocked, errorMsg);
                }

                // Сбрасываем блокировку если время истекло
                if (user.IsLocked && user.LockoutEnd.HasValue && DateTime.UtcNow >= user.LockoutEnd.Value)
                {
                    user.IsLocked = false;
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                }

                user.FailedLoginAttempts = 0;
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                await _auditService.LogLoginAsync(username, true);
                return AuthenticationResult.Success(user, AuthenticationType.LocalService);
            }
            catch (Exception ex)
            {
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating local user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Генерация хэша пароля с солью
        /// </summary>
        private (string salt, string hash) GeneratePasswordHash(string password)
        {
            // Генерируем случайную соль
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            var salt = Convert.ToBase64String(saltBytes);

            // Создаем хэш пароля с использованием PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);
            var hash = Convert.ToBase64String(hashBytes);

            return (salt, hash);
        }

        /// <summary>
        /// Проверка пароля
        /// </summary>
        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(storedSalt);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
                var hashBytes = pbkdf2.GetBytes(32);
                var computedHash = Convert.ToBase64String(hashBytes);

                return computedHash == storedHash;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Записать неудачную попытку входа сервисного администратора
        /// </summary>
        private async Task RecordFailedServiceAdminLogin(AuthenticationConfiguration config)
        {
            config.ServiceAdmin.FailedLoginAttempts++;
            config.ServiceAdmin.LastFailedLogin = DateTime.UtcNow;

            if (config.ServiceAdmin.FailedLoginAttempts >= config.ServiceAdmin.MaxLoginAttempts)
            {
                config.ServiceAdmin.IsLocked = true;
                config.ServiceAdmin.UnlockTime = DateTime.UtcNow.AddMinutes(config.ServiceAdmin.LockoutDurationMinutes);

                _logger.LogWarning("Service admin account locked due to too many failed attempts");
            }

            await _configService.SaveConfigurationAsync(config);
        }

        /// <summary>
        /// Аутентификация с fallback режимом
        /// </summary>
        private async Task<User> AuthenticateWithFallback(string username)
        {
            var config = _configService.GetConfiguration();
            if (!config.EnableFallbackMode)
            {
                throw new UnauthorizedAccessException("Домен недоступен, а fallback режим отключен");
            }

            // Ищем пользователя в локальной базе (кэш)
            var user = await _userRepository.GetByUsernameAsync(ExtractUsernameFromDomain(username));
            if (user == null)
            {
                throw new UnauthorizedAccessException("Пользователь не найден в локальном кэше");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Учетная запись пользователя отключена");
            }

            _logger.LogInformation("User {Username} authenticated using fallback mode", username);
            return user;
        }

        /// <summary>
        /// Получить или создать пользователя из AD
        /// </summary>
        private async Task<User> GetOrCreateUserFromAD(AdUserInfo userInfo)
        {
            var user = await _userRepository.GetByUsernameAsync(userInfo.Username);

            if (user == null)
            {
                var config = _configService.GetConfiguration();
                user = new User
                {
                    Username = userInfo.Username,
                    DisplayName = userInfo.DisplayName,
                    Email = userInfo.Email,
                    Role = DetermineUserRoleFromGroups(userInfo.Groups, config),
                    IsActive = userInfo.IsEnabled,
                    IsServiceAccount = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                // Обновляем информацию пользователя из AD
                user.DisplayName = userInfo.DisplayName;
                user.Email = userInfo.Email;
                user.IsActive = userInfo.IsEnabled;

                var config = _configService.GetConfiguration();
                user.Role = DetermineUserRoleFromGroups(userInfo.Groups, config);

                await _userRepository.UpdateAsync(user);
            }

            return user;
        }

        /// <summary>
        /// Определить роль пользователя по группам
        /// </summary>
        private UserRole DetermineUserRoleFromGroups(System.Collections.Generic.List<string> groups, AuthenticationConfiguration config)
        {
            var adminGroups = config.AdminGroups.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var powerUserGroups = config.PowerUserGroups.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (groups.Any(g => adminGroups.Any(ag => ag.Trim().Equals(g, StringComparison.OrdinalIgnoreCase))))
            {
                return UserRole.Administrator;
            }

            if (groups.Any(g => powerUserGroups.Any(pg => pg.Trim().Equals(g, StringComparison.OrdinalIgnoreCase))))
            {
                return UserRole.PowerUser;
            }

            return UserRole.Standard;
        }

        /// <summary>
        /// Извлечь имя пользователя из доменного имени
        /// </summary>
        private string ExtractUsernameFromDomain(string domainUsername)
        {
            if (domainUsername.Contains('\\'))
            {
                return domainUsername.Split('\\')[1];
            }

            if (domainUsername.Contains('@'))
            {
                return domainUsername.Split('@')[0];
            }

            return domainUsername;
        }

        /// <summary>
        /// Проверить разрешение для PowerUser
        /// </summary>
        private bool IsPermissionAllowedForPowerUser(string permission)
        {
            var allowedPermissions = new[]
            {
                "LaunchApplication",
                "ViewApplications",
                "ManageOwnSettings",
                "ViewAuditLog",
                "ExportData"
            };

            return allowedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверить разрешение для Standard пользователя
        /// </summary>
        private bool IsPermissionAllowedForStandardUser(string permission)
        {
            var allowedPermissions = new[]
            {
                "LaunchApplication",
                "ViewApplications"
            };

            return allowedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
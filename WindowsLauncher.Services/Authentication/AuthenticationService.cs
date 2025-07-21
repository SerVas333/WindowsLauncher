// WindowsLauncher.Services/Authentication/AuthenticationService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
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
    /// Исправленный сервис аутентификации с улучшенной обработкой fallback режима
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IActiveDirectoryService _adService;
        private readonly IAuthenticationConfigurationService _configService;
        private readonly IAuditService _auditService;
        private readonly IUserRepository _userRepository;
        private readonly ILocalUserService _localUserService;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            IActiveDirectoryService adService,
            IAuthenticationConfigurationService configService,
            IAuditService auditService,
            IUserRepository userRepository,
            ILocalUserService localUserService,
            ILogger<AuthenticationService> logger)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _localUserService = localUserService ?? throw new ArgumentNullException(nameof(localUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Аутентифицировать текущего пользователя Windows (для интерфейса)
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync()
        {
            try
            {
                _logger.LogInformation("=== STARTING WINDOWS USER AUTHENTICATION ===");

                // Получаем текущего пользователя Windows
                var windowsIdentity = WindowsIdentity.GetCurrent();
                var fullUsername = windowsIdentity.Name;

                if (string.IsNullOrEmpty(fullUsername))
                {
                    _logger.LogError("Failed to get current Windows user identity");
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials,
                        "Не удалось получить данные пользователя Windows");
                }

                _logger.LogInformation("Windows user detected: {FullUsername}", fullUsername);

                // Извлекаем чистое имя пользователя
                var username = ExtractUsernameFromDomain(fullUsername);
                _logger.LogInformation("Extracted username: {Username}", username);

                // Проверяем доступность домена
                var isDomainAvailable = await IsDomainAvailableAsync();
                _logger.LogInformation("Domain availability check: {IsAvailable}", isDomainAvailable);

                if (isDomainAvailable)
                {
                    // Пытаемся аутентифицироваться через AD
                    _logger.LogInformation("Attempting AD authentication for user: {Username}", username);
                    var adResult = await AuthenticateViaDomainAsync(username);
                    if (adResult.IsSuccess)
                    {
                        _logger.LogInformation("AD authentication successful for user: {Username}", username);
                        return adResult;
                    }

                    _logger.LogWarning("AD authentication failed for user {Username}: {Error}",
                        username, adResult.ErrorMessage);
                }

                // Fallback режим - создаем временного пользователя
                _logger.LogInformation("Using fallback authentication for user: {Username}", username);
                var fallbackUser = await CreateFallbackUserAsync(username, fullUsername);

                await _auditService.LogLoginAsync(username, true, "Fallback authentication");
                _logger.LogInformation("Fallback authentication successful for user: {Username}", username);

                return AuthenticationResult.Success(fallbackUser, AuthenticationType.WindowsSSO);
            }
            catch (Exception ex)
            {
                var username = WindowsIdentity.GetCurrent()?.Name ?? "Unknown";
                _logger.LogError(ex, "Error during Windows user authentication");
                await _auditService.LogLoginAsync(username, false, ex.Message);
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

                _logger.LogInformation("Authenticating with credentials. AuthType: {AuthType}, IsServiceAccount: {IsService}, Username: {Username}",
                    credentials.AuthenticationType, credentials.IsServiceAccount, credentials.Username);

                // Определяем тип аутентификации
                switch (credentials.AuthenticationType)
                {
                    case AuthenticationType.LocalUsers:
                        return await AuthenticateLocalUserAsync(credentials.Username, credentials.Password);
                    
                    case AuthenticationType.LocalService:
                        return await AuthenticateServiceAdminInternalAsync(credentials.Username, credentials.Password);
                    
                    case AuthenticationType.Guest:
                        return await AuthenticateGuestAsync(credentials.Username);
                    
                    case AuthenticationType.DomainLDAP:
                    default:
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
        /// Аутентифицировать пользователя по логину/паролю
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Authenticating user with credentials: {Username}", username);

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

                // Получаем информацию пользователя из AD
                var userInfo = await _adService.GetUserInfoAsync(username);
                if (userInfo == null)
                {
                    await _auditService.LogLoginAsync(username, false, "User not found in AD");
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound, "Пользователь не найден в Active Directory");
                }

                var user = await GetOrCreateUserFromAD(userInfo);
                user.LastLoginAt = DateTime.UtcNow;
                
                // Only update if user has a valid ID (not a newly created entity)
                if (user.Id > 0)
                {
                    await _userRepository.UpdateAsync(user);
                }

                await _auditService.LogLoginAsync(user.Username, true);
                _logger.LogInformation("User {Username} authenticated successfully via AD", username);

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
        /// Аутентифицировать пользователя Windows (обратная совместимость)
        /// </summary>
        public async Task<User> AuthenticateWindowsUserAsync()
        {
            var result = await AuthenticateAsync();
            if (result.IsSuccess && result.User != null)
                return result.User;

            throw new UnauthorizedAccessException(result.ErrorMessage ?? "Authentication failed");
        }

        /// <summary>
        /// Аутентифицировать сервисного администратора (обратная совместимость)
        /// </summary>
        public async Task<User> AuthenticateServiceAdminAsync(string username, string password)
        {
            var result = await AuthenticateServiceAdminInternalAsync(username, password);
            if (result.IsSuccess && result.User != null)
                return result.User;

            throw new UnauthorizedAccessException(result.ErrorMessage ?? "Authentication failed");
        }

        #region Service Admin Methods

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

                await _userRepository.AddAsync(serviceAdmin);

                // Обновляем конфигурацию
                var config = _configService.GetConfiguration();
                config.ServiceAdmin.Username = username;
                config.ServiceAdmin.SetPassword(passwordHash, salt);
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

                var isOldPasswordValid = VerifyPassword(oldPassword, config.ServiceAdmin.PasswordHash, config.ServiceAdmin.Salt);
                if (!isOldPasswordValid)
                {
                    throw new UnauthorizedAccessException("Неверный текущий пароль");
                }

                var (salt, passwordHash) = GeneratePasswordHash(newPassword);

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

        #endregion

        #region Utility Methods

        public async Task<bool> HasPermissionAsync(int userId, string permission)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return false;
                }

                return user.Role switch
                {
                    UserRole.Administrator => true,
                    UserRole.PowerUser => IsPermissionAllowedForPowerUser(permission),
                    UserRole.Standard => IsPermissionAllowedForStandardUser(permission),
                    UserRole.Guest => IsPermissionAllowedForGuestUser(permission),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
                return false;
            }
        }

        public async Task<UserRole> GetUserRoleAsync(string username)
        {
            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user != null)
                {
                    return user.Role;
                }

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

        public void Logout()
        {
            try
            {
                _logger.LogInformation("User logged out (simple logout)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simple logout");
            }
        }

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

        public async Task<bool> IsDomainAvailableAsync(string? domain = null)
        {
            try
            {
                if (string.IsNullOrEmpty(domain))
                {
                    return await _adService.IsDomainAvailableAsync();
                }
                else
                {
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

        #endregion

        #region Private Methods

        /// <summary>
        /// Аутентификация через домен
        /// </summary>
        private async Task<AuthenticationResult> AuthenticateViaDomainAsync(string username)
        {
            try
            {
                // Получаем информацию пользователя из AD
                var userInfo = await _adService.GetUserInfoAsync(username);
                if (userInfo == null || !userInfo.IsEnabled)
                {
                    _logger.LogWarning("User {Username} not found or disabled in AD", username);
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound,
                        "Пользователь не найден или отключен в Active Directory");
                }

                // Получаем или создаем пользователя в локальной базе
                var user = await GetOrCreateUserFromAD(userInfo);
                user.LastLoginAt = DateTime.UtcNow;
                
                // Обновляем только существующих пользователей
                if (user.Id > 0)
                {
                    await _userRepository.UpdateAsync(user);
                }

                await _auditService.LogLoginAsync(user.Username, true);

                return AuthenticationResult.Success(user, AuthenticationType.DomainLDAP);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during domain authentication for user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Создание fallback пользователя
        /// </summary>
        private async Task<User> CreateFallbackUserAsync(string username, string fullUsername)
        {
            try
            {
                // Ищем пользователя в локальной базе
                var existingUser = await _userRepository.GetByUsernameAsync(username);
                if (existingUser != null && existingUser.IsActive)
                {
                    _logger.LogInformation("Found existing user in local database: {Username}", username);
                    existingUser.LastLoginAt = DateTime.UtcNow;
                    await _userRepository.UpdateAsync(existingUser);
                    return existingUser;
                }

                // Создаем нового fallback пользователя
                _logger.LogInformation("Creating fallback user: {Username}", username);

                var fallbackUser = new User
                {
                    Username = username,
                    DisplayName = GetDisplayNameFromIdentity(fullUsername),
                    Email = $"{username}@local",
                    Role = DetermineFallbackUserRole(username),
                    IsActive = true,
                    IsServiceAccount = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    Groups = GetFallbackUserGroups(username)
                };

                await _userRepository.AddAsync(fallbackUser);

                _logger.LogInformation("Created fallback user {Username} with role {Role}", username, fallbackUser.Role);
                return fallbackUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback user for {Username}", username);
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
                _logger.LogDebug("ServiceAdmin config loaded: Username={Username}, IsPasswordSet={IsPasswordSet}, IsEnabled={IsEnabled}",
                    config.ServiceAdmin?.Username, config.ServiceAdmin?.IsPasswordSet, config.ServiceAdmin?.IsEnabled);

                // Проверяем блокировку
                if (config.ServiceAdmin?.IsLocked == true &&
                    config.ServiceAdmin.UnlockTime.HasValue &&
                    DateTime.UtcNow < config.ServiceAdmin.UnlockTime.Value)
                {
                    var remainingTime = config.ServiceAdmin.UnlockTime.Value - DateTime.UtcNow;
                    var errorMsg = $"Учетная запись заблокирована. Разблокировка через {remainingTime.Minutes} минут";
                    return AuthenticationResult.Failure(AuthenticationStatus.AccountLocked, errorMsg);
                }

                // Сбрасываем блокировку если время истекло
                if (config.ServiceAdmin?.IsLockExpired == true)
                {
                    config.ServiceAdmin.ResetLockout();
                    await _configService.SaveConfigurationAsync(config);
                }

                // Проверяем учетные данные
                if (config.ServiceAdmin.Username != username)
                {
                    await RecordFailedServiceAdminLogin(config);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
                }

                // Проверяем пароль
                _logger.LogDebug("Service admin password verification: Username={Username}, HasPasswordHash={HasHash}, HasSalt={HasSalt}", 
                    username, !string.IsNullOrEmpty(config.ServiceAdmin.PasswordHash), !string.IsNullOrEmpty(config.ServiceAdmin.Salt));
                
                var isPasswordValid = VerifyPassword(password, config.ServiceAdmin.PasswordHash, config.ServiceAdmin.Salt);
                _logger.LogDebug("Service admin password verification result: {IsValid}", isPasswordValid);
                
                if (!isPasswordValid)
                {
                    _logger.LogWarning("Service admin password verification failed for user: {Username}", username);
                    await RecordFailedServiceAdminLogin(config);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Неверные учетные данные");
                }

                // Успешная аутентификация
                config.ServiceAdmin.RecordSuccessfulLogin();
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
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };
                    await _userRepository.AddAsync(user);
                }
                else
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userRepository.UpdateAsync(user);
                }

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
        /// Аутентификация локального пользователя (новый метод)
        /// </summary>
        private async Task<AuthenticationResult> AuthenticateLocalUserAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Authenticating local user: {Username}", username);

                var authResult = await _localUserService.AuthenticateLocalUserAsync(username, password);
                if (authResult.IsSuccess)
                {
                    return AuthenticationResult.Success(authResult.User!, AuthenticationType.LocalUsers);
                }
                else
                {
                    var status = authResult.Status switch
                    {
                        AuthenticationStatus.UserNotFound => AuthenticationStatus.UserNotFound,
                        AuthenticationStatus.InvalidCredentials => AuthenticationStatus.InvalidCredentials,
                        AuthenticationStatus.AccountDisabled => AuthenticationStatus.AccountDisabled,
                        AuthenticationStatus.AccountLocked => AuthenticationStatus.AccountLocked,
                        _ => AuthenticationStatus.NetworkError
                    };
                    
                    return AuthenticationResult.Failure(status, authResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating local user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Гостевая аутентификация без пароля
        /// </summary>
        private async Task<AuthenticationResult> AuthenticateGuestAsync(string username)
        {
            try
            {
                _logger.LogInformation("Authenticating guest user: {Username}", username);

                // Проверяем, что это именно пользователь guest
                if (!username.Equals("guest", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Guest authentication attempted with invalid username: {Username}", username);
                    return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Гостевой вход доступен только для пользователя 'guest'");
                }

                // Ищем или создаем пользователя guest в базе данных
                var guestUser = await GetOrCreateGuestUserAsync();
                if (guestUser == null)
                {
                    _logger.LogError("Failed to create or find guest user");
                    return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, "Не удалось создать гостевого пользователя");
                }

                // Проверяем активность
                if (!guestUser.IsActive)
                {
                    await _auditService.LogLoginAsync(username, false, "Guest account is inactive");
                    return AuthenticationResult.Failure(AuthenticationStatus.UserNotFound, "Гостевой аккаунт отключен");
                }

                // Обновляем данные входа
                guestUser.LastLoginAt = DateTime.UtcNow;
                guestUser.LastActivityAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(guestUser);

                await _auditService.LogLoginAsync(username, true, "Guest login successful");
                _logger.LogInformation("Guest authentication successful for user: {Username}", username);

                return AuthenticationResult.Success(guestUser, AuthenticationType.Guest);
            }
            catch (Exception ex)
            {
                await _auditService.LogLoginAsync(username, false, ex.Message);
                _logger.LogError(ex, "Error authenticating guest user {Username}", username);
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        /// <summary>
        /// Получить или создать пользователя guest
        /// </summary>
        private async Task<User> GetOrCreateGuestUserAsync()
        {
            try
            {
                // Ищем существующего пользователя guest
                var existingUser = await _userRepository.GetByUsernameAsync("guest");
                if (existingUser != null)
                {
                    _logger.LogInformation("Found existing guest user with ID: {UserId}", existingUser.Id);
                    return existingUser;
                }

                // Создаем нового пользователя guest
                _logger.LogInformation("Creating new guest user");

                var guestUser = new User
                {
                    Username = "guest",
                    DisplayName = "Гостевой пользователь",
                    Email = "guest@local",
                    Role = UserRole.Guest,
                    AuthenticationType = AuthenticationType.Guest,
                    IsActive = true,
                    IsServiceAccount = false,
                    IsLocalUser = false,
                    AllowLocalLogin = false,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    Groups = new List<string> { "GuestUsers" },
                    // Для гостевого пользователя пароль не нужен
                    PasswordHash = string.Empty,
                    Salt = string.Empty
                };

                await _userRepository.AddAsync(guestUser);
                await _auditService.LogEventAsync("system", "User.Create", $"Created guest user: {guestUser.Username}", true);

                _logger.LogInformation("Created guest user with ID: {UserId}", guestUser.Id);
                return guestUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or finding guest user");
                throw;
            }
        }

        /// <summary>
        /// Внутренний метод аутентификации локального пользователя (старый метод для fallback)
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
        /// Определение роли fallback пользователя
        /// </summary>
        private UserRole DetermineFallbackUserRole(string username)
        {
            // Простая логика определения роли по имени пользователя
            var lowerUsername = username.ToLower();

            if (lowerUsername.Contains("admin") || lowerUsername.Contains("administrator"))
            {
                return UserRole.Administrator;
            }

            if (lowerUsername.Contains("power") || lowerUsername.Contains("advanced"))
            {
                return UserRole.PowerUser;
            }

            // Проверяем, является ли пользователь членом локальной группы администраторов
            try
            {
                var currentPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                if (currentPrincipal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    return UserRole.Administrator;
                }

                if (currentPrincipal.IsInRole(WindowsBuiltInRole.PowerUser))
                {
                    return UserRole.PowerUser;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking Windows roles for user {Username}", username);
            }

            return UserRole.Standard;
        }

        /// <summary>
        /// Получение групп для fallback пользователя
        /// </summary>
        private System.Collections.Generic.List<string> GetFallbackUserGroups(string username)
        {
            var groups = new System.Collections.Generic.List<string> { "LauncherUsers" };

            // Добавляем группы на основе роли
            var role = DetermineFallbackUserRole(username);
            switch (role)
            {
                case UserRole.Administrator:
                    groups.Add("LauncherAdmins");
                    groups.Add("LauncherPowerUsers");
                    break;
                case UserRole.PowerUser:
                    groups.Add("LauncherPowerUsers");
                    break;
            }

            return groups;
        }

        /// <summary>
        /// Получение отображаемого имени из Windows Identity
        /// </summary>
        private string GetDisplayNameFromIdentity(string fullUsername)
        {
            try
            {
                // Пытаемся получить полное имя пользователя из Windows
                var userName = ExtractUsernameFromDomain(fullUsername);

                // Здесь можно добавить дополнительную логику для получения DisplayName
                // например, через DirectoryServices или WMI

                return userName; // Пока возвращаем просто имя пользователя
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting display name for {FullUsername}", fullUsername);
                return ExtractUsernameFromDomain(fullUsername);
            }
        }

        /// <summary>
        /// Генерация хэша пароля с солью
        /// </summary>
        private (string salt, string hash) GeneratePasswordHash(string password)
        {
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            var salt = Convert.ToBase64String(saltBytes);

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
                if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                {
                    _logger.LogDebug("Password verification failed: missing required data. Password={HasPassword}, Hash={HasHash}, Salt={HasSalt}",
                        !string.IsNullOrEmpty(password), !string.IsNullOrEmpty(storedHash), !string.IsNullOrEmpty(storedSalt));
                    return false;
                }

                var saltBytes = Convert.FromBase64String(storedSalt);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
                var hashBytes = pbkdf2.GetBytes(32);
                var computedHash = Convert.ToBase64String(hashBytes);

                var isMatch = computedHash == storedHash;
                _logger.LogDebug("Password hash comparison: StoredHash={StoredHashLength}, ComputedHash={ComputedHashLength}, Match={Match}",
                    storedHash?.Length ?? 0, computedHash?.Length ?? 0, isMatch);

                return isMatch;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password verification");
                return false;
            }
        }

        /// <summary>
        /// Записать неудачную попытку входа сервисного администратора
        /// </summary>
        private async Task RecordFailedServiceAdminLogin(AuthenticationConfiguration config)
        {
            config.ServiceAdmin.RecordFailedLogin();

            if (config.ServiceAdmin.IsLocked)
            {
                _logger.LogWarning("Service admin account locked due to too many failed attempts");
            }

            await _configService.SaveConfigurationAsync(config);
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
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    Groups = userInfo.Groups
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                // Обновляем информацию пользователя из AD
                user.DisplayName = userInfo.DisplayName;
                user.Email = userInfo.Email;
                user.IsActive = userInfo.IsEnabled;
                user.Groups = userInfo.Groups;

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
            if (groups == null || groups.Count == 0)
                return UserRole.Standard;

            var adminGroups = config.AdminGroups?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                ?? new System.Collections.Generic.List<string> { "LauncherAdmins", "Domain Admins" };
            var powerUserGroups = config.PowerUserGroups?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                ?? new System.Collections.Generic.List<string> { "LauncherPowerUsers" };

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
            if (string.IsNullOrEmpty(domainUsername))
                return string.Empty;

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

        /// <summary>
        /// Проверить разрешение для Guest пользователя
        /// </summary>
        private bool IsPermissionAllowedForGuestUser(string permission)
        {
            var allowedPermissions = new[]
            {
                "LaunchApplication", // Только запуск разрешенных для гостей приложений
                "ViewApplications"   // Просмотр только гостевых приложений
            };

            return allowedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
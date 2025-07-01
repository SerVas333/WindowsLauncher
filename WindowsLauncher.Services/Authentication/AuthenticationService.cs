using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Гибридный сервис аутентификации с поддержкой Windows SSO, LDAP и сервисного администратора
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IActiveDirectoryService _adService;
        private readonly IAuthenticationConfigurationService _configService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly ActiveDirectoryConfiguration _adConfig;

        private AuthenticationSession _currentSession;

        public AuthenticationSession CurrentSession => _currentSession;

        public event EventHandler<AuthenticationResult> AuthenticationChanged;

        public AuthenticationService(
            IActiveDirectoryService adService,
            IAuthenticationConfigurationService configService,
            IAuditService auditService,
            ILogger<AuthenticationService> logger,
            IOptions<ActiveDirectoryConfiguration> adConfig)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _adConfig = adConfig?.Value ?? throw new ArgumentNullException(nameof(adConfig));
        }

        /// <summary>
        /// Автоматическая аутентификация с приоритетом Windows SSO
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync()
        {
            _logger.LogInformation("Starting automatic authentication process");

            try
            {
                // 1. Пробуем Windows SSO (если компьютер в домене)
                if (IsComputerInDomain())
                {
                    _logger.LogDebug("Computer is domain-joined, attempting Windows SSO");
                    var windowsResult = await AuthenticateWindowsAsync();

                    if (windowsResult.IsSuccess)
                    {
                        await LogAuthenticationAsync(windowsResult);
                        return windowsResult;
                    }

                    _logger.LogWarning("Windows SSO failed: {Error}", windowsResult.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("Computer is not domain-joined, Windows SSO unavailable");
                }

                // 2. Проверяем доступность домена для LDAP
                var isDomainAvailable = await IsDomainAvailableAsync();
                if (!isDomainAvailable)
                {
                    _logger.LogWarning("Domain is not available for LDAP authentication");

                    // Если домен недоступен, предлагаем только сервисный режим
                    var serviceResult = AuthenticationResult.Failure(
                        AuthenticationStatus.ServiceModeRequired,
                        "Домен недоступен. Доступен только режим сервисного администратора."
                    );

                    AuthenticationChanged?.Invoke(this, serviceResult);
                    return serviceResult;
                }

                // 3. Запрашиваем учетные данные для LDAP аутентификации
                _logger.LogInformation("Requesting user credentials for LDAP authentication");
                var credentialsResult = await RequestUserCredentialsAsync();

                if (credentialsResult != null)
                {
                    var ldapResult = await AuthenticateLdapAsync(credentialsResult);
                    await LogAuthenticationAsync(ldapResult);
                    return ldapResult;
                }

                // 4. Пользователь отменил ввод учетных данных
                var cancelledResult = AuthenticationResult.Failure(
                    AuthenticationStatus.Cancelled,
                    "Аутентификация отменена пользователем"
                );

                AuthenticationChanged?.Invoke(this, cancelledResult);
                return cancelledResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication");
                var errorResult = AuthenticationResult.Failure(
                    AuthenticationStatus.NetworkError,
                    $"Ошибка аутентификации: {ex.Message}"
                );

                AuthenticationChanged?.Invoke(this, errorResult);
                return errorResult;
            }
        }

        /// <summary>
        /// Аутентификация с явными учетными данными
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationCredentials credentials)
        {
            if (credentials == null || !credentials.IsValid())
            {
                return AuthenticationResult.Failure(
                    AuthenticationStatus.InvalidCredentials,
                    "Некорректные учетные данные"
                );
            }

            _logger.LogInformation("Authenticating user {Username} from domain {Domain}",
                credentials.Username, credentials.Domain ?? "default");

            try
            {
                AuthenticationResult result;

                // Проверяем, является ли это сервисным администратором
                if (credentials.IsServiceAccount || credentials.Username.Equals(_adConfig.ServiceAdmin.Username, StringComparison.OrdinalIgnoreCase))
                {
                    result = await AuthenticateServiceAdminAsync(credentials.Username, credentials.Password);
                }
                else
                {
                    result = await AuthenticateLdapAsync(credentials);
                }

                await LogAuthenticationAsync(result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user {Username}", credentials.Username);
                return AuthenticationResult.Failure(
                    AuthenticationStatus.NetworkError,
                    $"Ошибка аутентификации: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Windows SSO аутентификация
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateWindowsAsync()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();

                if (identity == null || string.IsNullOrEmpty(identity.Name))
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Не удалось получить информацию о текущем пользователе Windows"
                    );
                }

                _logger.LogDebug("Windows identity: {Identity}", identity.Name);

                // Проверяем, что это доменный пользователь
                if (!identity.Name.Contains("\\") || identity.Name.StartsWith(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Пользователь не является доменным"
                    );
                }

                // Получаем информацию о пользователе из AD
                var parts = identity.Name.Split('\\');
                var domain = parts[0];
                var username = parts[1];

                var user = await _adService.GetUserAsync(username, domain);
                if (user == null)
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.UserNotFound,
                        $"Пользователь {username} не найден в домене {domain}"
                    );
                }

                // Создаем сессию
                _currentSession = new AuthenticationSession
                {
                    User = user,
                    AuthType = AuthenticationType.WindowsSSO,
                    Domain = domain,
                    ExpiresAt = DateTime.Now.AddHours(8) // 8 часов для Windows SSO
                };

                var result = AuthenticationResult.Success(user, AuthenticationType.WindowsSSO, domain);
                result.Metadata["WindowsIdentity"] = identity.Name;
                result.Metadata["AuthenticationMethod"] = "Integrated Windows Authentication";

                _logger.LogInformation("Windows SSO authentication successful for {User}", identity.Name);
                AuthenticationChanged?.Invoke(this, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Windows SSO authentication failed");
                return AuthenticationResult.Failure(
                    AuthenticationStatus.NetworkError,
                    $"Ошибка Windows SSO: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// LDAP аутентификация для недоменных компьютеров
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateLdapAsync(AuthenticationCredentials credentials)
        {
            if (credentials == null || !credentials.IsValid())
            {
                return AuthenticationResult.Failure(
                    AuthenticationStatus.InvalidCredentials,
                    "Некорректные учетные данные"
                );
            }

            try
            {
                var domain = credentials.Domain ?? _adConfig.Domain;

                _logger.LogDebug("Attempting LDAP authentication for {User}@{Domain}",
                    credentials.Username, domain);

                // Проверяем учетные данные в AD
                var isValid = await _adService.ValidateCredentialsAsync(
                    credentials.Username,
                    credentials.Password,
                    domain
                );

                if (!isValid)
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Неверные учетные данные"
                    );
                }

                // Получаем информацию о пользователе
                var user = await _adService.GetUserAsync(credentials.Username, domain);
                if (user == null)
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.UserNotFound,
                        $"Пользователь {credentials.Username} не найден в домене {domain}"
                    );
                }

                // Создаем сессию
                _currentSession = new AuthenticationSession
                {
                    User = user,
                    AuthType = AuthenticationType.DomainLDAP,
                    Domain = domain,
                    ExpiresAt = DateTime.Now.AddHours(4) // 4 часа для LDAP
                };

                var result = AuthenticationResult.Success(user, AuthenticationType.DomainLDAP, domain);
                result.Metadata["Domain"] = domain;
                result.Metadata["AuthenticationMethod"] = "LDAP";

                _logger.LogInformation("LDAP authentication successful for {User}@{Domain}",
                    credentials.Username, domain);
                AuthenticationChanged?.Invoke(this, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP authentication failed for {User}", credentials.Username);
                return AuthenticationResult.Failure(
                    AuthenticationStatus.NetworkError,
                    $"Ошибка LDAP аутентификации: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Аутентификация сервисного администратора
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateServiceAdminAsync(string username, string password)
        {
            try
            {
                if (!_adConfig.ServiceAdmin.IsEnabled)
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Режим сервисного администратора отключен"
                    );
                }

                if (!_adConfig.ServiceAdmin.IsPasswordSet)
                {
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Пароль сервисного администратора не установлен"
                    );
                }

                // Проверяем логин
                if (!username.Equals(_adConfig.ServiceAdmin.Username, StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(2000); // Защита от брутфорса
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Неверные учетные данные"
                    );
                }

                // Проверяем пароль
                if (!VerifyPassword(password, _adConfig.ServiceAdmin.PasswordHash, _adConfig.ServiceAdmin.Salt))
                {
                    await Task.Delay(2000); // Защита от брутфорса
                    return AuthenticationResult.Failure(
                        AuthenticationStatus.InvalidCredentials,
                        "Неверные учетные данные"
                    );
                }

                // Создаем локального пользователя-администратора
                var serviceUser = new User
                {
                    Id = Guid.Empty,
                    Username = _adConfig.ServiceAdmin.Username,
                    DisplayName = "Service Administrator",
                    Email = "serviceadmin@local",
                    Role = UserRole.Administrator,
                    IsActive = true
                };

                // Создаем сессию
                _currentSession = new AuthenticationSession
                {
                    User = serviceUser,
                    AuthType = AuthenticationType.LocalService,
                    Domain = "LOCAL",
                    ExpiresAt = DateTime.Now.AddMinutes(_adConfig.ServiceAdmin.SessionTimeoutMinutes)
                };

                var result = AuthenticationResult.Success(serviceUser, AuthenticationType.LocalService, "LOCAL");
                result.Metadata["IsServiceAdmin"] = true;
                result.Metadata["AuthenticationMethod"] = "Local Service Account";

                _logger.LogInformation("Service administrator authentication successful");
                AuthenticationChanged?.Invoke(this, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service administrator authentication failed");
                return AuthenticationResult.Failure(
                    AuthenticationStatus.NetworkError,
                    $"Ошибка аутентификации администратора: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Проверка доступности домена
        /// </summary>
        public async Task<bool> IsDomainAvailableAsync(string domain = null)
        {
            try
            {
                domain ??= _adConfig.Domain;

                if (string.IsNullOrEmpty(domain))
                    return false;

                // Пингуем LDAP сервер
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(_adConfig.LdapServer ?? domain, 5000);

                if (reply.Status != IPStatus.Success)
                    return false;

                // Проверяем LDAP порт
                return await _adService.TestConnectionAsync(_adConfig.LdapServer ?? domain, _adConfig.Port);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Domain availability check failed: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Проверка, находится ли компьютер в домене
        /// </summary>
        public bool IsComputerInDomain()
        {
            try
            {
                var domain = Environment.UserDomainName;
                var computerName = Environment.MachineName;

                // Если имя домена совпадает с именем компьютера, то компьютер не в домене
                return !string.Equals(domain, computerName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Выход из системы
        /// </summary>
        public async Task LogoutAsync()
        {
            if (_currentSession != null)
            {
                await _auditService.LogAsync(new AuditLog
                {
                    UserId = _currentSession.User.Id,
                    Action = "Logout",
                    Details = $"User {_currentSession.User.Username} logged out",
                    IpAddress = _currentSession.IpAddress,
                    UserAgent = Environment.MachineName
                });

                _currentSession.IsActive = false;
                _currentSession = null;

                _logger.LogInformation("User logged out successfully");
            }

            AuthenticationChanged?.Invoke(this, AuthenticationResult.Failure(AuthenticationStatus.Cancelled, "Logged out"));
        }

        /// <summary>
        /// Обновление сессии
        /// </summary>
        public async Task RefreshSessionAsync()
        {
            if (_currentSession == null || !_currentSession.IsValid)
                return;

            // Продлеваем сессию в зависимости от типа аутентификации
            var extensionTime = _currentSession.AuthType switch
            {
                AuthenticationType.WindowsSSO => TimeSpan.FromHours(8),
                AuthenticationType.DomainLDAP => TimeSpan.FromHours(4),
                AuthenticationType.LocalService => TimeSpan.FromMinutes(_adConfig.ServiceAdmin.SessionTimeoutMinutes),
                _ => TimeSpan.FromHours(1)
            };

            _currentSession.ExtendSession(extensionTime);

            _logger.LogDebug("Session extended for user {User} until {ExpiresAt}",
                _currentSession.User.Username, _currentSession.ExpiresAt);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Проверка активности сессии
        /// </summary>
        public bool IsSessionValid()
        {
            return _currentSession?.IsValid ?? false;
        }

        /// <summary>
        /// Настройка пароля сервисного администратора (только при первом запуске)
        /// </summary>
        public async Task<bool> SetupServiceAdminPasswordAsync(string password)
        {
            try
            {
                if (_adConfig.ServiceAdmin.IsPasswordSet)
                {
                    _logger.LogWarning("Attempt to setup service admin password when already configured");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                {
                    _logger.LogWarning("Invalid password provided for service admin setup");
                    return false;
                }

                // Генерируем соль и хешируем пароль
                var salt = GenerateSalt();
                var hash = HashPassword(password, salt);

                // Обновляем конфигурацию
                var config = _configService.GetConfiguration();
                config.ServiceAdmin.PasswordHash = hash;
                config.ServiceAdmin.Salt = salt;
                config.ServiceAdmin.IsEnabled = true;

                await _configService.SaveConfigurationAsync(config);

                _logger.LogInformation("Service administrator password configured successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup service administrator password");
                return false;
            }
        }

        /// <summary>
        /// Проверка, настроен ли сервисный администратор
        /// </summary>
        public bool IsServiceAdminConfigured()
        {
            return _adConfig.ServiceAdmin.IsPasswordSet;
        }

        #region Private Methods

        /// <summary>
        /// Запрос учетных данных у пользователя
        /// </summary>
        private async Task<AuthenticationCredentials> RequestUserCredentialsAsync()
        {
            try
            {
                // Здесь будет показан LoginWindow
                // Пока возвращаем null для демонстрации
                await Task.Delay(100);

                // TODO: Реализовать показ LoginWindow
                // var loginWindow = new LoginWindow();
                // if (loginWindow.ShowDialog() == true)
                // {
                //     return new AuthenticationCredentials
                //     {
                //         Username = loginWindow.Username,
                //         Password = loginWindow.Password,
                //         Domain = loginWindow.Domain
                //     };
                // }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting user credentials");
                return null;
            }
        }

        /// <summary>
        /// Логирование результата аутентификации
        /// </summary>
        private async Task LogAuthenticationAsync(AuthenticationResult result)
        {
            try
            {
                var logEntry = new AuditLog
                {
                    UserId = result.User?.Id ?? Guid.Empty,
                    Action = result.IsSuccess ? "Login_Success" : "Login_Failed",
                    Details = result.IsSuccess
                        ? $"User {result.User?.Username} authenticated via {result.AuthType}"
                        : $"Authentication failed: {result.ErrorMessage}",
                    IpAddress = GetLocalIpAddress(),
                    UserAgent = Environment.MachineName,
                    Timestamp = DateTime.Now
                };

                // Добавляем метаданные
                if (result.Metadata?.Count > 0)
                {
                    logEntry.Details += $" | Metadata: {string.Join(", ", result.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}";
                }

                await _auditService.LogAsync(logEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log authentication result");
            }
        }

        /// <summary>
        /// Генерация соли для хеширования
        /// </summary>
        private static string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Хеширование пароля с солью
        /// </summary>
        private static string HashPassword(string password, string salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                Convert.FromBase64String(salt),
                100000, // 100,000 итераций
                HashAlgorithmName.SHA256
            );

            var hash = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Проверка пароля
        /// </summary>
        private static bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }

        /// <summary>
        /// Получение локального IP адреса
        /// </summary>
        private static string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                var localIp = host.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                         && !System.Net.IPAddress.IsLoopback(ip));
                return localIp?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        #endregion
    }
}
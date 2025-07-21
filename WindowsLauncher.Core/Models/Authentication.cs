using System;
using System.Collections.Generic;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Типы аутентификации в системе
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// Windows SSO (компьютер в домене)
        /// </summary>
        WindowsSSO,

        /// <summary>
        /// Доменная через LDAP (компьютер вне домена)
        /// </summary>
        DomainLDAP,

        /// <summary>
        /// Локальный сервисный администратор (устаревший)
        /// </summary>
        LocalService,

        /// <summary>
        /// Локальные пользователи (полноценные аккаунты)
        /// </summary>
        LocalUsers,

        /// <summary>
        /// Гостевой доступ без пароля
        /// </summary>
        Guest,

        /// <summary>
        /// Кэшированные доменные пользователи (offline режим)
        /// </summary>
        CachedDomain
    }

    /// <summary>
    /// Статус аутентификации
    /// ✅ РАСШИРЕН для совместимости
    /// </summary>
    public enum AuthenticationStatus
    {
        Success,
        InvalidCredentials,
        UserNotFound,
        DomainUnavailable,
        NetworkError,
        Cancelled,
        ServiceModeRequired,
        AccountLocked,
        // ✅ ДОБАВЛЕНЫ дополнительные статусы
        Failed,
        Error,
        PasswordExpired,
        Locked,
        AccountDisabled
    }

    /// <summary>
    /// Результат аутентификации
    /// ✅ ИСПРАВЛЕНО: Добавлено свойство Message для совместимости
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public bool IsSuccessful => IsSuccess; // Для совместимости
        public AuthenticationStatus Status { get; set; }
        public User? User { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// ✅ ДОБАВЛЕНО: Общее сообщение (алиас для ErrorMessage для совместимости)
        /// </summary>
        public string Message
        {
            get => string.IsNullOrEmpty(ErrorMessage) && IsSuccess ? "Authentication successful" : ErrorMessage;
            set => ErrorMessage = value;
        }

        public string Domain { get; set; } = string.Empty;
        public DateTime AuthenticatedAt { get; set; } = DateTime.Now;

        // Дополнительные свойства для совместимости с AD
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public UserRole Role { get; set; }

        /// <summary>
        /// Дополнительные данные для логирования
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// ✅ ДОБАВЛЕНЫ дополнительные свойства для полной совместимости
        /// </summary>
        public string? Token { get; set; }
        public DateTime? TokenExpiration { get; set; }
        public string? ClientIpAddress { get; set; }
        public string? ClientUserAgent { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
        public int AttemptCount { get; set; } = 1;
        public List<string> Warnings { get; set; } = new();
        public bool RequiresPasswordChange { get; set; } = false;
        public DateTime? LockedUntil { get; set; }

        public static AuthenticationResult Success(User user, AuthenticationType authType, string? domain = null)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                Status = AuthenticationStatus.Success,
                User = user,
                AuthType = authType,
                Domain = domain ?? string.Empty,
                Username = user.Username,
                DisplayName = user.FullName, // ✅ ИСПРАВЛЕНО: Используем FullName
                Email = user.Email,
                Groups = user.Groups,
                Role = user.Role,
                Message = "Authentication successful"
            };
        }

        public static AuthenticationResult Failure(AuthenticationStatus status, string error)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                Status = status,
                ErrorMessage = error,
                Message = error
            };
        }

        /// <summary>
        /// ✅ ДОБАВЛЕНЫ дополнительные статические методы для удобства
        /// </summary>
        public static AuthenticationResult Error(string errorMessage, Exception? exception = null)
        {
            var result = new AuthenticationResult
            {
                IsSuccess = false,
                Status = AuthenticationStatus.Error,
                ErrorMessage = errorMessage,
                Message = errorMessage
            };

            if (exception != null)
            {
                result.AdditionalData["Exception"] = exception.ToString();
                result.AdditionalData["ExceptionType"] = exception.GetType().Name;
            }

            return result;
        }

        public static AuthenticationResult Locked(DateTime lockedUntil, string message = "Account is locked")
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                Status = AuthenticationStatus.Locked,
                ErrorMessage = message,
                Message = message,
                LockedUntil = lockedUntil
            };
        }

        public static AuthenticationResult PasswordExpired(string message = "Password has expired")
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                Status = AuthenticationStatus.PasswordExpired,
                ErrorMessage = message,
                Message = message,
                RequiresPasswordChange = true
            };
        }

        /// <summary>
        /// Добавление предупреждения
        /// </summary>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning) && !Warnings.Contains(warning))
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// Проверка наличия предупреждений
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Получение всех предупреждений как строки
        /// </summary>
        public string GetWarningsText()
        {
            return string.Join("; ", Warnings);
        }

        /// <summary>
        /// Копирование результата
        /// </summary>
        public AuthenticationResult Clone()
        {
            return new AuthenticationResult
            {
                IsSuccess = IsSuccess,
                User = User?.Clone(),
                Status = Status,
                AuthType = AuthType,
                ErrorMessage = ErrorMessage,
                Domain = Domain,
                AuthenticatedAt = AuthenticatedAt,
                Username = Username,
                DisplayName = DisplayName,
                Email = Email,
                Groups = new List<string>(Groups),
                Role = Role,
                Metadata = new Dictionary<string, object>(Metadata),
                Token = Token,
                TokenExpiration = TokenExpiration,
                ClientIpAddress = ClientIpAddress,
                ClientUserAgent = ClientUserAgent,
                AdditionalData = new Dictionary<string, object>(AdditionalData),
                AttemptCount = AttemptCount,
                Warnings = new List<string>(Warnings),
                RequiresPasswordChange = RequiresPasswordChange,
                LockedUntil = LockedUntil
            };
        }

        public override string ToString()
        {
            var statusText = IsSuccess ? "Success" : $"Failed ({Status})";
            var userText = User?.Username ?? Username ?? "No user";
            return $"AuthResult: {statusText} - {userText} - {Message}";
        }
    }

    /// <summary>
    /// Учетные данные для аутентификации
    /// </summary>
    public class AuthenticationCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool IsServiceAccount { get; set; }
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.DomainLDAP;

        /// <summary>
        /// Валидация учетных данных
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        /// <summary>
        /// Полное имя пользователя для логирования (без пароля)
        /// </summary>
        public string FullUsername => string.IsNullOrEmpty(Domain) ? Username : $"{Domain}\\{Username}";
    }

    /// <summary>
    /// Конфигурация Active Directory
    /// </summary>
    public class ActiveDirectoryConfiguration
    {
        public string Domain { get; set; } = string.Empty;
        public string LdapServer { get; set; } = string.Empty;
        public int Port { get; set; } = 389;
        public bool UseTLS { get; set; } = true;
        public string ServiceUser { get; set; } = string.Empty;
        public string ServicePassword { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public bool RequireDomainMembership { get; set; } = false;

        /// <summary>
        /// Группы администраторов
        /// </summary>
        public string AdminGroups { get; set; } = "LauncherAdmins";

        /// <summary>
        /// Группы продвинутых пользователей
        /// </summary>
        public string PowerUserGroups { get; set; } = "LauncherPowerUsers";

        /// <summary>
        /// Включить fallback режим при недоступности AD
        /// </summary>
        public bool EnableFallbackMode { get; set; } = true;

        /// <summary>
        /// Список доверенных доменов
        /// </summary>
        public List<string> TrustedDomains { get; set; } = new();

        /// <summary>
        /// Настройки сервисного администратора
        /// </summary>
        public ServiceAdminConfiguration ServiceAdmin { get; set; } = new();
    }

    /// <summary>
    /// Информация о текущей сессии
    /// </summary>
    public class AuthenticationSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public User? User { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string Domain { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public string ComputerName { get; set; } = Environment.MachineName;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Проверка активности сессии
        /// </summary>
        public bool IsValid => IsActive && DateTime.Now < ExpiresAt;

        /// <summary>
        /// Продление сессии
        /// </summary>
        public void ExtendSession(TimeSpan duration)
        {
            ExpiresAt = DateTime.Now.Add(duration);
        }
    }

    /// <summary>
    /// Информация о пользователе AD
    /// </summary>
    public class AdUserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastLogon { get; set; }
        public DateTime? PasswordLastSet { get; set; }
    }

    /// <summary>
    /// ✅ ДОБАВЛЕНО: Методы аутентификации для совместимости
    /// </summary>
    public enum AuthenticationMethod
    {
        /// <summary>
        /// Аутентификация через Active Directory
        /// </summary>
        Domain = 0,

        /// <summary>
        /// Локальная служебная учетная запись
        /// </summary>
        ServiceAccount = 1,

        /// <summary>
        /// Windows аутентификация
        /// </summary>
        Windows = 2,

        /// <summary>
        /// Токен аутентификации
        /// </summary>
        Token = 3,

        /// <summary>
        /// Тестовый режим
        /// </summary>
        Test = 99
    }
}
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
        /// Локальный сервисный администратор
        /// </summary>
        LocalService
    }

    /// <summary>
    /// Статус аутентификации
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
        AccountLocked
    }

    /// <summary>
    /// Результат аутентификации
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public bool IsSuccessful => IsSuccess; // Для совместимости
        public AuthenticationStatus Status { get; set; }
        public User? User { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
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
                DisplayName = user.DisplayName,
                Email = user.Email,
                Groups = user.Groups,
                Role = user.Role
            };
        }

        public static AuthenticationResult Failure(AuthenticationStatus status, string error)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                Status = status,
                ErrorMessage = error
            };
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
}
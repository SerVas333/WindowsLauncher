using System;
using System.Collections.Generic;

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
        ServiceModeRequired
    }

    /// <summary>
    /// Результат аутентификации
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public AuthenticationStatus Status { get; set; }
        public User User { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string ErrorMessage { get; set; }
        public string Domain { get; set; }
        public DateTime AuthenticatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Дополнительные данные для логирования
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static AuthenticationResult Success(User user, AuthenticationType authType, string domain = null)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                Status = AuthenticationStatus.Success,
                User = user,
                AuthType = authType,
                Domain = domain
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
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
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
        public string Domain { get; set; }
        public string LdapServer { get; set; }
        public int Port { get; set; } = 389;
        public bool UseTLS { get; set; } = true;
        public string ServiceUser { get; set; }
        public string ServicePassword { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public bool RequireDomainMembership { get; set; } = false;

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
    /// Конфигурация сервисного администратора
    /// </summary>
    public class ServiceAdminConfiguration
    {
        /// <summary>
        /// Логин сервисного администратора (по умолчанию "serviceadmin")
        /// </summary>
        public string Username { get; set; } = "serviceadmin";

        /// <summary>
        /// Хеш пароля сервисного администратора
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Соль для хеширования
        /// </summary>
        public string Salt { get; set; }

        /// <summary>
        /// Включен ли режим сервисного администратора
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Время жизни сессии сервисного администратора (в минутах)
        /// </summary>
        public int SessionTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Проверка, установлен ли пароль
        /// </summary>
        public bool IsPasswordSet => !string.IsNullOrEmpty(PasswordHash) && !string.IsNullOrEmpty(Salt);
    }

    /// <summary>
    /// Информация о текущей сессии
    /// </summary>
    public class AuthenticationSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public User User { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string Domain { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public string ComputerName { get; set; } = Environment.MachineName;
        public string IpAddress { get; set; }
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
}
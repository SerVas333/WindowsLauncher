// WindowsLauncher.Core/Models/AuthenticationConfiguration.cs
// ТОЛЬКО МОДЕЛИ - БЕЗ СЕРВИСОВ!

using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Унифицированная конфигурация аутентификации
    /// </summary>
    public class AuthenticationConfiguration
    {
        /// <summary>
        /// Уникальный идентификатор конфигурации
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Доменное имя (например: company.local)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// LDAP сервер (например: dc.company.local)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string LdapServer { get; set; } = string.Empty;

        /// <summary>
        /// Порт LDAP (обычно 389 или 636 для LDAPS)
        /// </summary>
        [Range(1, 65535)]
        public int Port { get; set; } = 389;

        /// <summary>
        /// Использовать TLS/SSL шифрование
        /// </summary>
        public bool UseTLS { get; set; } = true;

        /// <summary>
        /// Таймаут подключения в секундах
        /// </summary>
        [Range(5, 300)]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Таймаут для совместимости с ActiveDirectoryConfiguration
        /// </summary>
        public int TimeoutSeconds
        {
            get => ConnectionTimeoutSeconds;
            set => ConnectionTimeoutSeconds = value;
        }

        /// <summary>
        /// Сервисный пользователь для подключения к AD
        /// </summary>
        public string ServiceUser { get; set; } = string.Empty;

        /// <summary>
        /// Пароль сервисного пользователя
        /// </summary>
        public string ServicePassword { get; set; } = string.Empty;

        /// <summary>
        /// Требовать членство в домене
        /// </summary>
        public bool RequireDomainMembership { get; set; } = false;

        /// <summary>
        /// Базовый DN для поиска (например: DC=company,DC=local)
        /// </summary>
        [StringLength(500)]
        public string BaseDN { get; set; } = string.Empty;

        /// <summary>
        /// Группы пользователей по умолчанию
        /// </summary>
        public string DefaultUserGroups { get; set; } = "LauncherUsers";

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

        /// <summary>
        /// Указывает, что конфигурация завершена
        /// </summary>
        public bool IsConfigured { get; set; } = false;

        /// <summary>
        /// Дата последнего изменения
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Включить кэширование данных AD
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Время жизни кэша в минутах
        /// </summary>
        [Range(1, 1440)]
        public int CacheLifetimeMinutes { get; set; } = 60;

        /// <summary>
        /// Неявное преобразование в ActiveDirectoryConfiguration для совместимости
        /// </summary>
        public static implicit operator ActiveDirectoryConfiguration(AuthenticationConfiguration config)
        {
            return new ActiveDirectoryConfiguration
            {
                Domain = config.Domain,
                LdapServer = config.LdapServer,
                Port = config.Port,
                UseTLS = config.UseTLS,
                ServiceUser = config.ServiceUser,
                ServicePassword = config.ServicePassword,
                TimeoutSeconds = config.TimeoutSeconds,
                RequireDomainMembership = config.RequireDomainMembership,
                AdminGroups = config.AdminGroups,
                PowerUserGroups = config.PowerUserGroups,
                EnableFallbackMode = config.EnableFallbackMode,
                TrustedDomains = config.TrustedDomains,
                ServiceAdmin = config.ServiceAdmin
            };
        }

        /// <summary>
        /// Неявное преобразование из ActiveDirectoryConfiguration для совместимости
        /// </summary>
        public static implicit operator AuthenticationConfiguration(ActiveDirectoryConfiguration config)
        {
            return new AuthenticationConfiguration
            {
                Domain = config.Domain,
                LdapServer = config.LdapServer,
                Port = config.Port,
                UseTLS = config.UseTLS,
                ServiceUser = config.ServiceUser,
                ServicePassword = config.ServicePassword,
                TimeoutSeconds = config.TimeoutSeconds,
                RequireDomainMembership = config.RequireDomainMembership,
                AdminGroups = config.AdminGroups,
                PowerUserGroups = config.PowerUserGroups,
                EnableFallbackMode = config.EnableFallbackMode,
                TrustedDomains = config.TrustedDomains,
                ServiceAdmin = config.ServiceAdmin
            };
        }
    }

    /// <summary>
    /// Конфигурация сервисного администратора
    /// </summary>
    public class ServiceAdminConfiguration
    {
        /// <summary>
        /// Имя пользователя сервисного администратора
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = "serviceadmin";

        /// <summary>
        /// Хэш пароля (BCrypt)
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Соль для хэширования
        /// </summary>
        [Required]
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Время жизни сессии в минутах
        /// </summary>
        [Range(5, 1440)]
        public int SessionTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Максимальное количество попыток входа
        /// </summary>
        [Range(3, 10)]
        public int MaxLoginAttempts { get; set; } = 5;

        /// <summary>
        /// Время блокировки в минутах после превышения попыток
        /// </summary>
        [Range(5, 60)]
        public int LockoutDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Последний успешный вход
        /// </summary>
        public DateTime? LastSuccessfulLogin { get; set; }

        /// <summary>
        /// Количество неудачных попыток подряд
        /// </summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Время последней неудачной попытки
        /// </summary>
        public DateTime? LastFailedLogin { get; set; }

        /// <summary>
        /// Учетная запись заблокирована
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Время разблокировки
        /// </summary>
        public DateTime? UnlockTime { get; set; }

        /// <summary>
        /// Требовать смену пароля при первом входе
        /// </summary>
        public bool RequirePasswordChange { get; set; } = true;

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата последнего изменения пароля
        /// </summary>
        public DateTime? LastPasswordChange { get; set; }

        /// <summary>
        /// Включен ли режим сервисного администратора
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Проверка, установлен ли пароль
        /// </summary>
        public bool IsPasswordSet => !string.IsNullOrEmpty(PasswordHash) && !string.IsNullOrEmpty(Salt);
    }
}
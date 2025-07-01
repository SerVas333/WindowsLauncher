using System;
using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Конфигурация аутентификации Active Directory
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
        /// Включить fallback режим при недоступности AD
        /// </summary>
        public bool EnableFallbackMode { get; set; } = true;
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
    }
}
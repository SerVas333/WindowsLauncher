// WindowsLauncher.Core/Models/User.cs - МИНИМАЛЬНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Модель пользователя системы
    /// </summary>
    [Table("USERS")]
    public class User
    {
        /// <summary>
        /// Уникальный идентификатор пользователя
        /// </summary>
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        /// <summary>
        /// Имя пользователя (логин)
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("USERNAME")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя пользователя
        /// </summary>
        [MaxLength(200)]
        [Column("DISPLAY_NAME")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Email адрес пользователя
        /// </summary>
        [MaxLength(255)]
        [Column("EMAIL")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Роль пользователя в системе
        /// </summary>
        [Required]
        [Column("ROLE")]
        public UserRole Role { get; set; } = UserRole.Standard;

        /// <summary>
        /// Активен ли пользователь
        /// </summary>
        [Column("IS_ACTIVE")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Является ли пользователь сервисным аккаунтом
        /// </summary>
        [Column("IS_SERVICE_ACCOUNT")]
        public bool IsServiceAccount { get; set; } = false;

        /// <summary>
        /// Хэш пароля (только для сервисных аккаунтов)
        /// </summary>
        [MaxLength(500)]
        [Column("PASSWORD_HASH")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Соль для хэширования пароля
        /// </summary>
        [MaxLength(500)]
        [Column("SALT")]
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Дата и время создания пользователя
        /// </summary>
        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата и время последнего входа
        /// </summary>
        [Column("LAST_LOGIN_AT")]
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Дата и время последней активности
        /// </summary>
        [Column("LAST_ACTIVITY_AT")]
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// Количество неудачных попыток входа
        /// </summary>
        [Column("FAILED_LOGIN_ATTEMPTS")]
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Заблокирован ли пользователь
        /// </summary>
        [Column("IS_LOCKED")]
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Время окончания блокировки
        /// </summary>
        [Column("LOCKOUT_END")]
        public DateTime? LockoutEnd { get; set; }

        /// <summary>
        /// Дата последней смены пароля
        /// </summary>
        [Column("LAST_PASSWORD_CHANGE")]
        public DateTime? LastPasswordChange { get; set; }

        /// <summary>
        /// Группы пользователя (сериализованный JSON)
        /// </summary>
        [Column("GROUPS_JSON")]
        [MaxLength(2000)]
        public string GroupsJson { get; set; } = "[]";

        /// <summary>
        /// Дополнительные настройки пользователя (JSON)
        /// </summary>
        [Column("SETTINGS_JSON")]
        [MaxLength(4000)]
        public string SettingsJson { get; set; } = "{}";

        /// <summary>
        /// Дополнительные метаданные (JSON)
        /// </summary>
        [Column("METADATA_JSON")]
        [MaxLength(2000)]
        public string MetadataJson { get; set; } = "{}";

        #region Новые поля для гибридной системы авторизации

        /// <summary>
        /// Тип аутентификации пользователя
        /// </summary>
        [Column("AUTHENTICATION_TYPE")]
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.LocalService;

        /// <summary>
        /// Доменное имя пользователя (для кэшированных доменных пользователей)
        /// </summary>
        [MaxLength(100)]
        [Column("DOMAIN_USERNAME")]
        public string DomainUsername { get; set; } = string.Empty;

        /// <summary>
        /// Время последней синхронизации с доменом
        /// </summary>
        [Column("LAST_DOMAIN_SYNC")]
        public DateTime? LastDomainSync { get; set; }

        /// <summary>
        /// Является ли пользователь локальным (не доменным)
        /// </summary>
        [Column("IS_LOCAL_USER")]
        public bool IsLocalUser { get; set; } = true;

        /// <summary>
        /// Разрешить локальный вход для доменного пользователя (offline режим)
        /// </summary>
        [Column("ALLOW_LOCAL_LOGIN")]
        public bool AllowLocalLogin { get; set; } = false;

        #endregion

        #region Свойства для совместимости (НЕ МАППЯТСЯ В БД)

        /// <summary>
        /// Группы пользователя (вычисляется из GroupsJson)
        /// </summary>
        [NotMapped]
        public List<string> Groups
        {
            get
            {
                try
                {
                    return string.IsNullOrEmpty(GroupsJson)
                        ? new List<string>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(GroupsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                try
                {
                    GroupsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                }
                catch
                {
                    GroupsJson = "[]";
                }
            }
        }

        /// <summary>
        /// Алиас для CreatedAt для совместимости
        /// </summary>
        [NotMapped]
        public DateTime CreatedDate
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }

        /// <summary>
        /// Алиас для LastLoginAt для совместимости
        /// </summary>
        [NotMapped]
        public DateTime LastLogin
        {
            get => LastLoginAt ?? DateTime.MinValue;
            set => LastLoginAt = value == DateTime.MinValue ? null : value;
        }

        /// <summary>
        /// GUID пользователя для совместимости с LoginWindow
        /// </summary>
        [NotMapped]
        public Guid UserId => new Guid($"{Id:D8}-0000-0000-0000-000000000000");

        /// <summary>
        /// Полное имя пользователя (алиас для DisplayName)
        /// </summary>
        [NotMapped]
        public string FullName
        {
            get => DisplayName;
            set => DisplayName = value;
        }

        #endregion

        #region Навигационные свойства

        /// <summary>
        /// Настройки пользователя
        /// </summary>
        public virtual UserSettings? UserSettings { get; set; }

        /// <summary>
        /// Логи аудита пользователя
        /// </summary>
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        #endregion

        #region Простые utility методы (только для удобства работы с моделью)

        /// <summary>
        /// Проверить, принадлежит ли пользователь к группе
        /// </summary>
        public bool IsInGroup(string groupName)
        {
            return Groups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Проверить минимальную роль
        /// </summary>
        public bool HasMinimumRole(UserRole minRole)
        {
            return Role >= minRole;
        }

        /// <summary>
        /// Является ли пользователь доменным (живым или кэшированным)
        /// </summary>
        public bool IsDomainUser()
        {
            return AuthenticationType == AuthenticationType.DomainLDAP || 
                   AuthenticationType == AuthenticationType.WindowsSSO ||
                   AuthenticationType == AuthenticationType.CachedDomain;
        }

        /// <summary>
        /// Можно ли использовать для offline входа
        /// </summary>
        public bool CanLoginOffline()
        {
            return IsLocalUser || 
                   (IsDomainUser() && AllowLocalLogin && !string.IsNullOrEmpty(PasswordHash));
        }

        /// <summary>
        /// Нужна ли синхронизация с доменом
        /// </summary>
        public bool RequiresDomainSync(TimeSpan maxAge)
        {
            if (!IsDomainUser()) return false;
            if (LastDomainSync == null) return true;
            return DateTime.UtcNow - LastDomainSync.Value > maxAge;
        }

        /// <summary>
        /// Обновить время синхронизации с доменом
        /// </summary>
        public void UpdateDomainSync()
        {
            if (IsDomainUser())
            {
                LastDomainSync = DateTime.UtcNow;
            }
        }

        #endregion

        #region Переопределения

        public override string ToString()
        {
            return $"{DisplayName} ({Username}) - {Role}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is User other)
            {
                return Id == other.Id && Username == other.Username;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Username);
        }

        /// <summary>
        /// Создание копии пользователя
        /// </summary>
        public User Clone()
        {
            return new User
            {
                Id = Id,
                Username = Username,
                DisplayName = DisplayName,
                Email = Email,
                Role = Role,
                IsActive = IsActive,
                IsServiceAccount = IsServiceAccount,
                PasswordHash = PasswordHash,
                Salt = Salt,
                CreatedAt = CreatedAt,
                LastLoginAt = LastLoginAt,
                LastActivityAt = LastActivityAt,
                FailedLoginAttempts = FailedLoginAttempts,
                IsLocked = IsLocked,
                LockoutEnd = LockoutEnd,
                LastPasswordChange = LastPasswordChange,
                GroupsJson = GroupsJson,
                SettingsJson = SettingsJson,
                MetadataJson = MetadataJson,
                // Новые поля для гибридной авторизации
                AuthenticationType = AuthenticationType,
                DomainUsername = DomainUsername,
                LastDomainSync = LastDomainSync,
                IsLocalUser = IsLocalUser,
                AllowLocalLogin = AllowLocalLogin
            };
        }

        #endregion
    }
}
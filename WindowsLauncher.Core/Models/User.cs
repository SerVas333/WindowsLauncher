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
    [Table("Users")]
    public class User
    {
        /// <summary>
        /// Уникальный идентификатор пользователя
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Имя пользователя (логин)
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("Username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя пользователя
        /// </summary>
        [MaxLength(200)]
        [Column("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Email адрес пользователя
        /// </summary>
        [MaxLength(255)]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Роль пользователя в системе
        /// </summary>
        [Required]
        [Column("Role")]
        public UserRole Role { get; set; } = UserRole.Standard;

        /// <summary>
        /// Активен ли пользователь
        /// </summary>
        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Является ли пользователь сервисным аккаунтом
        /// </summary>
        [Column("IsServiceAccount")]
        public bool IsServiceAccount { get; set; } = false;

        /// <summary>
        /// Хэш пароля (только для сервисных аккаунтов)
        /// </summary>
        [MaxLength(500)]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Соль для хэширования пароля
        /// </summary>
        [MaxLength(500)]
        [Column("Salt")]
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Дата и время создания пользователя
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата и время последнего входа
        /// </summary>
        [Column("LastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Дата и время последней активности
        /// </summary>
        [Column("LastActivityAt")]
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// Количество неудачных попыток входа
        /// </summary>
        [Column("FailedLoginAttempts")]
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Заблокирован ли пользователь
        /// </summary>
        [Column("IsLocked")]
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Время окончания блокировки
        /// </summary>
        [Column("LockoutEnd")]
        public DateTime? LockoutEnd { get; set; }

        /// <summary>
        /// Дата последней смены пароля
        /// </summary>
        [Column("LastPasswordChange")]
        public DateTime? LastPasswordChange { get; set; }

        /// <summary>
        /// Группы пользователя (сериализованный JSON)
        /// </summary>
        [Column("GroupsJson")]
        [MaxLength(2000)]
        public string GroupsJson { get; set; } = "[]";

        /// <summary>
        /// Дополнительные настройки пользователя (JSON)
        /// </summary>
        [Column("SettingsJson")]
        [MaxLength(4000)]
        public string SettingsJson { get; set; } = "{}";

        /// <summary>
        /// Дополнительные метаданные (JSON)
        /// </summary>
        [Column("MetadataJson")]
        [MaxLength(2000)]
        public string MetadataJson { get; set; } = "{}";

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

        #endregion

        #region Переопределения

        public override string ToString()
        {
            return $"{DisplayName} ({Username}) - {Role}";
        }

        public override bool Equals(object obj)
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
                MetadataJson = MetadataJson
            };
        }

        #endregion
    }
}
// WindowsLauncher.Core/Models/AuditLog.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ БЕЗ ДУБЛИРОВАНИЯ
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Модель для логирования действий пользователей
    /// </summary>
    [Table("AuditLogs")]
    public class AuditLog
    {
        /// <summary>
        /// Уникальный идентификатор записи
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID пользователя (может быть null для системных действий)
        /// </summary>
        [Column("UserId")]
        public int? UserId { get; set; }

        /// <summary>
        /// Имя пользователя
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("Username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Тип действия (Login, Logout, LaunchApp, AccessDenied и т.д.)
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("Action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Название приложения (для действий с приложениями)
        /// </summary>
        [MaxLength(200)]
        [Column("ApplicationName")]
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// Детали действия
        /// </summary>
        [MaxLength(2000)]
        [Column("Details")]
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Время выполнения действия
        /// </summary>
        [Column("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Успешно ли выполнено действие
        /// </summary>
        [Column("Success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Сообщение об ошибке (если действие неуспешно)
        /// </summary>
        [MaxLength(1000)]
        [Column("ErrorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Имя компьютера, с которого выполнено действие
        /// </summary>
        [MaxLength(100)]
        [Column("ComputerName")]
        public string ComputerName { get; set; } = Environment.MachineName;

        /// <summary>
        /// IP адрес пользователя - ЕДИНСТВЕННОЕ поле для IP
        /// </summary>
        [MaxLength(45)] // Поддержка IPv6
        [Column("IPAddress")]
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// User Agent браузера/приложения
        /// </summary>
        [MaxLength(500)]
        [Column("UserAgent")]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Дополнительные метаданные в формате JSON
        /// </summary>
        [MaxLength(2000)]
        [Column("MetadataJson")]
        public string MetadataJson { get; set; } = "{}";

        #region Свойства для совместимости (НЕ МАППЯТСЯ В БД)

        /// <summary>
        /// Алиас для IPAddress для совместимости со старым кодом
        /// НЕ маппится в базу данных!
        /// </summary>
        [NotMapped]
        public string IpAddress
        {
            get => IPAddress;
            set => IPAddress = value;
        }

        #endregion

        #region Навигационные свойства

        /// <summary>
        /// Связь с пользователем (может быть null)
        /// </summary>
        public virtual User? User { get; set; }

        #endregion

        #region Статические методы для создания логов

        /// <summary>
        /// Создать лог входа в систему
        /// </summary>
        public static AuditLog CreateLoginLog(string username, bool success, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = "Login",
                Details = $"User login attempt from {Environment.MachineName}",
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать лог выхода из системы
        /// </summary>
        public static AuditLog CreateLogoutLog(string username)
        {
            return new AuditLog
            {
                Username = username,
                Action = "Logout",
                Details = $"User logout from {Environment.MachineName}",
                Success = true,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать лог запуска приложения
        /// </summary>
        public static AuditLog CreateApplicationLaunchLog(string username, string applicationName, bool success, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = "LaunchApplication",
                ApplicationName = applicationName,
                Details = $"Application launch: {applicationName}",
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать лог отказа в доступе
        /// </summary>
        public static AuditLog CreateAccessDeniedLog(string username, string resource, string reason)
        {
            return new AuditLog
            {
                Username = username,
                Action = "AccessDenied",
                Details = $"Access denied to {resource}",
                Success = false,
                ErrorMessage = reason,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать лог системного события
        /// </summary>
        public static AuditLog CreateSystemEventLog(string username, string action, string details, bool success = true, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = action,
                Details = details,
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        #endregion

        #region Методы экземпляра

        /// <summary>
        /// Установить IP адрес из контекста
        /// </summary>
        public void SetIPAddress(string ipAddress)
        {
            IPAddress = ipAddress ?? string.Empty;
        }

        /// <summary>
        /// Добавить метаданные
        /// </summary>
        public void AddMetadata(string key, object value)
        {
            try
            {
                var metadata = string.IsNullOrEmpty(MetadataJson)
                    ? new Dictionary<string, object>()
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson) ?? new Dictionary<string, object>();

                metadata[key] = value;
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            }
            catch
            {
                // В случае ошибки сериализации, просто игнорируем
                MetadataJson = "{}";
            }
        }

        #endregion

        #region Переопределения

        /// <summary>
        /// Строковое представление лога
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "SUCCESS" : "FAILED";
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {status} - {Username}: {Action} - {Details}";
        }

        /// <summary>
        /// Проверка равенства
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is AuditLog other)
            {
                return Id == other.Id &&
                       Username == other.Username &&
                       Action == other.Action &&
                       Timestamp == other.Timestamp;
            }
            return false;
        }

        /// <summary>
        /// Хэш-код объекта
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Username, Action, Timestamp);
        }

        #endregion
    }
}
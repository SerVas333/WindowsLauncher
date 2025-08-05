using System;
using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models.Email
{
    /// <summary>
    /// Настройки SMTP сервера для отправки email
    /// </summary>
    public class SmtpSettings
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string Host { get; set; } = string.Empty;
        
        [Range(1, 65535)]
        public int Port { get; set; } = 587;
        
        [Required]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// Зашифрованный пароль (не храним в открытом виде)
        /// </summary>
        [Required]
        public string EncryptedPassword { get; set; } = string.Empty;
        
        public bool UseSSL { get; set; } = true;
        
        public bool UseStartTLS { get; set; } = true;
        
        [MaxLength(200)]
        public string? DefaultFromEmail { get; set; }
        
        [MaxLength(100)]
        public string? DefaultFromName { get; set; }
        
        public SmtpServerType ServerType { get; set; } = SmtpServerType.Primary;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? UpdatedAt { get; set; }
        
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// Последняя успешная отправка (для мониторинга)
        /// </summary>
        public DateTime? LastSuccessfulSend { get; set; }
        
        /// <summary>
        /// Счетчик ошибок подряд (для переключения на резервный)
        /// </summary>
        public int ConsecutiveErrors { get; set; } = 0;
        
        /// <summary>
        /// Отображаемое имя для UI
        /// </summary>
        public string DisplayName => $"{Name} ({Host}:{Port})";
    }
    
    /// <summary>
    /// Тип SMTP сервера
    /// </summary>
    public enum SmtpServerType
    {
        /// <summary>
        /// Основной SMTP сервер (обязательный)
        /// </summary>
        Primary,
        
        /// <summary>
        /// Резервный SMTP сервер (опциональный)
        /// </summary>
        Backup
    }
}
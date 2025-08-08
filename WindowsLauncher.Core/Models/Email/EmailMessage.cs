using System;
using System.Collections.Generic;

namespace WindowsLauncher.Core.Models.Email
{
    /// <summary>
    /// Представляет email сообщение для отправки
    /// </summary>
    public class EmailMessage
    {
        public string From { get; set; } = string.Empty;
        
        public string FromDisplayName { get; set; } = string.Empty;
        
        public List<string> To { get; set; } = new List<string>();
        
        public List<string> Cc { get; set; } = new List<string>();
        
        public List<string> Bcc { get; set; } = new List<string>();
        
        public string Subject { get; set; } = string.Empty;
        
        public string Body { get; set; } = string.Empty;
        
        public bool IsHtml { get; set; } = false;
        
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    }
    
    /// <summary>
    /// Вложение email сообщения
    /// </summary>
    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        
        public string FilePath { get; set; } = string.Empty;
        
        public byte[]? Content { get; set; }
        
        public string ContentType { get; set; } = "application/octet-stream";
        
        public long Size { get; set; }
        
        public bool IsInline { get; set; } = false;
        
        public string? ContentId { get; set; }
        
        /// <summary>
        /// Отформатированный размер файла для отображения в UI
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} байт";
                else if (Size < 1024 * 1024)
                    return $"{Size / 1024.0:F1} КБ";
                else
                    return $"{Size / (1024.0 * 1024.0):F1} МБ";
            }
        }
    }
    
    /// <summary>
    /// Приоритет email сообщения
    /// </summary>
    public enum EmailPriority
    {
        Low,
        Normal,
        High
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Core.Interfaces.Email
{
    /// <summary>
    /// Интерфейс для отправки email сообщений
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Отправить email сообщение
        /// Автоматически использует основной SMTP, при неудаче - резервный
        /// </summary>
        /// <param name="message">Сообщение для отправки</param>
        /// <returns>Результат отправки</returns>
        Task<EmailSendResult> SendEmailAsync(EmailMessage message);
        
        /// <summary>
        /// Отправить простое текстовое сообщение
        /// </summary>
        /// <param name="to">Получатель</param>
        /// <param name="subject">Тема</param>
        /// <param name="body">Текст сообщения</param>
        /// <param name="isHtml">HTML форматирование</param>
        /// <returns>Результат отправки</returns>
        Task<EmailSendResult> SendSimpleEmailAsync(string to, string subject, string body, bool isHtml = false);
        
        /// <summary>
        /// Отправить сообщение с вложением
        /// </summary>
        /// <param name="to">Получатель</param>
        /// <param name="subject">Тема</param>
        /// <param name="body">Текст сообщения</param>
        /// <param name="attachmentPath">Путь к файлу</param>
        /// <returns>Результат отправки</returns>
        Task<EmailSendResult> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath);
        
        /// <summary>
        /// Проверить подключение к SMTP серверу
        /// </summary>
        /// <param name="settings">Настройки SMTP</param>
        /// <returns>Результат проверки</returns>
        Task<EmailTestResult> TestSmtpConnectionAsync(SmtpSettings settings);
        
        /// <summary>
        /// Получить активные настройки SMTP (основной и резервный)
        /// </summary>
        /// <returns>Кортеж настроек (Primary, Backup)</returns>
        Task<(SmtpSettings? Primary, SmtpSettings? Backup)> GetActiveSmtpSettingsAsync();
    }
    
    /// <summary>
    /// Результат отправки email
    /// </summary>
    public class EmailSendResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public SmtpServerType? UsedServer { get; set; }
        public DateTime SentAt { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static EmailSendResult Success(SmtpServerType usedServer, TimeSpan duration)
        {
            return new EmailSendResult
            {
                IsSuccess = true,
                UsedServer = usedServer,
                SentAt = DateTime.Now,
                Duration = duration
            };
        }
        
        public static EmailSendResult Failure(string errorMessage)
        {
            return new EmailSendResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                SentAt = DateTime.Now
            };
        }
    }
    
    /// <summary>
    /// Результат тестирования SMTP подключения
    /// </summary>
    public class EmailTestResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ServerInfo { get; set; }
        
        public static EmailTestResult Success(TimeSpan duration, string? serverInfo = null)
        {
            return new EmailTestResult
            {
                IsSuccess = true,
                Duration = duration,
                ServerInfo = serverInfo
            };
        }
        
        public static EmailTestResult Failure(string errorMessage, TimeSpan duration)
        {
            return new EmailTestResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Duration = duration
            };
        }
    }
}
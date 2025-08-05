using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services.Email
{
    /// <summary>
    /// Сервис отправки email с использованием MailKit
    /// Поддерживает основной и резервный SMTP серверы
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly WindowsLauncher.Core.Interfaces.IEncryptionService _encryptionService;
        private readonly ISmtpSettingsRepository _smtpRepository;
        
        public EmailService(
            ILogger<EmailService> logger,
            WindowsLauncher.Core.Interfaces.IEncryptionService encryptionService,
            ISmtpSettingsRepository smtpRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _smtpRepository = smtpRepository ?? throw new ArgumentNullException(nameof(smtpRepository));
        }
        
        /// <summary>
        /// Отправить email сообщение с fallback на резервный сервер
        /// </summary>
        public async Task<EmailSendResult> SendEmailAsync(EmailMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            
            var startTime = DateTime.Now;
            
            try
            {
                // Получаем настройки SMTP
                var (primary, backup) = await GetActiveSmtpSettingsAsync();
                
                if (primary == null)
                {
                    _logger.LogError("No primary SMTP settings configured");
                    return EmailSendResult.Failure("Настройки SMTP не найдены");
                }
                
                // Пытаемся отправить через основной сервер
                var primaryResult = await TrySendEmailAsync(message, primary, SmtpServerType.Primary);
                if (primaryResult.IsSuccess)
                {
                    await ResetServerErrorCountAsync(primary);
                    return primaryResult;
                }
                
                // Увеличиваем счетчик ошибок основного сервера
                await IncrementServerErrorCountAsync(primary);
                
                // Если есть резервный сервер - пытаемся через него
                if (backup != null)
                {
                    _logger.LogWarning("Primary SMTP failed, trying backup server: {Error}", primaryResult.ErrorMessage);
                    
                    var backupResult = await TrySendEmailAsync(message, backup, SmtpServerType.Backup);
                    if (backupResult.IsSuccess)
                    {
                        await ResetServerErrorCountAsync(backup);
                        return backupResult;
                    }
                    
                    await IncrementServerErrorCountAsync(backup);
                    _logger.LogError("Both primary and backup SMTP servers failed");
                    
                    return EmailSendResult.Failure($"Основной сервер: {primaryResult.ErrorMessage}. Резервный сервер: {backupResult.ErrorMessage}");
                }
                
                return primaryResult;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Unexpected error sending email in {Duration}ms", duration.TotalMilliseconds);
                return EmailSendResult.Failure($"Неожиданная ошибка: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Попытка отправки через конкретный SMTP сервер используя MailKitSimplified
        /// </summary>
        private async Task<EmailSendResult> TrySendEmailAsync(EmailMessage message, SmtpSettings settings, SmtpServerType serverType)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // Настраиваем SMTP сервер используя стандартный MailKit
                var password = _encryptionService.Decrypt(settings.EncryptedPassword);
                
                using var client = new SmtpClient();
                
                // Подключаемся к SMTP серверу
                await client.ConnectAsync(settings.Host, settings.Port, GetSecureSocketOptions(settings));
                
                // Аутентификация
                await client.AuthenticateAsync(settings.Username, password);
                
                // Создаем MimeMessage
                var mimeMessage = new MimeMessage();
                
                // Настраиваем отправителя
                var fromEmail = !string.IsNullOrEmpty(message.From) ? message.From : settings.DefaultFromEmail ?? settings.Username;
                var fromName = !string.IsNullOrEmpty(message.FromDisplayName) ? message.FromDisplayName : settings.DefaultFromName;
                
                if (!string.IsNullOrEmpty(fromName))
                {
                    mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
                }
                else
                {
                    mimeMessage.From.Add(new MailboxAddress(fromEmail, fromEmail));
                }
                
                // Добавляем получателей
                foreach (var to in message.To)
                {
                    mimeMessage.To.Add(new MailboxAddress(to, to));
                }
                
                foreach (var cc in message.Cc)
                {
                    mimeMessage.Cc.Add(new MailboxAddress(cc, cc));
                }
                
                foreach (var bcc in message.Bcc)
                {
                    mimeMessage.Bcc.Add(new MailboxAddress(bcc, bcc));
                }
                
                // Устанавливаем тему
                mimeMessage.Subject = message.Subject;
                
                // Создаем тело сообщения
                var bodyBuilder = new BodyBuilder();
                
                if (message.IsHtml)
                {
                    bodyBuilder.HtmlBody = message.Body;
                }
                else
                {
                    bodyBuilder.TextBody = message.Body;
                }
                
                // Добавляем вложения
                foreach (var attachment in message.Attachments)
                {
                    if (attachment.Content != null && attachment.Content.Length > 0)
                    {
                        bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType ?? "application/octet-stream"));
                    }
                    else if (!string.IsNullOrEmpty(attachment.FilePath) && File.Exists(attachment.FilePath))
                    {
                        bodyBuilder.Attachments.Add(attachment.FilePath);
                    }
                }
                
                mimeMessage.Body = bodyBuilder.ToMessageBody();
                
                // Отправляем сообщение
                await client.SendAsync(mimeMessage);
                await client.DisconnectAsync(true);
                
                var duration = DateTime.Now - startTime;
                _logger.LogInformation("Email sent successfully via {ServerType} SMTP in {Duration}ms", 
                    serverType, duration.TotalMilliseconds);
                
                return EmailSendResult.Success(serverType, duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Failed to send email via {ServerType} SMTP {Host}:{Port} in {Duration}ms", 
                    serverType, settings.Host, settings.Port, duration.TotalMilliseconds);
                
                return EmailSendResult.Failure($"Ошибка {serverType} SMTP ({settings.Host}:{settings.Port}): {ex.Message}");
            }
        }
        
        
        /// <summary>
        /// Отправить простое текстовое сообщение
        /// </summary>
        public async Task<EmailSendResult> SendSimpleEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            var message = new EmailMessage
            {
                To = new List<string> { to },
                Subject = subject,
                Body = body,
                IsHtml = isHtml
            };
            
            return await SendEmailAsync(message);
        }
        
        /// <summary>
        /// Отправить сообщение с вложением
        /// </summary>
        public async Task<EmailSendResult> SendEmailWithAttachmentAsync(string to, string subject, string body, string attachmentPath)
        {
            if (!File.Exists(attachmentPath))
            {
                return EmailSendResult.Failure($"Файл вложения не найден: {attachmentPath}");
            }
            
            var fileInfo = new FileInfo(attachmentPath);
            var attachment = new EmailAttachment
            {
                FileName = fileInfo.Name,
                FilePath = attachmentPath,
                Size = fileInfo.Length,
                ContentType = GetContentType(fileInfo.Extension)
            };
            
            var message = new EmailMessage
            {
                To = new List<string> { to },
                Subject = subject,
                Body = body,
                Attachments = new List<EmailAttachment> { attachment }
            };
            
            return await SendEmailAsync(message);
        }
        
        /// <summary>
        /// Тестировать SMTP подключение используя MailKitSimplified
        /// </summary>
        public async Task<EmailTestResult> TestSmtpConnectionAsync(SmtpSettings settings)
        {
            var startTime = DateTime.Now;
            
            try
            {
                var password = _encryptionService.Decrypt(settings.EncryptedPassword);
                
                // Создаем SMTP клиент для тестирования
                using var client = new SmtpClient();
                
                // Подключаемся к SMTP серверу
                await client.ConnectAsync(settings.Host, settings.Port, GetSecureSocketOptions(settings));
                
                // Аутентификация
                await client.AuthenticateAsync(settings.Username, password);
                
                // Создаем тестовое сообщение
                var testMessage = new MimeMessage();
                var testEmail = settings.DefaultFromEmail ?? settings.Username;
                
                testMessage.From.Add(new MailboxAddress(testEmail, testEmail));
                testMessage.To.Add(new MailboxAddress(testEmail, testEmail));
                testMessage.Subject = "SMTP Connection Test";
                testMessage.Body = new TextPart("plain")
                {
                    Text = "This is a test message to verify SMTP connection."
                };
                
                // Отправляем тестовое сообщение
                await client.SendAsync(testMessage);
                await client.DisconnectAsync(true);
                
                var duration = DateTime.Now - startTime;
                var serverInfo = $"Connected to {settings.Host}:{settings.Port} (MailKit)";
                
                _logger.LogInformation("SMTP connection test successful for {Host}:{Port} in {Duration}ms", 
                    settings.Host, settings.Port, duration.TotalMilliseconds);
                
                return EmailTestResult.Success(duration, serverInfo);
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "SMTP connection test failed for {Host}:{Port} in {Duration}ms", 
                    settings.Host, settings.Port, duration.TotalMilliseconds);
                
                return EmailTestResult.Failure(ex.Message, duration);
            }
        }
        
        /// <summary>
        /// Получить активные настройки SMTP
        /// </summary>
        public async Task<(SmtpSettings? Primary, SmtpSettings? Backup)> GetActiveSmtpSettingsAsync()
        {
            var allSettings = await _smtpRepository.GetActiveSettingsAsync();
            
            var primary = allSettings.FirstOrDefault(s => s.ServerType == SmtpServerType.Primary);
            var backup = allSettings.FirstOrDefault(s => s.ServerType == SmtpServerType.Backup);
            
            return (primary, backup);
        }
        
        #region Helper Methods
        
        private SecureSocketOptions GetSecureSocketOptions(SmtpSettings settings)
        {
            if (settings.UseSSL)
            {
                return SecureSocketOptions.SslOnConnect;
            }
            else if (settings.UseStartTLS)
            {
                return SecureSocketOptions.StartTls;
            }
            else
            {
                return SecureSocketOptions.None;
            }
        }
        
        private string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }
        
        private async Task IncrementServerErrorCountAsync(SmtpSettings settings)
        {
            try
            {
                settings.ConsecutiveErrors++;
                await _smtpRepository.UpdateAsync(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment error count for SMTP server {Host}", settings.Host);
            }
        }
        
        private async Task ResetServerErrorCountAsync(SmtpSettings settings)
        {
            try
            {
                if (settings.ConsecutiveErrors > 0)
                {
                    settings.ConsecutiveErrors = 0;
                    settings.LastSuccessfulSend = DateTime.Now;
                    await _smtpRepository.UpdateAsync(settings);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset error count for SMTP server {Host}", settings.Host);
            }
        }
        
        #endregion
    }
}
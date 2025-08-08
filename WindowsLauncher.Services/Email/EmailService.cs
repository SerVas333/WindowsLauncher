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
                var password = _encryptionService.DecryptSecure(settings.EncryptedPassword);
                
                using var client = new SmtpClient();
                
                // Подключаемся к SMTP серверу
                await client.ConnectAsync(settings.Host, settings.Port, GetSecureSocketOptions(settings));
                
                // Аутентификация
                await client.AuthenticateAsync(settings.Username, password);
                
                // Создаем MimeMessage
                var mimeMessage = new MimeMessage();
                
                // Настраиваем отправителя - используем безопасный адрес, соответствующий SMTP аутентификации
                var fromEmail = DetermineValidFromAddress(message, settings);
                var fromName = !string.IsNullOrEmpty(message.FromDisplayName) ? message.FromDisplayName : settings.DefaultFromName;
                
                // Логируем информацию о отправителе для диагностики
                _logger.LogDebug("Email From address determined: {FromEmail} (Original: {OriginalFrom}, Username: {Username})", 
                    fromEmail, message.From, settings.Username);
                
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
        /// Преобразовать SmtpCapabilities в список строк с названиями поддерживаемых возможностей
        /// </summary>
        private List<string> GetCapabilityList(SmtpCapabilities capabilities)
        {
            var list = new List<string>();

            if (capabilities.HasFlag(SmtpCapabilities.Authentication))
                list.Add("Authentication");
            if (capabilities.HasFlag(SmtpCapabilities.Size))
                list.Add("Size");
            if (capabilities.HasFlag(SmtpCapabilities.Dsn))
                list.Add("Delivery Status Notifications");
            if (capabilities.HasFlag(SmtpCapabilities.StartTLS))
                list.Add("StartTLS");
            if (capabilities.HasFlag(SmtpCapabilities.UTF8))
                list.Add("UTF8");
            if (capabilities.HasFlag(SmtpCapabilities.BinaryMime))
                list.Add("Binary MIME");
            if (capabilities.HasFlag(SmtpCapabilities.EnhancedStatusCodes))
                list.Add("Enhanced Status Codes");
            // Добавьте другие флаги, если нужно

            return list;
        }

        /// <summary>
        /// Тестировать SMTP подключение с детальной диагностикой
        /// </summary>
        public async Task<EmailTestResult> TestSmtpConnectionAsync(SmtpSettings settings)
        {
            var startTime = DateTime.Now;

            try
            {
                var password = _encryptionService.DecryptSecure(settings.EncryptedPassword);

                // Определяем email адрес для тестирования
                var testEmail = DetermineTestEmailAddress(settings);

                _logger.LogInformation("=== SMTP CONNECTION TEST START ===");
                _logger.LogInformation("Host: {Host}:{Port}", settings.Host, settings.Port);
                _logger.LogInformation("SSL: {UseSSL}, StartTLS: {UseStartTLS}", settings.UseSSL, settings.UseStartTLS);
                _logger.LogInformation("Username: {Username}", settings.Username);
                _logger.LogInformation("Test Email: {TestEmail}", testEmail);
                _logger.LogInformation("Security Options: {SecurityOptions}", GetSecureSocketOptions(settings));

                using var client = new SmtpClient();

                // Для тестирования пропускаем проверку сертификата (НЕ используйте в продакшене)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogInformation("Connecting to SMTP server...");

                await client.ConnectAsync(settings.Host, settings.Port, GetSecureSocketOptions(settings));

                var capabilitiesList = GetCapabilityList(client.Capabilities);

                _logger.LogInformation("Connected successfully. Server capabilities: {Capabilities}",
                    string.Join(", ", capabilitiesList));

                _logger.LogInformation("Authenticating with username: {Username}", settings.Username);

                await client.AuthenticateAsync(settings.Username, password);

                _logger.LogInformation("Authentication successful");

                _logger.LogInformation("SMTP test completed - connection and authentication successful");

                await client.DisconnectAsync(true);

                var duration = DateTime.Now - startTime;

                // Предполагаю, BuildServerInfoString теперь принимает список строк capabilitiesList
                var serverInfo = BuildServerInfoString(settings, testEmail, capabilitiesList);

                _logger.LogInformation("SMTP connection test successful for {Host}:{Port} in {Duration}ms",
                    settings.Host, settings.Port, duration.TotalMilliseconds);
                _logger.LogInformation("=== SMTP CONNECTION TEST END (SUCCESS) ===");

                return EmailTestResult.Success(duration, serverInfo);
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                var errorMessage = FormatSmtpError(ex);

                _logger.LogError(ex, "SMTP connection test failed for {Host}:{Port} in {Duration}ms",
                    settings.Host, settings.Port, duration.TotalMilliseconds);
                _logger.LogError("=== SMTP CONNECTION TEST END (FAILED) ===");

                return EmailTestResult.Failure(errorMessage, duration);
            }
        }


        /// <summary>
        /// Определить email адрес для тестирования
        /// </summary>
        private string DetermineTestEmailAddress(SmtpSettings settings)
        {
            // Приоритет: DefaultFromEmail -> Username (если это email) -> Username@domain
            if (!string.IsNullOrEmpty(settings.DefaultFromEmail) && IsValidEmail(settings.DefaultFromEmail))
            {
                return settings.DefaultFromEmail;
            }
            
            if (!string.IsNullOrEmpty(settings.Username) && IsValidEmail(settings.Username))
            {
                return settings.Username;
            }
            
            // Если Username не email, пытаемся создать email из домена хоста
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Host))
            {
                var domain = settings.Host.StartsWith("smtp.") ? settings.Host.Substring(5) : 
                            settings.Host.StartsWith("mail.") ? settings.Host.Substring(5) : settings.Host;
                return $"{settings.Username}@{domain}";
            }
            
            return settings.Username ?? "test@example.com";
        }
        
        /// <summary>
        /// Определить валидный From адрес для реальной отправки email
        /// Использует только адреса, соответствующие SMTP аутентификации
        /// </summary>
        private string DetermineValidFromAddress(EmailMessage message, SmtpSettings settings)
        {
            // Логируем исходные данные для диагностики
            _logger.LogDebug("Determining valid From address - Message.From: {MessageFrom}, DefaultFromEmail: {DefaultFromEmail}, Username: {Username}",
                message.From, settings.DefaultFromEmail, settings.Username);
            
            // Используем ту же логику, что и для тестирования, но с учетом пожеланий пользователя
            // 1. Если пользователь указал From и он соответствует настройкам SMTP - используем его
            if (!string.IsNullOrEmpty(message.From) && IsValidEmail(message.From))
            {
                // Проверяем, может ли этот адрес использоваться с текущими SMTP настройками
                if (CanUseFromAddress(message.From, settings))
                {
                    _logger.LogDebug("Using user-specified From address: {FromEmail}", message.From);
                    return message.From;
                }
                else
                {
                    _logger.LogWarning("User-specified From address '{FromEmail}' cannot be used with SMTP settings, using safe alternative", message.From);
                }
            }
            
            // 2. Используем DefaultFromEmail если он настроен
            if (!string.IsNullOrEmpty(settings.DefaultFromEmail) && IsValidEmail(settings.DefaultFromEmail))
            {
                _logger.LogDebug("Using DefaultFromEmail: {DefaultFromEmail}", settings.DefaultFromEmail);
                return settings.DefaultFromEmail;
            }
            
            // 3. Если Username это email - используем его
            if (!string.IsNullOrEmpty(settings.Username) && IsValidEmail(settings.Username))
            {
                _logger.LogDebug("Using Username as email: {Username}", settings.Username);
                return settings.Username;
            }
            
            // 4. Создаем email из Username и домена
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Host))
            {
                var domain = settings.Host.StartsWith("smtp.") ? settings.Host.Substring(5) : 
                            settings.Host.StartsWith("mail.") ? settings.Host.Substring(5) : settings.Host;
                var constructedEmail = $"{settings.Username}@{domain}";
                _logger.LogDebug("Constructed email from username and host: {ConstructedEmail}", constructedEmail);
                return constructedEmail;
            }
            
            // 5. Fallback - используем Username как есть
            var fallbackEmail = settings.Username ?? "noreply@localhost";
            _logger.LogWarning("Using fallback email address: {FallbackEmail}", fallbackEmail);
            return fallbackEmail;
        }
        
        /// <summary>
        /// Проверить, может ли указанный From адрес использоваться с настройками SMTP
        /// </summary>
        private bool CanUseFromAddress(string fromAddress, SmtpSettings settings)
        {
            // Для большинства SMTP серверов From адрес должен:
            // 1. Совпадать с Username (если это email)
            // 2. Совпадать с DefaultFromEmail  
            // 3. Быть в том же домене, что и Username
            
            if (string.Equals(fromAddress, settings.Username, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            if (!string.IsNullOrEmpty(settings.DefaultFromEmail) && 
                string.Equals(fromAddress, settings.DefaultFromEmail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // Проверяем домен (упрощенная проверка)
            if (!string.IsNullOrEmpty(settings.Username) && IsValidEmail(settings.Username))
            {
                var usernameDomain = settings.Username.Split('@').LastOrDefault();
                var fromDomain = fromAddress.Split('@').LastOrDefault();
                
                if (!string.IsNullOrEmpty(usernameDomain) && !string.IsNullOrEmpty(fromDomain) &&
                    string.Equals(usernameDomain, fromDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Проверить валидность email адреса
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Построить информационную строку о сервере
        /// </summary>
        private string BuildServerInfoString(SmtpSettings settings, string testEmail, List<string> capabilities)
        {
            var securityInfo = settings.UseSSL ? "SSL" : settings.UseStartTLS ? "StartTLS" : "None";
            var capabilitiesInfo = capabilities.Count > 0 ? string.Join(", ", capabilities.Take(3)) : "Unknown";
            
            return $"{settings.Host}:{settings.Port} ({securityInfo}) | Test Email: {testEmail} | Caps: {capabilitiesInfo}";
        }
        
        /// <summary>
        /// Форматировать SMTP ошибку для пользователя
        /// </summary>
        private string FormatSmtpError(Exception ex)
        {
            return ex switch
            {
                MailKit.Net.Smtp.SmtpCommandException smtpEx => $"SMTP сервер ответил: {smtpEx.Message} (Код: {smtpEx.StatusCode})",
                MailKit.Security.AuthenticationException authEx => $"Ошибка аутентификации: {authEx.Message}. Проверьте логин и пароль.",
                System.Net.Sockets.SocketException sockEx => $"Ошибка подключения: {sockEx.Message}. Проверьте адрес сервера и порт.",
                System.TimeoutException => "Превышено время ожидания. Проверьте подключение к интернету и настройки брандмауэра.",
                _ => $"Ошибка подключения: {ex.Message}"
            };
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
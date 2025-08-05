using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Email;

namespace WindowsLauncher.Services.Email
{
    /// <summary>
    /// Сервис шифрования с использованием Windows DPAPI
    /// Обеспечивает безопасное хранение SMTP паролей
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private const string ENCRYPTION_PREFIX = "DPAPI:";
        
        public EncryptionService(ILogger<EncryptionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Зашифровать строку с использованием Windows DPAPI
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                _logger.LogWarning("Attempted to encrypt null or empty string");
                return string.Empty;
            }
            
            // Если уже зашифровано - возвращаем как есть
            if (IsEncrypted(plainText))
            {
                _logger.LogDebug("String is already encrypted, returning as-is");
                return plainText;
            }
            
            try
            {
                // Конвертируем в байты
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                
                // Шифруем с привязкой к текущему пользователю
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainTextBytes, 
                    null, // no additional entropy
                    DataProtectionScope.CurrentUser); // привязка к текущему пользователю
                
                // Конвертируем в Base64 с префиксом
                string encryptedText = ENCRYPTION_PREFIX + Convert.ToBase64String(encryptedBytes);
                
                _logger.LogDebug("Successfully encrypted string of length {Length}", plainText.Length);
                return encryptedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt string");
                throw new InvalidOperationException("Encryption failed", ex);
            }
        }
        
        /// <summary>
        /// Расшифровать строку с использованием Windows DPAPI
        /// </summary>
        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                _logger.LogWarning("Attempted to decrypt null or empty string");
                return string.Empty;
            }
            
            // Если не зашифровано - возвращаем как есть (обратная совместимость)
            if (!IsEncrypted(encryptedText))
            {
                _logger.LogWarning("String is not encrypted, returning as plain text (backward compatibility)");
                return encryptedText;
            }
            
            try
            {
                // Удаляем префикс
                string base64Data = encryptedText.Substring(ENCRYPTION_PREFIX.Length);
                
                // Конвертируем из Base64
                byte[] encryptedBytes = Convert.FromBase64String(base64Data);
                
                // Расшифровываем
                byte[] plainTextBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null, // no additional entropy  
                    DataProtectionScope.CurrentUser); // привязка к текущему пользователю
                
                // Конвертируем в строку
                string plainText = Encoding.UTF8.GetString(plainTextBytes);
                
                _logger.LogDebug("Successfully decrypted string");
                return plainText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt string");
                throw new InvalidOperationException("Decryption failed. Data may be corrupted or encrypted on different machine/user account.", ex);
            }
        }
        
        /// <summary>
        /// Проверить, является ли строка зашифрованной
        /// </summary>
        public bool IsEncrypted(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            return text.StartsWith(ENCRYPTION_PREFIX, StringComparison.Ordinal);
        }
    }
}
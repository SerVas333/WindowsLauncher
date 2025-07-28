using Microsoft.Extensions.Logging;
using System.Text;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services.Security
{
    /// <summary>
    /// Сервис обфускации для защиты паролей БД от случайного просмотра
    /// Использует простое Base64 кодирование - переносимо между машинами
    /// ВНИМАНИЕ: Это НЕ криптографическая защита, только обфускация!
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private const string EncryptionPrefix = "OBF:"; // Префикс для обфусцированных данных
        
        public EncryptionService(ILogger<EncryptionService> logger)
        {
            _logger = logger;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                // Проверяем, не обфусцирована ли уже строка
                if (IsEncrypted(plainText))
                {
                    _logger.LogDebug("String is already obfuscated");
                    return plainText;
                }

                // Простая обфускация через Base64 с солью
                var saltedText = $"{plainText}:{Environment.MachineName}:KDV";
                var plainBytes = Encoding.UTF8.GetBytes(saltedText);
                var base64Text = Convert.ToBase64String(plainBytes);
                
                var result = EncryptionPrefix + base64Text;
                
                _logger.LogDebug("Successfully obfuscated database password");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obfuscate password");
                // В случае ошибки возвращаем исходный текст
                return plainText;
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                // Если строка не обфусцирована, возвращаем как есть
                if (!IsEncrypted(cipherText))
                {
                    _logger.LogDebug("String is not obfuscated, returning as-is");
                    return cipherText;
                }

                // Убираем префикс и декодируем из Base64
                var base64Data = cipherText.Substring(EncryptionPrefix.Length);
                var decodedBytes = Convert.FromBase64String(base64Data);
                var saltedText = Encoding.UTF8.GetString(decodedBytes);
                
                // Извлекаем исходный пароль (до первого двоеточия)
                var colonIndex = saltedText.IndexOf(':');
                var result = colonIndex > 0 ? saltedText.Substring(0, colonIndex) : saltedText;
                
                _logger.LogDebug("Successfully deobfuscated database password");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deobfuscate password");
                
                // При ошибке возвращаем как есть (может быть незашифрованный пароль)
                return cipherText.StartsWith(EncryptionPrefix) 
                    ? string.Empty  // Поврежденные обфусцированные данные
                    : cipherText;   // Возможно незашифрованный пароль
            }
        }

        public bool IsEncrypted(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.StartsWith(EncryptionPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Тестирование функциональности обфускации
        /// </summary>
        public bool TestEncryption()
        {
            try
            {
                const string testData = "test_password_123";
                var obfuscated = Encrypt(testData);
                var deobfuscated = Decrypt(obfuscated);
                
                var success = testData.Equals(deobfuscated, StringComparison.Ordinal);
                _logger.LogInformation("Password obfuscation test result: {Success}", success);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password obfuscation test failed");
                return false;
            }
        }
    }
}
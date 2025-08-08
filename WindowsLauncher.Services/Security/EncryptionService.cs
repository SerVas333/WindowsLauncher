using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services.Security
{
    /// <summary>
    /// Унифицированный сервис шифрования для приложения
    /// - OBF: префикс для обфускации паролей БД (Base64 + соль)
    /// - AES: префикс для криптографического шифрования SMTP паролей (AES + IV)
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private const string EncryptionPrefix = "OBF:"; // Префикс для обфусцированных данных
        private const string SecureEncryptionPrefix = "AES:"; // Префикс для AES шифрования
        
        // Безопасная генерация AES ключа на основе характеристик системы
        private readonly byte[] _aesKey;
        
        public EncryptionService(ILogger<EncryptionService> logger)
        {
            _logger = logger;
            _aesKey = GenerateSecureAesKey();
            _logger.LogDebug("AES encryption key generated based on system characteristics");
        }

        /// <summary>
        /// Генерирует безопасный AES ключ на основе характеристик системы
        /// Использует машинное имя, версию приложения и константы для создания стабильного ключа
        /// </summary>
        private byte[] GenerateSecureAesKey()
        {
            try
            {
                var keyComponents = new List<string>
                {
                    "WindowsLauncher2025",           // Базовая константа приложения
                    Environment.MachineName,         // Имя машины
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0", // Версия
                    "KDV-Corporate-AES-Key"          // Корпоративная соль
                };

                // Объединяем компоненты с разделителем
                var keyString = string.Join(":", keyComponents);
                
                // Используем SHA256 для получения стабильного 32-байтового ключа
                using var sha256 = SHA256.Create();
                var keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
                
                _logger.LogDebug($"Generated AES key from machine: {Environment.MachineName}");
                return keyBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate secure AES key, falling back to default");
                // Fallback к базовому ключу в случае ошибки
                return SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("WindowsLauncher2025-Fallback-Key"));
            }
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

        #region Secure AES Encryption для SMTP паролей

        /// <summary>
        /// Зашифровать строку с использованием AES (криптографически безопасно)
        /// </summary>
        public string EncryptSecure(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                // Проверяем, не зашифрована ли уже строка
                if (IsSecurelyEncrypted(plainText))
                {
                    _logger.LogDebug("String is already AES encrypted");
                    return plainText;
                }

                using var aes = Aes.Create();
                aes.Key = _aesKey;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                // Комбинируем IV + зашифрованные данные
                var result = new byte[aes.IV.Length + encryptedBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                var base64Result = Convert.ToBase64String(result);
                var finalResult = SecureEncryptionPrefix + base64Result;
                
                _logger.LogDebug("Successfully encrypted string with AES");
                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt with AES");
                return plainText;
            }
        }

        /// <summary>
        /// Расшифровать строку, зашифрованную с помощью AES
        /// </summary>
        public string DecryptSecure(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                // Если строка не зашифрована AES, возвращаем как есть
                if (!IsSecurelyEncrypted(cipherText))
                {
                    _logger.LogDebug("String is not AES encrypted, returning as-is");
                    return cipherText;
                }

                // Убираем префикс и декодируем из Base64
                var base64Data = cipherText.Substring(SecureEncryptionPrefix.Length);
                var fullCipherBytes = Convert.FromBase64String(base64Data);

                using var aes = Aes.Create();
                aes.Key = _aesKey;

                // Извлекаем IV (первые 16 байт)
                var iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(fullCipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;

                // Извлекаем зашифрованные данные
                var cipherBytes = new byte[fullCipherBytes.Length - iv.Length];
                Buffer.BlockCopy(fullCipherBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                var result = Encoding.UTF8.GetString(decryptedBytes);
                _logger.LogDebug("Successfully decrypted AES encrypted string");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt AES encrypted string");
                
                // При ошибке возвращаем пустую строку для поврежденных AES данных
                return cipherText.StartsWith(SecureEncryptionPrefix) 
                    ? string.Empty  
                    : cipherText;
            }
        }

        /// <summary>
        /// Проверить, зашифрована ли строка криптографически (AES)
        /// </summary>
        public bool IsSecurelyEncrypted(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.StartsWith(SecureEncryptionPrefix, StringComparison.Ordinal);
        }

        #endregion
    }
}
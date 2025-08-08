// ===== PasswordEncryption.cs - Утилита для шифрования паролей SMTP =====
// Простая консольная утилита для генерации зашифрованных паролей
// Использование: dotnet run "your_password_here"

using System;
using System.Security.Cryptography;
using System.Text;

namespace WindowsLauncher.Tools
{
    /// <summary>
    /// Консольная утилита для шифрования SMTP паролей
    /// </summary>
    public class PasswordEncryption
    {
        // Тот же ключ, что используется в EncryptionService
        private static readonly string EncryptionKey = "WindowsLauncher2025-SecretKey-32Chars!"; 
        
        public static void Main(string[] args)
        {
            Console.WriteLine("===== Windows Launcher Password Encryption Tool =====");
            Console.WriteLine();
            
            if (args.Length == 0)
            {
                Console.WriteLine("Использование: dotnet run \"your_password_here\"");
                Console.WriteLine("Пример: dotnet run \"abcd-efgh-ijkl-mnop\"");
                Console.WriteLine();
                Console.WriteLine("Для интерактивного режима просто запустите без аргументов:");
                
                Console.Write("Введите пароль для шифрования: ");
                var password = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(password))
                {
                    Console.WriteLine("Пароль не может быть пустым!");
                    return;
                }
                
                EncryptAndDisplay(password);
            }
            else
            {
                var password = args[0];
                EncryptAndDisplay(password);
            }
        }
        
        private static void EncryptAndDisplay(string password)
        {
            try
            {
                var encryptedPassword = Encrypt(password);
                
                Console.WriteLine();
                Console.WriteLine("Результат шифрования:");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"Исходный пароль: {password}");
                Console.WriteLine($"Зашифрованный:   {encryptedPassword}");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine();
                Console.WriteLine("Используйте зашифрованную версию в базе данных:");
                Console.WriteLine($"ENCRYPTED_PASSWORD = '{encryptedPassword}'");
                Console.WriteLine();
                Console.WriteLine("⚠️  ВАЖНО: Никогда не коммитьте незашифрованные пароли в репозиторий!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка шифрования: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Шифрование пароля (аналог EncryptionService.Encrypt)
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                // Комбинируем IV + зашифрованные данные
                var result = new byte[aes.IV.Length + encryptedBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                return Convert.ToBase64String(result);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Дешифрование пароля для тестирования (аналог EncryptionService.Decrypt)
        /// </summary>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var fullCipherBytes = Convert.FromBase64String(encryptedText);

                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);

                // Извлекаем IV (первые 16 байт)
                var iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(fullCipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;

                // Извлекаем зашифрованные данные
                var cipherBytes = new byte[fullCipherBytes.Length - iv.Length];
                Buffer.BlockCopy(fullCipherBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
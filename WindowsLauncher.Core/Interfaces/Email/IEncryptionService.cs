namespace WindowsLauncher.Core.Interfaces.Email
{
    /// <summary>
    /// Интерфейс для шифрования/дешифрования данных
    /// Используется для безопасного хранения SMTP паролей
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Зашифровать строку с использованием Windows DPAPI
        /// Привязка к текущему пользователю и машине
        /// </summary>
        /// <param name="plainText">Открытый текст для шифрования</param>
        /// <returns>Зашифрованная строка в Base64</returns>
        string Encrypt(string plainText);
        
        /// <summary>
        /// Расшифровать строку с использованием Windows DPAPI
        /// </summary>
        /// <param name="encryptedText">Зашифрованная строка в Base64</param>
        /// <returns>Расшифрованный открытый текст</returns>
        string Decrypt(string encryptedText);
        
        /// <summary>
        /// Проверить, является ли строка зашифрованной
        /// </summary>
        /// <param name="text">Проверяемая строка</param>
        /// <returns>true если строка зашифрована</returns>
        bool IsEncrypted(string text);
    }
}
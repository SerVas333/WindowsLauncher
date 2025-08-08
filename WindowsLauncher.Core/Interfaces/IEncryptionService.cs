namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для шифрования/дешифрования чувствительных данных
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Зашифровать строку
        /// </summary>
        /// <param name="plainText">Исходный текст</param>
        /// <returns>Зашифрованная строка в Base64</returns>
        string Encrypt(string plainText);

        /// <summary>
        /// Расшифровать строку
        /// </summary>
        /// <param name="cipherText">Зашифрованная строка в Base64</param>
        /// <returns>Расшифрованный текст</returns>
        string Decrypt(string cipherText);

        /// <summary>
        /// Проверить, является ли строка зашифрованной
        /// </summary>
        /// <param name="value">Строка для проверки</param>
        /// <returns>True если строка зашифрована</returns>
        bool IsEncrypted(string value);

        /// <summary>
        /// Зашифровать строку с использованием AES (криптографически безопасно)
        /// </summary>
        /// <param name="plainText">Исходный текст</param>
        /// <returns>Зашифрованная строка с префиксом AES:</returns>
        string EncryptSecure(string plainText);

        /// <summary>
        /// Расшифровать строку, зашифрованную с помощью AES
        /// </summary>
        /// <param name="cipherText">Зашифрованная строка с префиксом AES:</param>
        /// <returns>Расшифрованный текст</returns>
        string DecryptSecure(string cipherText);

        /// <summary>
        /// Проверить, зашифрована ли строка криптографически (AES)
        /// </summary>
        /// <param name="value">Строка для проверки</param>
        /// <returns>True если строка зашифрована AES</returns>
        bool IsSecurelyEncrypted(string value);
    }
}
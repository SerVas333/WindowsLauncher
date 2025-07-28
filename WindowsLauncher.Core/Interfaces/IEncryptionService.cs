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
    }
}
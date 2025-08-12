namespace WindowsLauncher.Core.Enums
{
    /// <summary>
    /// Метод установки APK приложения в WSA
    /// </summary>
    public enum InstallationMethod
    {
        /// <summary>
        /// Неизвестный метод установки
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Стандартная установка APK файла через ADB
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Split APK установка (App Bundle)
        /// </summary>
        Split = 2,

        /// <summary>
        /// XAPK установка (расширенный формат APK)
        /// </summary>
        XAPK = 3,

        /// <summary>
        /// Принудительная переустановка существующего приложения
        /// </summary>
        ForceReinstall = 4,

        /// <summary>
        /// Sideload через WSA Developer Mode
        /// </summary>
        Sideload = 5
    }
}
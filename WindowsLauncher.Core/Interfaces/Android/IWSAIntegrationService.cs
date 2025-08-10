using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Сервис для низкоуровневой интеграции с Windows Subsystem for Android (WSA)
    /// </summary>
    public interface IWSAIntegrationService
    {
        /// <summary>
        /// Проверить доступность Windows Subsystem for Android в системе
        /// </summary>
        /// <returns>True, если WSA установлен и доступен</returns>
        Task<bool> IsWSAAvailableAsync();

        /// <summary>
        /// Проверить запущен ли WSA
        /// </summary>
        /// <returns>True, если WSA активно работает</returns>
        Task<bool> IsWSARunningAsync();

        /// <summary>
        /// Запустить WSA если он не запущен
        /// </summary>
        /// <returns>True, если WSA был успешно запущен</returns>
        Task<bool> StartWSAAsync();

        /// <summary>
        /// Остановить WSA
        /// </summary>
        /// <returns>True, если WSA был успешно остановлен</returns>
        Task<bool> StopWSAAsync();

        /// <summary>
        /// Проверить доступность ADB (Android Debug Bridge)
        /// </summary>
        /// <returns>True, если ADB доступен для выполнения команд</returns>
        Task<bool> IsAdbAvailableAsync();

        /// <summary>
        /// Подключиться к WSA через ADB
        /// </summary>
        /// <returns>True, если подключение установлено успешно</returns>
        Task<bool> ConnectToWSAAsync();

        /// <summary>
        /// Проверить корректность APK файла
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>True, если APK файл корректен и может быть установлен</returns>
        Task<bool> ValidateApkFileAsync(string apkPath);

        /// <summary>
        /// Извлечь метаданные из APK файла
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Метаданные APK или null, если извлечение не удалось</returns>
        Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath);

        /// <summary>
        /// Установить APK приложение в WSA
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Результат установки APK</returns>
        Task<ApkInstallResult> InstallApkAsync(string apkPath);

        /// <summary>
        /// Удалить Android приложение из WSA
        /// </summary>
        /// <param name="packageName">Package name приложения для удаления</param>
        /// <returns>True, если приложение было удалено успешно</returns>
        Task<bool> UninstallAppAsync(string packageName);

        /// <summary>
        /// Запустить Android приложение
        /// </summary>
        /// <param name="packageName">Package name приложения для запуска</param>
        /// <returns>Результат запуска приложения</returns>
        Task<AppLaunchResult> LaunchAppAsync(string packageName);

        /// <summary>
        /// Остановить работающее Android приложение
        /// </summary>
        /// <param name="packageName">Package name приложения для остановки</param>
        /// <returns>True, если приложение было остановлено</returns>
        Task<bool> StopAppAsync(string packageName);

        /// <summary>
        /// Получить список установленных Android приложений
        /// </summary>
        /// <param name="includeSystemApps">Включить системные приложения в список</param>
        /// <returns>Список установленных приложений</returns>
        Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync(bool includeSystemApps = false);

        /// <summary>
        /// Получить информацию о конкретном установленном приложении
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>Информация о приложении или null, если не найдено</returns>
        Task<InstalledAndroidApp?> GetAppInfoAsync(string packageName);

        /// <summary>
        /// Проверить, установлено ли приложение с указанным package name
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>True, если приложение установлено</returns>
        Task<bool> IsAppInstalledAsync(string packageName);

        /// <summary>
        /// Получить логи Android приложения
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <param name="maxLines">Максимальное количество строк логов</param>
        /// <returns>Логи приложения</returns>
        Task<string> GetAppLogsAsync(string packageName, int maxLines = 100);

        /// <summary>
        /// Очистить данные Android приложения
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>True, если данные были очищены успешно</returns>
        Task<bool> ClearAppDataAsync(string packageName);

        /// <summary>
        /// Получить версию Android в WSA
        /// </summary>
        /// <returns>Версия Android или null, если не удалось определить</returns>
        Task<string?> GetAndroidVersionAsync();

        /// <summary>
        /// Получить информацию о состоянии WSA
        /// </summary>
        /// <returns>Информация о версии WSA, статусе, доступной памяти и т.д.</returns>
        Task<Dictionary<string, object>> GetWSAStatusAsync();
    }
}
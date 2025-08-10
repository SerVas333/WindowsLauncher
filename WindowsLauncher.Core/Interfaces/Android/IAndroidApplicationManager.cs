using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Высокоуровневый сервис для управления Android приложениями в WindowsLauncher
    /// </summary>
    public interface IAndroidApplicationManager
    {
        /// <summary>
        /// Проверить доступность Android платформы (WSA + ADB)
        /// </summary>
        /// <returns>True, если Android приложения могут быть запущены</returns>
        Task<bool> IsAndroidSupportAvailableAsync();

        /// <summary>
        /// Инициализировать Android окружение (запустить WSA, подключить ADB)
        /// </summary>
        /// <returns>True, если инициализация прошла успешно</returns>
        Task<bool> InitializeAndroidEnvironmentAsync();

        /// <summary>
        /// Проверить корректность APK файла для установки
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>True, если APK может быть установлен</returns>
        Task<bool> ValidateApkAsync(string apkPath);

        /// <summary>
        /// Извлечь метаданные из APK файла для отображения в UI
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Метаданные APK или null при ошибке</returns>
        Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath);

        /// <summary>
        /// Установить APK приложение с предварительной проверкой
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Результат установки с детальной информацией</returns>
        Task<ApkInstallResult> InstallApkAsync(string apkPath);

        /// <summary>
        /// Обновить существующее Android приложение
        /// </summary>
        /// <param name="packageName">Package name приложения для обновления</param>
        /// <param name="newApkPath">Путь к новой версии APK</param>
        /// <returns>Результат обновления приложения</returns>
        Task<ApkInstallResult> UpdateAppAsync(string packageName, string newApkPath);

        /// <summary>
        /// Запустить Android приложение с проверкой состояния
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>Результат запуска приложения</returns>
        Task<AppLaunchResult> LaunchAndroidAppAsync(string packageName);

        /// <summary>
        /// Остановить Android приложение безопасно
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>True, если приложение остановлено</returns>
        Task<bool> StopAndroidAppAsync(string packageName);

        /// <summary>
        /// Удалить Android приложение с очисткой данных
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>True, если приложение удалено</returns>
        Task<bool> UninstallAndroidAppAsync(string packageName);

        /// <summary>
        /// Получить список установленных Android приложений (только пользовательские)
        /// </summary>
        /// <returns>Список пользовательских Android приложений</returns>
        Task<IEnumerable<InstalledAndroidApp>> GetInstalledAndroidAppsAsync();

        /// <summary>
        /// Проверить доступность обновлений для установленных приложений
        /// </summary>
        /// <param name="apkDirectory">Директория с APK файлами для проверки обновлений</param>
        /// <returns>Список приложений с доступными обновлениями</returns>
        Task<IEnumerable<(InstalledAndroidApp app, ApkMetadata newVersion)>> CheckForUpdatesAsync(string apkDirectory);

        /// <summary>
        /// Получить подробную информацию об Android приложении
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>Детальная информация или null если не найдено</returns>
        Task<InstalledAndroidApp?> GetAndroidAppDetailsAsync(string packageName);

        /// <summary>
        /// Очистить кэш и временные данные Android приложения
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <returns>True, если очистка прошла успешно</returns>
        Task<bool> ClearAndroidAppCacheAsync(string packageName);

        /// <summary>
        /// Получить логи работы Android приложения
        /// </summary>
        /// <param name="packageName">Package name приложения</param>
        /// <param name="maxLines">Максимальное количество строк логов</param>
        /// <returns>Текст логов приложения</returns>
        Task<string> GetAndroidAppLogsAsync(string packageName, int maxLines = 100);

        /// <summary>
        /// Проверить совместимость APK с текущей версией Android в WSA
        /// </summary>
        /// <param name="apkMetadata">Метаданные APK для проверки</param>
        /// <returns>True, если APK совместим с WSA</returns>
        Task<bool> IsApkCompatibleWithWSAAsync(ApkMetadata apkMetadata);

        /// <summary>
        /// Получить статистику использования Android функций
        /// </summary>
        /// <returns>Словарь со статистикой (количество приложений, использование памяти, etc.)</returns>
        Task<Dictionary<string, object>> GetAndroidUsageStatsAsync();

        /// <summary>
        /// Выполнить диагностику Android окружения
        /// </summary>
        /// <returns>Отчет о состоянии WSA, ADB, установленных приложений</returns>
        Task<string> RunAndroidDiagnosticsAsync();
    }
}
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Сервис для управления установленными Android приложениями
    /// Отвечает за: получение списка приложений, запуск, остановку, удаление
    /// </summary>
    public interface IInstalledAppsService
    {
        /// <summary>
        /// Получить список установленных Android приложений
        /// </summary>
        /// <param name="includeSystemApps">Включить системные приложения в список</param>
        /// <param name="useCache">Использовать кэшированные данные если доступны</param>
        /// <returns>Список установленных приложений</returns>
        Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync(bool includeSystemApps = false, bool useCache = true);

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
        /// Удалить Android приложение из WSA
        /// </summary>
        /// <param name="packageName">Package name приложения для удаления</param>
        /// <returns>True, если приложение было удалено успешно</returns>
        Task<bool> UninstallAppAsync(string packageName);

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
        /// Получить статистику использования Android приложений
        /// </summary>
        /// <returns>Словарь со статистикой использования приложений</returns>
        Task<Dictionary<string, object>> GetAppsUsageStatsAsync();

        /// <summary>
        /// Обновить кэш установленных приложений
        /// </summary>
        /// <returns>True, если кэш был успешно обновлен</returns>
        Task<bool> RefreshAppsCache();

        /// <summary>
        /// Событие изменения списка установленных приложений
        /// </summary>
        event EventHandler<InstalledAppsChangedEventArgs>? InstalledAppsChanged;
    }

    /// <summary>
    /// Аргументы события изменения списка установленных приложений
    /// </summary>
    public class InstalledAppsChangedEventArgs : EventArgs
    {
        public ChangeType ChangeType { get; set; }
        public string PackageName { get; set; } = "";
        public InstalledAndroidApp? AppInfo { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Тип изменения в списке установленных приложений
    /// </summary>
    public enum ChangeType
    {
        AppInstalled,
        AppUninstalled,
        AppUpdated,
        CacheRefreshed
    }
}
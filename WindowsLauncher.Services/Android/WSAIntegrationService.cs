using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Модернизированный WSA Integration Service, использующий композицию специализированных сервисов
    /// Полная замена оригинального WSAIntegrationService с улучшенной архитектурой
    /// </summary>
    public class WSAIntegrationService : IWSAIntegrationService, IDisposable
    {
        private readonly IWSAConnectionService _connectionService;
        private readonly IApkManagementService _apkService;
        private readonly IInstalledAppsService _appsService;
        private readonly ILogger<WSAIntegrationService> _logger;

        public WSAIntegrationService(
            IWSAConnectionService connectionService,
            IApkManagementService apkService,
            IInstalledAppsService appsService,
            ILogger<WSAIntegrationService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _apkService = apkService ?? throw new ArgumentNullException(nameof(apkService));
            _appsService = appsService ?? throw new ArgumentNullException(nameof(appsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("WSAIntegrationService initialized with specialized services architecture");
        }

        #region WSA Connection Management

        public async Task<bool> IsWSAAvailableAsync()
        {
            return await _connectionService.IsWSAAvailableAsync();
        }

        public async Task<bool> IsWSARunningAsync()
        {
            return await _connectionService.IsWSARunningAsync();
        }

        public async Task<bool> StartWSAAsync()
        {
            return await _connectionService.StartWSAAsync();
        }

        public async Task<bool> StopWSAAsync()
        {
            return await _connectionService.StopWSAAsync();
        }

        public async Task<bool> IsAdbAvailableAsync()
        {
            return await _connectionService.IsAdbAvailableAsync();
        }

        public async Task<bool> ConnectToWSAAsync()
        {
            return await _connectionService.ConnectToWSAAsync();
        }

        public async Task<string?> GetAndroidVersionAsync()
        {
            return await _connectionService.GetAndroidVersionAsync();
        }

        #endregion

        #region APK Management

        public async Task<bool> ValidateApkFileAsync(string apkPath)
        {
            return await _apkService.ValidateApkFileAsync(apkPath);
        }

        public async Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath)
        {
            return await _apkService.ExtractApkMetadataAsync(apkPath);
        }

        public async Task<ApkInstallResult> InstallApkAsync(string apkPath)
        {
            _logger.LogInformation("Installing APK through specialized service: {ApkPath}", apkPath);
            
            // Используем новый сервис с progress reporting
            return await _apkService.InstallApkAsync(apkPath, progress: null, CancellationToken.None);
        }

        #endregion

        #region Installed Apps Management

        public async Task<bool> UninstallAppAsync(string packageName)
        {
            return await _appsService.UninstallAppAsync(packageName);
        }

        public async Task<AppLaunchResult> LaunchAppAsync(string packageName)
        {
            return await _appsService.LaunchAppAsync(packageName);
        }

        public async Task<bool> StopAppAsync(string packageName)
        {
            return await _appsService.StopAppAsync(packageName);
        }

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync(bool includeSystemApps = false)
        {
            return await _appsService.GetInstalledAppsAsync(includeSystemApps);
        }

        public async Task<InstalledAndroidApp?> GetAppInfoAsync(string packageName)
        {
            return await _appsService.GetAppInfoAsync(packageName);
        }

        public async Task<bool> IsAppInstalledAsync(string packageName)
        {
            return await _appsService.IsAppInstalledAsync(packageName);
        }

        public async Task<string> GetAppLogsAsync(string packageName, int maxLines = 100)
        {
            return await _appsService.GetAppLogsAsync(packageName, maxLines);
        }

        public async Task<bool> ClearAppDataAsync(string packageName)
        {
            return await _appsService.ClearAppDataAsync(packageName);
        }

        #endregion

        #region Status and Diagnostics

        public async Task<Dictionary<string, object>> GetWSAStatusAsync()
        {
            try
            {
                var status = new Dictionary<string, object>();

                // Получаем статус подключения
                var connectionStatus = await _connectionService.GetConnectionStatusAsync();
                foreach (var kvp in connectionStatus)
                {
                    status[kvp.Key] = kvp.Value;
                }

                // Добавляем статистику приложений
                var appsStats = await _appsService.GetAppsUsageStatsAsync();
                foreach (var kvp in appsStats)
                {
                    status[$"Apps_{kvp.Key}"] = kvp.Value;
                }

                // Информация о архитектуре
                status["ServiceArchitecture"] = "V2 - Specialized Services";
                status["LastStatusUpdate"] = DateTime.Now;

                _logger.LogDebug("WSA status collected from {ConnectionKeys} connection keys and {AppsKeys} apps keys", 
                    connectionStatus.Count, appsStats.Count);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting WSA status");
                return new Dictionary<string, object> { ["Error"] = ex.Message };
            }
        }

        #endregion

        #region Enhanced Methods (New Architecture Benefits)

        /// <summary>
        /// Установить APK с прогрессом и возможностью отмены
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <param name="progress">Callback для отслеживания прогресса</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат установки</returns>
        public async Task<ApkInstallResult> InstallApkWithProgressAsync(string apkPath, IProgress<ApkInstallProgress>? progress, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Installing APK with progress tracking: {ApkPath}", apkPath);
            return await _apkService.InstallApkAsync(apkPath, progress, cancellationToken);
        }

        /// <summary>
        /// Проверить совместимость APK с текущей версией WSA
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>True если совместим</returns>
        public async Task<bool> IsApkCompatibleAsync(string apkPath)
        {
            var metadata = await _apkService.ExtractApkMetadataAsync(apkPath);
            if (metadata == null)
            {
                return false;
            }

            return await _apkService.IsApkCompatibleWithWSAAsync(metadata);
        }

        /// <summary>
        /// Получить подробную информацию о APK файле
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Информация о файле или null</returns>
        public async Task<ApkFileInfo?> GetApkFileInfoAsync(string apkPath)
        {
            return await _apkService.GetApkFileInfoAsync(apkPath);
        }

        /// <summary>
        /// Обновить кэш установленных приложений принудительно
        /// </summary>
        /// <returns>True если обновление прошло успешно</returns>
        public async Task<bool> RefreshInstalledAppsAsync()
        {
            _logger.LogInformation("Refreshing installed apps cache");
            return await _appsService.RefreshAppsCache();
        }

        /// <summary>
        /// Получить расширенную статистику использования Android функций
        /// </summary>
        /// <returns>Словарь со статистикой</returns>
        public async Task<Dictionary<string, object>> GetDetailedUsageStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                // Статистика подключения
                var connectionStatus = await _connectionService.GetConnectionStatusAsync();
                stats["ConnectionStats"] = connectionStatus;

                // Статистика приложений
                var appsStats = await _appsService.GetAppsUsageStatsAsync();
                stats["AppsStats"] = appsStats;

                // Общие метрики
                stats["TotalQueries"] = "Tracked by individual services";
                stats["ServiceUptime"] = DateTime.Now;
                stats["Architecture"] = "Microservices-based V2";

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting detailed usage stats");
                stats["Error"] = ex.Message;
                return stats;
            }
        }

        #endregion

        #region Event Subscriptions

        /// <summary>
        /// Подписаться на события изменения статуса подключения
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void SubscribeToConnectionEvents(EventHandler<WSAConnectionStatusEventArgs> handler)
        {
            _connectionService.ConnectionStatusChanged += handler;
        }

        /// <summary>
        /// Отписаться от событий изменения статуса подключения
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void UnsubscribeFromConnectionEvents(EventHandler<WSAConnectionStatusEventArgs> handler)
        {
            _connectionService.ConnectionStatusChanged -= handler;
        }

        /// <summary>
        /// Подписаться на события изменения списка установленных приложений
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void SubscribeToAppsEvents(EventHandler<InstalledAppsChangedEventArgs> handler)
        {
            _appsService.InstalledAppsChanged += handler;
        }

        /// <summary>
        /// Отписаться от событий изменения списка установленных приложений
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void UnsubscribeFromAppsEvents(EventHandler<InstalledAppsChangedEventArgs> handler)
        {
            _appsService.InstalledAppsChanged -= handler;
        }

        /// <summary>
        /// Подписаться на события прогресса установки APK
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void SubscribeToInstallProgressEvents(EventHandler<ApkInstallProgressEventArgs> handler)
        {
            _apkService.InstallProgressChanged += handler;
        }

        /// <summary>
        /// Отписаться от событий прогресса установки APK
        /// </summary>
        /// <param name="handler">Обработчик события</param>
        public void UnsubscribeFromInstallProgressEvents(EventHandler<ApkInstallProgressEventArgs> handler)
        {
            _apkService.InstallProgressChanged -= handler;
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            // Этот сервис не владеет ресурсами, которые требуют явного освобождения
            // Все зависимости управляются DI контейнером
            _logger.LogDebug("WSAIntegrationService disposed");
        }

        #endregion
    }
}
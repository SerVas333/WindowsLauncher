using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис для управления установленными Android приложениями с кэшированием и мониторингом изменений
    /// </summary>
    public class InstalledAppsService : IInstalledAppsService
    {
        private readonly IWSAConnectionService _connectionService;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<InstalledAppsService> _logger;

        private readonly ConcurrentDictionary<string, InstalledAndroidApp> _appsCache;
        private readonly Timer _cacheRefreshTimer;
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly SemaphoreSlim _refreshSemaphore;

        // Кэшированный путь к ADB для производительности
        private string? _adbPath;

        public event EventHandler<InstalledAppsChangedEventArgs>? InstalledAppsChanged;

        public InstalledAppsService(
            IWSAConnectionService connectionService,
            IProcessExecutor processExecutor,
            ILogger<InstalledAppsService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _appsCache = new ConcurrentDictionary<string, InstalledAndroidApp>();
            _refreshSemaphore = new SemaphoreSlim(1, 1);

            // Периодическое обновление кэша приложений (каждые 5 минут)
            _cacheRefreshTimer = new Timer(async _ => await RefreshAppsCacheInternal(), 
                null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            _logger.LogDebug("InstalledAppsService initialized");
        }

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync(bool includeSystemApps = false, bool useCache = true)
        {
            try
            {
                // Если кэш свежий и пользователь разрешает его использовать
                if (useCache && IsCacheValid() && _appsCache.Any())
                {
                    _logger.LogDebug("Returning cached apps list ({Count} apps)", _appsCache.Count);
                    return _appsCache.Values
                        .Where(app => !app.IsSystemApp || includeSystemApps)
                        .OrderBy(app => app.AppName ?? app.PackageName)
                        .ToList();
                }

                // Обновляем кэш
                await RefreshAppsCache();

                return _appsCache.Values
                    .Where(app => !app.IsSystemApp || includeSystemApps)
                    .OrderBy(app => app.AppName ?? app.PackageName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting installed apps");
                return Enumerable.Empty<InstalledAndroidApp>();
            }
        }

        public async Task<InstalledAndroidApp?> GetAppInfoAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return null;
            }

            try
            {
                // Сначала проверяем кэш
                if (_appsCache.TryGetValue(packageName, out var cachedApp) && IsCacheValid())
                {
                    _logger.LogDebug("Returning cached app info: {PackageName}", packageName);
                    return cachedApp;
                }

                if (!await EnsureConnectionAsync())
                {
                    return null;
                }

                _logger.LogDebug("Fetching detailed app info: {PackageName}", packageName);

                // Получаем детальную информацию о приложении
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell dumpsys package {packageName}", 10000);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Failed to get app info for {PackageName}: {Error}", packageName, result.StandardError);
                    return null;
                }

                var appInfo = ParseDetailedAppInfo(packageName, result.StandardOutput);
                
                // Обновляем кэш
                if (appInfo != null)
                {
                    _appsCache.AddOrUpdate(packageName, appInfo, (key, oldValue) => appInfo);
                }

                return appInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting app info: {PackageName}", packageName);
                return null;
            }
        }

        public async Task<bool> IsAppInstalledAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            try
            {
                // Проверяем кэш сначала
                if (_appsCache.ContainsKey(packageName) && IsCacheValid())
                {
                    return true;
                }

                if (!await EnsureConnectionAsync())
                {
                    return false;
                }

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell pm list packages {packageName}", 5000);

                bool installed = result.IsSuccess && result.StandardOutput.Contains($"package:{packageName}");
                
                _logger.LogDebug("App installation check: {PackageName} = {Installed}", packageName, installed);
                
                return installed;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception occurred while checking if app is installed: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<AppLaunchResult> LaunchAppAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return AppLaunchResult.CreateFailure(packageName, "Package name is null or empty");
            }

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return AppLaunchResult.CreateFailure(packageName, "Failed to connect to WSA");
                }

                _logger.LogInformation("Launching Android app: {PackageName}", packageName);

                // Проверяем что приложение установлено
                if (!await IsAppInstalledAsync(packageName))
                {
                    return AppLaunchResult.CreateFailure(packageName, "Application is not installed");
                }

                // Используем monkey для запуска приложения (более надежный метод)
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1", 15000);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Successfully launched app: {PackageName}", packageName);
                    
                    // Попытаемся получить PID процесса
                    var pidResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                        $"shell pidof {packageName}", 5000);
                    
                    int? processId = null;
                    if (pidResult.IsSuccess && int.TryParse(pidResult.StandardOutput.Trim(), out int pid))
                    {
                        processId = pid;
                        _logger.LogDebug("App {PackageName} launched with PID: {ProcessId}", packageName, pid);
                    }

                    // Обновляем статус в кэше
                    if (_appsCache.TryGetValue(packageName, out var app))
                    {
                        app.IsRunning = true;
                        app.LastLaunchedAt = DateTime.Now;
                    }

                    return AppLaunchResult.CreateSuccess(packageName, processId);
                }
                else
                {
                    var errorMessage = ParseLaunchError(result.StandardError + " " + result.StandardOutput);
                    _logger.LogError("Failed to launch app: {PackageName}. Error: {Error}", packageName, errorMessage);
                    return AppLaunchResult.CreateFailure(packageName, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while launching app: {PackageName}", packageName);
                return AppLaunchResult.CreateFailure(packageName, $"Launch exception: {ex.Message}");
            }
        }

        public async Task<bool> StopAppAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return false;
                }

                _logger.LogInformation("Stopping Android app: {PackageName}", packageName);

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell am force-stop {packageName}", 10000);

                bool success = result.IsSuccess;
                
                if (success)
                {
                    _logger.LogInformation("Successfully stopped app: {PackageName}", packageName);
                    
                    // Обновляем статус в кэше
                    if (_appsCache.TryGetValue(packageName, out var app))
                    {
                        app.IsRunning = false;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to stop app: {PackageName}. Error: {Error}", 
                        packageName, result.StandardError);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while stopping app: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<bool> UninstallAppAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return false;
                }

                _logger.LogInformation("Uninstalling Android app: {PackageName}", packageName);

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"uninstall {packageName}", 30000);
                
                bool success = result.IsSuccess && result.StandardOutput.Contains("Success");
                
                if (success)
                {
                    _logger.LogInformation("Successfully uninstalled app: {PackageName}", packageName);
                    
                    // Удаляем из кэша
                    if (_appsCache.TryRemove(packageName, out var removedApp))
                    {
                        OnInstalledAppsChanged(ChangeType.AppUninstalled, packageName, removedApp);
                    }
                }
                else
                {
                    var errorMessage = ParseUninstallError(result.StandardError + " " + result.StandardOutput);
                    _logger.LogWarning("Failed to uninstall app: {PackageName}. Error: {Error}", 
                        packageName, errorMessage);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while uninstalling app: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<string> GetAppLogsAsync(string packageName, int maxLines = 100)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return "Package name is empty";
            }

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return "Failed to connect to WSA";
                }

                _logger.LogDebug("Getting logs for app: {PackageName} (max {MaxLines} lines)", packageName, maxLines);

                // Используем logcat для получения логов приложения
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"logcat -t {maxLines} --pid=$(pidof {packageName} 2>/dev/null || echo 0)", 10000);

                if (result.IsSuccess)
                {
                    return string.IsNullOrEmpty(result.StandardOutput) 
                        ? $"No logs found for {packageName}" 
                        : result.StandardOutput;
                }
                else
                {
                    _logger.LogWarning("Failed to get logs for {PackageName}: {Error}", packageName, result.StandardError);
                    return $"Failed to get logs: {result.StandardError}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting app logs: {PackageName}", packageName);
                return $"Error getting logs: {ex.Message}";
            }
        }

        public async Task<bool> ClearAppDataAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return false;
            }

            try
            {
                if (!await EnsureConnectionAsync())
                {
                    return false;
                }

                _logger.LogInformation("Clearing data for Android app: {PackageName}", packageName);

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell pm clear {packageName}", 10000);

                bool success = result.IsSuccess && result.StandardOutput.Contains("Success");
                
                if (success)
                {
                    _logger.LogInformation("Successfully cleared app data: {PackageName}", packageName);
                }
                else
                {
                    _logger.LogWarning("Failed to clear app data: {PackageName}. Error: {Error}", 
                        packageName, result.StandardError);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while clearing app data: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetAppsUsageStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                var apps = await GetInstalledAppsAsync(includeSystemApps: false, useCache: true);
                var appsList = apps.ToList();

                stats["TotalUserApps"] = appsList.Count;
                stats["SystemApps"] = _appsCache.Values.Count(app => app.IsSystemApp);
                stats["RunningApps"] = appsList.Count(app => app.IsRunning);
                stats["RecentlyLaunched"] = appsList.Count(app => 
                    app.LastLaunchedAt.HasValue && 
                    (DateTime.Now - app.LastLaunchedAt.Value).TotalHours < 24);

                // Топ-5 самых используемых приложений (по времени последнего запуска)
                var topApps = appsList
                    .Where(app => app.LastLaunchedAt.HasValue)
                    .OrderByDescending(app => app.LastLaunchedAt!.Value)
                    .Take(5)
                    .Select(app => new { app.PackageName, app.AppName, app.LastLaunchedAt })
                    .ToList();

                stats["TopRecentApps"] = topApps;
                stats["LastCacheUpdate"] = _lastCacheUpdate;
                stats["CacheSize"] = _appsCache.Count;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting apps usage stats");
                stats["Error"] = ex.Message;
                return stats;
            }
        }

        public async Task<bool> RefreshAppsCache()
        {
            // Предотвращаем одновременное обновление кэша
            await _refreshSemaphore.WaitAsync();

            try
            {
                return await RefreshAppsCacheInternal();
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        #region Private Methods

        private async Task<bool> RefreshAppsCacheInternal()
        {
            try
            {
                if (!await EnsureConnectionAsync())
                {
                    _logger.LogDebug("Cannot refresh apps cache - WSA connection not available");
                    return false;
                }

                _logger.LogDebug("Refreshing installed apps cache");

                // Получаем список всех пакетов (пользовательские и системные)
                var userAppsResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", "shell pm list packages -3", 15000);
                var systemAppsResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", "shell pm list packages -s", 15000);

                if (!userAppsResult.IsSuccess && !systemAppsResult.IsSuccess)
                {
                    _logger.LogError("Failed to get installed apps list: User={UserError}, System={SystemError}", 
                        userAppsResult.StandardError, systemAppsResult.StandardError);
                    return false;
                }

                var newApps = new ConcurrentDictionary<string, InstalledAndroidApp>();

                // Парсим пользовательские приложения
                if (userAppsResult.IsSuccess)
                {
                    var userApps = ParsePackagesList(userAppsResult.StandardOutput, false);
                    foreach (var app in userApps)
                    {
                        newApps.TryAdd(app.PackageName, app);
                    }
                }

                // Парсим системные приложения
                if (systemAppsResult.IsSuccess)
                {
                    var systemApps = ParsePackagesList(systemAppsResult.StandardOutput, true);
                    foreach (var app in systemApps)
                    {
                        newApps.TryAdd(app.PackageName, app);
                    }
                }

                // Определяем изменения в списке приложений
                await DetectAppChanges(_appsCache, newApps);

                // Обновляем кэш
                _appsCache.Clear();
                foreach (var kvp in newApps)
                {
                    _appsCache.TryAdd(kvp.Key, kvp.Value);
                }

                _lastCacheUpdate = DateTime.Now;

                _logger.LogInformation("Apps cache refreshed: {UserApps} user apps, {TotalApps} total apps", 
                    newApps.Values.Count(app => !app.IsSystemApp), newApps.Count);

                OnInstalledAppsChanged(ChangeType.CacheRefreshed, "", null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while refreshing apps cache");
                return false;
            }
        }

        private async Task<bool> EnsureConnectionAsync()
        {
            try
            {
                if (!await _connectionService.ConnectToWSAAsync())
                {
                    return false;
                }

                // Обновляем путь к ADB если необходимо
                if (string.IsNullOrEmpty(_adbPath))
                {
                    var connectionStatus = await _connectionService.GetConnectionStatusAsync();
                    _adbPath = connectionStatus.GetValueOrDefault("ADBPath", null) as string;
                }

                return !string.IsNullOrEmpty(_adbPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to ensure WSA connection");
                return false;
            }
        }

        private bool IsCacheValid()
        {
            // Кэш считается валидным в течение 3 минут
            return (DateTime.Now - _lastCacheUpdate).TotalMinutes < 3;
        }

        private IEnumerable<InstalledAndroidApp> ParsePackagesList(string packagesOutput, bool isSystemApp)
        {
            var apps = new List<InstalledAndroidApp>();

            if (string.IsNullOrWhiteSpace(packagesOutput))
                return apps;

            var lines = packagesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("package:"))
                {
                    var packageName = trimmedLine.Substring("package:".Length).Trim();
                    
                    var app = new InstalledAndroidApp
                    {
                        PackageName = packageName,
                        IsSystemApp = isSystemApp,
                        IsEnabled = true,
                        AppName = GetFriendlyAppName(packageName),
                        InstallDate = DateTime.Now // Приблизительная дата
                    };

                    apps.Add(app);
                }
            }

            return apps;
        }

        private InstalledAndroidApp? ParseDetailedAppInfo(string packageName, string dumpsysOutput)
        {
            try
            {
                var app = _appsCache.GetValueOrDefault(packageName) ?? new InstalledAndroidApp
                {
                    PackageName = packageName,
                    IsSystemApp = IsSystemPackage(packageName)
                };

                // Парсим dumpsys для получения детальной информации
                var lines = dumpsysOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("versionName="))
                    {
                        app.VersionName = trimmedLine.Substring("versionName=".Length).Trim();
                    }
                    else if (trimmedLine.StartsWith("versionCode="))
                    {
                        if (int.TryParse(trimmedLine.Substring("versionCode=".Length).Split(' ')[0], out int versionCode))
                        {
                            app.VersionCode = versionCode;
                        }
                    }
                    else if (trimmedLine.StartsWith("firstInstallTime="))
                    {
                        var timeStr = trimmedLine.Substring("firstInstallTime=".Length).Trim();
                        if (DateTime.TryParse(timeStr, out DateTime installTime))
                        {
                            app.InstallDate = installTime;
                        }
                    }
                    else if (trimmedLine.StartsWith("lastUpdateTime="))
                    {
                        var timeStr = trimmedLine.Substring("lastUpdateTime=".Length).Trim();
                        if (DateTime.TryParse(timeStr, out DateTime updateTime))
                        {
                            app.LastUpdateDate = updateTime;
                        }
                    }
                }

                // Если нет человекопонятного имени, создаем его
                if (string.IsNullOrEmpty(app.AppName))
                {
                    app.AppName = GetFriendlyAppName(packageName);
                }

                return app;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse detailed app info from dumpsys output");
                return null;
            }
        }

        private async Task DetectAppChanges(ConcurrentDictionary<string, InstalledAndroidApp> oldApps, ConcurrentDictionary<string, InstalledAndroidApp> newApps)
        {
            try
            {
                // Находим новые приложения (установленные)
                var installedApps = newApps.Keys.Except(oldApps.Keys).ToList();
                foreach (var packageName in installedApps)
                {
                    if (newApps.TryGetValue(packageName, out var installedApp))
                    {
                        _logger.LogInformation("Detected new installed app: {PackageName}", packageName);
                        OnInstalledAppsChanged(ChangeType.AppInstalled, packageName, installedApp);
                    }
                }

                // Находим удаленные приложения
                var uninstalledApps = oldApps.Keys.Except(newApps.Keys).ToList();
                foreach (var packageName in uninstalledApps)
                {
                    if (oldApps.TryGetValue(packageName, out var uninstalledApp))
                    {
                        _logger.LogInformation("Detected uninstalled app: {PackageName}", packageName);
                        OnInstalledAppsChanged(ChangeType.AppUninstalled, packageName, uninstalledApp);
                    }
                }

                // Проверяем обновленные приложения
                var commonApps = oldApps.Keys.Intersect(newApps.Keys).ToList();
                foreach (var packageName in commonApps)
                {
                    if (oldApps.TryGetValue(packageName, out var oldApp) && 
                        newApps.TryGetValue(packageName, out var newApp))
                    {
                        // Сравниваем версии
                        if (oldApp.VersionCode != newApp.VersionCode || 
                            oldApp.VersionName != newApp.VersionName)
                        {
                            _logger.LogInformation("Detected updated app: {PackageName} ({OldVersion} -> {NewVersion})", 
                                packageName, oldApp.VersionName, newApp.VersionName);
                            OnInstalledAppsChanged(ChangeType.AppUpdated, packageName, newApp);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while detecting app changes");
            }
        }

        private static string GetFriendlyAppName(string packageName)
        {
            // Создаем более дружелюбное имя из package name
            var parts = packageName.Split('.');
            if (parts.Length > 0)
            {
                var appName = parts.Last();
                // Капитализируем первую букву
                return char.ToUpperInvariant(appName[0]) + appName.Substring(1);
            }
            
            return packageName;
        }

        private static bool IsSystemPackage(string packageName)
        {
            var systemPrefixes = new[]
            {
                "android.", "com.android.", "com.google.", "com.microsoft.",
                "com.samsung.", "com.qualcomm.", "org.chromium.", 
                "com.miui.", "com.xiaomi.", "com.huawei."
            };

            return systemPrefixes.Any(prefix => packageName.StartsWith(prefix));
        }

        private static string ParseLaunchError(string errorOutput)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                return "Unknown launch error";

            if (errorOutput.Contains("Permission denied"))
                return "Недостаточно разрешений для запуска приложения";
            if (errorOutput.Contains("No activities found"))
                return "У приложения нет главной активности для запуска";
            if (errorOutput.Contains("SecurityException"))
                return "Нарушение безопасности при запуске приложения";

            return errorOutput.Length > 100 ? errorOutput.Substring(0, 100) + "..." : errorOutput;
        }

        private static string ParseUninstallError(string errorOutput)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                return "Unknown uninstall error";

            if (errorOutput.Contains("DELETE_FAILED_INTERNAL_ERROR"))
                return "Внутренняя ошибка при удалении приложения";
            if (errorOutput.Contains("DELETE_FAILED_DEVICE_POLICY_MANAGER"))
                return "Приложение защищено политикой устройства";
            if (errorOutput.Contains("Failure"))
                return "Не удалось удалить приложение";

            return errorOutput.Length > 100 ? errorOutput.Substring(0, 100) + "..." : errorOutput;
        }

        private void OnInstalledAppsChanged(ChangeType changeType, string packageName, InstalledAndroidApp? appInfo)
        {
            try
            {
                InstalledAppsChanged?.Invoke(this, new InstalledAppsChangedEventArgs
                {
                    ChangeType = changeType,
                    PackageName = packageName,
                    AppInfo = appInfo,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error firing InstalledAppsChanged event");
            }
        }

        #endregion

        public void Dispose()
        {
            _cacheRefreshTimer?.Dispose();
            _refreshSemaphore?.Dispose();
        }
    }
}
using Microsoft.Extensions.Logging;
using System.IO;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;
using WindowsLauncher.Core.Enums;
using System.Text.Json;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.Core.Infrastructure.Extensions;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Высокоуровневый сервис для управления Android приложениями в WindowsLauncher
    /// </summary>
    public class AndroidApplicationManager : IAndroidApplicationManager
    {
        private readonly IWSAIntegrationService _wsaService;
        private readonly IAndroidSubsystemService _androidSubsystem;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AndroidApplicationManager> _logger;
        private bool? _androidSupportAvailable;
        private bool _environmentInitialized;
        private readonly Dictionary<string, ApkMetadata> _metadataCache;
        private readonly object _cacheLock = new object();

        public AndroidApplicationManager(IWSAIntegrationService wsaService, IAndroidSubsystemService androidSubsystem, IServiceScopeFactory serviceScopeFactory, ILogger<AndroidApplicationManager> logger)
        {
            _wsaService = wsaService ?? throw new ArgumentNullException(nameof(wsaService));
            _androidSubsystem = androidSubsystem ?? throw new ArgumentNullException(nameof(androidSubsystem));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metadataCache = new Dictionary<string, ApkMetadata>();
        }

        public async Task<bool> IsAndroidSupportAvailableAsync()
        {
            // Проверяем режим Android подсистемы
            if (_androidSubsystem.CurrentMode == AndroidMode.Disabled)
            {
                _logger.LogDebug("Android support disabled by configuration");
                return false;
            }

            if (_androidSupportAvailable.HasValue)
            {
                return _androidSupportAvailable.Value;
            }

            try
            {
                _logger.LogDebug("Checking Android support availability in {Mode} mode", _androidSubsystem.CurrentMode);

                // Используем AndroidSubsystemService для проверки доступности
                _androidSupportAvailable = await _androidSubsystem.IsAndroidAvailableAsync();

                if (_androidSupportAvailable.Value)
                {
                    _logger.LogInformation("Android support is available in {Mode} mode", _androidSubsystem.CurrentMode);
                }
                else
                {
                    _logger.LogWarning("Android support not available in {Mode} mode, WSA status: {Status}", 
                        _androidSubsystem.CurrentMode, _androidSubsystem.WSAStatus);
                }

                return _androidSupportAvailable.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while checking Android support availability");
                _androidSupportAvailable = false;
                return false;
            }
        }

        public async Task<bool> InitializeAndroidEnvironmentAsync()
        {
            // Проверяем режим Android подсистемы
            if (_androidSubsystem.CurrentMode == AndroidMode.Disabled)
            {
                _logger.LogDebug("Android environment initialization skipped - disabled by configuration");
                return false;
            }

            if (_environmentInitialized)
            {
                return true;
            }

            try
            {
                _logger.LogInformation("Initializing Android environment in {Mode} mode", _androidSubsystem.CurrentMode);

                if (!await IsAndroidSupportAvailableAsync())
                {
                    _logger.LogError("Cannot initialize Android environment - support not available");
                    return false;
                }

                // В режиме Preload WSA уже должен быть запущен
                if (_androidSubsystem.CurrentMode == AndroidMode.Preload)
                {
                    _logger.LogDebug("Using preloaded WSA environment");
                }
                
                // Запускаем WSA если не запущен (для OnDemand режима)
                bool wsaRunning = await _wsaService.IsWSARunningAsync();
                if (!wsaRunning)
                {
                    _logger.LogInformation("Starting WSA...");
                    wsaRunning = await _wsaService.StartWSAAsync();
                    
                    if (!wsaRunning)
                    {
                        _logger.LogError("Failed to start WSA");
                        return false;
                    }
                }

                // Подключаемся через ADB
                bool connected = await _wsaService.ConnectToWSAAsync();
                if (!connected)
                {
                    _logger.LogError("Failed to connect to WSA via ADB");
                    return false;
                }

                _environmentInitialized = true;
                _logger.LogInformation("Android environment initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while initializing Android environment");
                return false;
            }
        }

        public async Task<bool> ValidateApkAsync(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
            {
                _logger.LogWarning("APK path is null or empty");
                return false;
            }

            try
            {
                _logger.LogDebug("Validating APK: {ApkPath}", apkPath);

                if (!File.Exists(apkPath))
                {
                    _logger.LogWarning("APK file does not exist: {ApkPath}", apkPath);
                    return false;
                }

                var fileInfo = new FileInfo(apkPath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogWarning("APK file is empty: {ApkPath}", apkPath);
                    return false;
                }

                // Проверяем размер файла (слишком большие APK могут вызвать проблемы)
                const long MaxApkSize = 2L * 1024 * 1024 * 1024; // 2GB
                if (fileInfo.Length > MaxApkSize)
                {
                    _logger.LogWarning("APK file is too large ({SizeMB} MB): {ApkPath}", 
                        fileInfo.Length / (1024 * 1024), apkPath);
                    return false;
                }

                bool isValid = await _wsaService.ValidateApkFileAsync(apkPath);
                
                if (isValid)
                {
                    _logger.LogDebug("APK validation successful: {ApkPath}", apkPath);
                }
                else
                {
                    _logger.LogWarning("APK validation failed: {ApkPath}", apkPath);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while validating APK: {ApkPath}", apkPath);
                return false;
            }
        }

        public async Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath)
        {
            if (!await ValidateApkAsync(apkPath))
            {
                return null;
            }

            try
            {
                // Проверяем кэш метаданных
                lock (_cacheLock)
                {
                    if (_metadataCache.TryGetValue(apkPath, out var cachedMetadata))
                    {
                        var fileInfo = new FileInfo(apkPath);
                        
                        // Проверяем, не изменился ли файл с последнего кэширования
                        if (cachedMetadata.LastModified >= fileInfo.LastWriteTime &&
                            cachedMetadata.FileSizeBytes == fileInfo.Length)
                        {
                            _logger.LogDebug("Using cached metadata for APK: {ApkPath}", apkPath);
                            return cachedMetadata;
                        }
                        
                        // Удаляем устаревшие данные из кэша
                        _metadataCache.Remove(apkPath);
                    }
                }

                _logger.LogDebug("Extracting metadata for APK: {ApkPath}", apkPath);
                
                var metadata = await _wsaService.ExtractApkMetadataAsync(apkPath);
                
                if (metadata != null)
                {
                    // Кэшируем метаданные
                    lock (_cacheLock)
                    {
                        _metadataCache[apkPath] = metadata;
                        
                        // Ограничиваем размер кэша
                        if (_metadataCache.Count > 50)
                        {
                            var oldestEntry = _metadataCache.OrderBy(kvp => kvp.Value.LastModified).First();
                            _metadataCache.Remove(oldestEntry.Key);
                        }
                    }

                    _logger.LogInformation("Successfully extracted metadata for APK: {PackageName} v{Version} ({SizeMB:F1} MB)",
                        metadata.PackageName, metadata.GetVersionString(), 
                        metadata.FileSizeBytes / (1024.0 * 1024.0));
                }
                
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while extracting APK metadata: {ApkPath}", apkPath);
                return null;
            }
        }

        public async Task<ApkInstallResult> InstallApkAsync(string apkPath)
        {
            // Проверяем режим Android подсистемы
            if (_androidSubsystem.CurrentMode == AndroidMode.Disabled)
            {
                return ApkInstallResult.CreateFailure("Android subsystem is disabled");
            }

            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return ApkInstallResult.CreateFailure($"Android environment not available in {_androidSubsystem.CurrentMode} mode");
                }

                var metadata = await ExtractApkMetadataAsync(apkPath);
                if (metadata == null)
                {
                    return ApkInstallResult.CreateFailure("Invalid APK or failed to extract metadata");
                }

                _logger.LogInformation("Installing Android APK: {PackageName} v{Version}",
                    metadata.PackageName, metadata.GetVersionString());

                // Проверяем совместимость с WSA
                bool isCompatible = await IsApkCompatibleWithWSAAsync(metadata);
                if (!isCompatible)
                {
                    return ApkInstallResult.CreateFailure("APK is not compatible with current Android version in WSA");
                }

                // Проверяем, не установлено ли приложение уже
                bool alreadyInstalled = await _wsaService.IsAppInstalledAsync(metadata.PackageName);
                if (alreadyInstalled)
                {
                    _logger.LogWarning("Application {PackageName} is already installed", metadata.PackageName);
                    return ApkInstallResult.CreateFailure($"Application {metadata.PackageName} is already installed. Use update instead.");
                }

                var result = await _wsaService.InstallApkAsync(apkPath);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully installed Android app: {PackageName}", result.PackageName);
                }
                else
                {
                    _logger.LogError("Failed to install Android app: {PackageName}. Error: {Error}", 
                        metadata.PackageName, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing APK: {ApkPath}", apkPath);
                return ApkInstallResult.CreateFailure($"Installation exception: {ex.Message}");
            }
        }

        public async Task<ApkInstallResult> UpdateAppAsync(string packageName, string newApkPath)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return ApkInstallResult.CreateFailure("Android environment not available");
                }

                var newMetadata = await ExtractApkMetadataAsync(newApkPath);
                if (newMetadata == null)
                {
                    return ApkInstallResult.CreateFailure("Invalid APK or failed to extract metadata");
                }

                if (newMetadata.PackageName != packageName)
                {
                    return ApkInstallResult.CreateFailure("APK package name does not match expected package name");
                }

                _logger.LogInformation("Updating Android app: {PackageName} to version {Version}",
                    packageName, newMetadata.GetVersionString());

                // Получаем информацию о текущей установленной версии
                var currentApp = await _wsaService.GetAppInfoAsync(packageName);
                if (currentApp == null)
                {
                    return ApkInstallResult.CreateFailure("Application is not currently installed");
                }

                // Проверяем версии
                if (currentApp.VersionCode.HasValue && currentApp.VersionCode >= newMetadata.VersionCode)
                {
                    _logger.LogWarning("New APK version ({NewVersion}) is not newer than installed version ({CurrentVersion})",
                        newMetadata.VersionCode, currentApp.VersionCode);
                    return ApkInstallResult.CreateFailure("New version is not newer than currently installed version");
                }

                // Устанавливаем обновление (ADB install с флагом -r для replace)
                var result = await _wsaService.InstallApkAsync(newApkPath);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully updated Android app: {PackageName} to v{Version}", 
                        packageName, newMetadata.GetVersionString());
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while updating app: {PackageName}", packageName);
                return ApkInstallResult.CreateFailure($"Update exception: {ex.Message}");
            }
        }

        public async Task<AppLaunchResult> LaunchAndroidAppAsync(string packageName)
        {
            // Проверяем режим Android подсистемы
            if (_androidSubsystem.CurrentMode == AndroidMode.Disabled)
            {
                return AppLaunchResult.CreateFailure(packageName, "Android subsystem is disabled");
            }

            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return AppLaunchResult.CreateFailure(packageName, $"Android environment not available in {_androidSubsystem.CurrentMode} mode");
                }

                // Отмечаем активность для оптимизации ресурсов
                if (_androidSubsystem is AndroidSubsystemService service)
                {
                    service.MarkActivity();
                }

                // Проверяем, установлено ли приложение
                bool isInstalled = await _wsaService.IsAppInstalledAsync(packageName);
                if (!isInstalled)
                {
                    _logger.LogInformation("Android app {PackageName} is not installed, attempting automatic installation", packageName);
                    
                    // Попытка автоматической установки APK если файл доступен
                    var apkPath = await TryFindApkPathForPackageAsync(packageName);
                    if (!string.IsNullOrEmpty(apkPath))
                    {
                        _logger.LogDebug("Found APK file for package {PackageName}: {ApkPath}", packageName, apkPath);
                        
                        var installResult = await InstallApkAsync(apkPath);
                        if (!installResult.Success)
                        {
                            return AppLaunchResult.CreateFailure(packageName, $"Failed to automatically install APK: {installResult.ErrorMessage}");
                        }
                        
                        _logger.LogInformation("Successfully installed APK {ApkPath} for package {PackageName}", apkPath, packageName);
                    }
                    else
                    {
                        return AppLaunchResult.CreateFailure(packageName, "Application is not installed and APK file not found for automatic installation");
                    }
                }

                _logger.LogInformation("Launching Android app: {PackageName}", packageName);

                var result = await _wsaService.LaunchAppAsync(packageName);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully launched Android app: {PackageName}", packageName);
                }
                else
                {
                    _logger.LogError("Failed to launch Android app: {PackageName}. Error: {Error}", 
                        packageName, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while launching Android app: {PackageName}", packageName);
                return AppLaunchResult.CreateFailure(packageName, $"Launch exception: {ex.Message}");
            }
        }

        public async Task<bool> StopAndroidAppAsync(string packageName)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return false;
                }

                _logger.LogInformation("Stopping Android app: {PackageName}", packageName);
                
                bool result = await _wsaService.StopAppAsync(packageName);
                
                if (result)
                {
                    _logger.LogInformation("Successfully stopped Android app: {PackageName}", packageName);
                }
                else
                {
                    _logger.LogWarning("Failed to stop Android app: {PackageName}", packageName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while stopping Android app: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<bool> UninstallAndroidAppAsync(string packageName)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return false;
                }

                _logger.LogInformation("Uninstalling Android app: {PackageName}", packageName);

                // Сначала останавливаем приложение
                await _wsaService.StopAppAsync(packageName);

                bool result = await _wsaService.UninstallAppAsync(packageName);
                
                if (result)
                {
                    _logger.LogInformation("Successfully uninstalled Android app: {PackageName}", packageName);
                    
                    // Очищаем кэш метаданных
                    lock (_cacheLock)
                    {
                        var keysToRemove = _metadataCache.Where(kvp => kvp.Value.PackageName == packageName)
                                                        .Select(kvp => kvp.Key)
                                                        .ToList();
                        foreach (var key in keysToRemove)
                        {
                            _metadataCache.Remove(key);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to uninstall Android app: {PackageName}", packageName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while uninstalling Android app: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAndroidAppsAsync()
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return Enumerable.Empty<InstalledAndroidApp>();
                }

                var apps = await _wsaService.GetInstalledAppsAsync(includeSystemApps: false);
                
                _logger.LogDebug("Retrieved {AppCount} installed user Android apps", apps.Count());
                
                return apps.Where(app => app.IsUserApp && app.IsEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting installed Android apps");
                return Enumerable.Empty<InstalledAndroidApp>();
            }
        }

        public async Task<IEnumerable<(InstalledAndroidApp app, ApkMetadata newVersion)>> CheckForUpdatesAsync(string apkDirectory)
        {
            var updatesAvailable = new List<(InstalledAndroidApp app, ApkMetadata newVersion)>();

            try
            {
                if (!Directory.Exists(apkDirectory))
                {
                    _logger.LogWarning("APK directory does not exist: {ApkDirectory}", apkDirectory);
                    return updatesAvailable;
                }

                var installedApps = await GetInstalledAndroidAppsAsync();
                var apkFiles = Directory.GetFiles(apkDirectory, "*.apk");

                _logger.LogInformation("Checking for updates: {InstalledCount} apps, {ApkCount} APK files",
                    installedApps.Count(), apkFiles.Length);

                foreach (var apkFile in apkFiles)
                {
                    try
                    {
                        var metadata = await ExtractApkMetadataAsync(apkFile);
                        if (metadata == null) continue;

                        var installedApp = installedApps.FirstOrDefault(app => 
                            app.PackageName.Equals(metadata.PackageName, StringComparison.OrdinalIgnoreCase));

                        if (installedApp != null && 
                            installedApp.VersionCode.HasValue && 
                            metadata.VersionCode > installedApp.VersionCode)
                        {
                            _logger.LogInformation("Update available for {PackageName}: v{CurrentVersion} -> v{NewVersion}",
                                metadata.PackageName, installedApp.VersionCode, metadata.VersionCode);
                            
                            updatesAvailable.Add((installedApp, metadata));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check APK for updates: {ApkFile}", apkFile);
                    }
                }

                _logger.LogInformation("Found {UpdateCount} available updates", updatesAvailable.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while checking for Android app updates");
            }

            return updatesAvailable;
        }

        public async Task<InstalledAndroidApp?> GetAndroidAppDetailsAsync(string packageName)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return null;
                }

                return await _wsaService.GetAppInfoAsync(packageName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting Android app details: {PackageName}", packageName);
                return null;
            }
        }

        public async Task<bool> ClearAndroidAppCacheAsync(string packageName)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return false;
                }

                _logger.LogInformation("Clearing cache for Android app: {PackageName}", packageName);
                
                return await _wsaService.ClearAppDataAsync(packageName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while clearing Android app cache: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<string> GetAndroidAppLogsAsync(string packageName, int maxLines = 100)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return "Android environment not available";
                }

                return await _wsaService.GetAppLogsAsync(packageName, maxLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting Android app logs: {PackageName}", packageName);
                return $"Error getting logs: {ex.Message}";
            }
        }

        public async Task<bool> IsApkCompatibleWithWSAAsync(ApkMetadata apkMetadata)
        {
            try
            {
                if (!await InitializeAndroidEnvironmentAsync())
                {
                    return false;
                }

                var androidVersion = await _wsaService.GetAndroidVersionAsync();
                if (string.IsNullOrEmpty(androidVersion))
                {
                    _logger.LogWarning("Could not determine Android version in WSA");
                    return true; // Предполагаем совместимость если не можем определить
                }

                // Простая проверка совместимости по версии SDK
                // В реальности может потребоваться более сложная логика
                const int WSAAndroidSdkVersion = 33; // Android 13, типичная версия для WSA
                
                bool compatible = apkMetadata.MinSdkVersion <= WSAAndroidSdkVersion;
                
                if (!compatible)
                {
                    _logger.LogWarning("APK {PackageName} requires Android SDK {MinSdk}, but WSA has SDK {WSASdk}",
                        apkMetadata.PackageName, apkMetadata.MinSdkVersion, WSAAndroidSdkVersion);
                }

                return compatible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while checking APK compatibility");
                return true; // Предполагаем совместимость при ошибке
            }
        }

        public async Task<Dictionary<string, object>> GetAndroidUsageStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                stats["SupportAvailable"] = await IsAndroidSupportAvailableAsync();
                stats["EnvironmentInitialized"] = _environmentInitialized;
                
                if (_environmentInitialized)
                {
                    var apps = await GetInstalledAndroidAppsAsync();
                    stats["InstalledAppsCount"] = apps.Count();
                    
                    var wsaStatus = await _wsaService.GetWSAStatusAsync();
                    foreach (var kvp in wsaStatus)
                    {
                        stats[$"WSA_{kvp.Key}"] = kvp.Value;
                    }
                }

                lock (_cacheLock)
                {
                    stats["MetadataCacheSize"] = _metadataCache.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting Android usage stats");
                stats["Error"] = ex.Message;
            }

            return stats;
        }

        public async Task<string> RunAndroidDiagnosticsAsync()
        {
            var diagnostics = new List<string>();

            try
            {
                diagnostics.Add("=== Android Support Diagnostics ===");
                diagnostics.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                diagnostics.Add("");

                // Проверка доступности компонентов
                bool wsaAvailable = await _wsaService.IsWSAAvailableAsync();
                bool adbAvailable = await _wsaService.IsAdbAvailableAsync();
                bool wsaRunning = await _wsaService.IsWSARunningAsync();

                diagnostics.Add($"WSA Available: {wsaAvailable}");
                diagnostics.Add($"ADB Available: {adbAvailable}");
                diagnostics.Add($"WSA Running: {wsaRunning}");
                diagnostics.Add($"Environment Initialized: {_environmentInitialized}");
                diagnostics.Add("");
                
                // Детальная информация о инструментах
                diagnostics.Add("Tool Diagnostics:");
                if (adbAvailable)
                {
                    try
                    {
                        var androidVersion = await _wsaService.GetAndroidVersionAsync();
                        diagnostics.Add($"  Android Version: {androidVersion ?? "Unknown"}");
                    }
                    catch
                    {
                        diagnostics.Add("  Android Version: Failed to detect");
                    }
                }
                
                // Проверяем установку Android инструментов
                var adbPath = Environment.GetEnvironmentVariable("PATH")?.Split(';')
                    .Select(p => Path.Combine(p, "adb.exe"))
                    .FirstOrDefault(File.Exists);
                    
                var aaptPath = Environment.GetEnvironmentVariable("PATH")?.Split(';')
                    .Select(p => Path.Combine(p, "aapt.exe"))
                    .FirstOrDefault(File.Exists);
                
                diagnostics.Add($"  ADB Path: {adbPath ?? "Not found in PATH"}");
                diagnostics.Add($"  AAPT Path: {aaptPath ?? "Not found in PATH"}");
                
                // Проверяем стандартные локации WindowsLauncher
                var standardAdbPath = @"C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe";
                var standardAaptPath = @"C:\WindowsLauncher\Tools\Android\android-14\aapt.exe";
                
                if (File.Exists(standardAdbPath))
                    diagnostics.Add($"  WindowsLauncher ADB: {standardAdbPath}");
                if (File.Exists(standardAaptPath))
                    diagnostics.Add($"  WindowsLauncher AAPT: {standardAaptPath}");
                
                diagnostics.Add("");

                if (wsaAvailable && adbAvailable)
                {
                    var wsaStatus = await _wsaService.GetWSAStatusAsync();
                    diagnostics.Add("WSA Status:");
                    foreach (var kvp in wsaStatus)
                    {
                        diagnostics.Add($"  {kvp.Key}: {kvp.Value}");
                    }
                    diagnostics.Add("");

                    var apps = await GetInstalledAndroidAppsAsync();
                    diagnostics.Add($"Installed User Apps: {apps.Count()}");
                    foreach (var app in apps.Take(10)) // Показываем первые 10
                    {
                        diagnostics.Add($"  - {app.GetDisplayName()} ({app.PackageName}) v{app.GetVersionString()}");
                    }
                    if (apps.Count() > 10)
                    {
                        diagnostics.Add($"  ... and {apps.Count() - 10} more apps");
                    }
                }

                diagnostics.Add("");
                diagnostics.Add($"Metadata Cache: {_metadataCache.Count} entries");
                diagnostics.Add($"Support Available: {await IsAndroidSupportAvailableAsync()}");
                
                _logger.LogInformation("Android diagnostics completed successfully");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"ERROR: {ex.Message}");
                _logger.LogError(ex, "Exception occurred while running Android diagnostics");
            }

            return string.Join(Environment.NewLine, diagnostics);
        }

        /// <summary>
        /// Пытается найти APK файл для указанного package name через базу данных приложений
        /// </summary>
        /// <param name="packageName">Package name Android приложения</param>
        /// <returns>Путь к APK файлу или null если не найден</returns>
        private async Task<string?> TryFindApkPathForPackageAsync(string packageName)
        {
            try
            {
                _logger.LogDebug("Searching for APK file for package: {PackageName}", packageName);

                // Получаем IApplicationService через ServiceScopeFactory чтобы избежать циклической зависимости
                var applicationService = _serviceScopeFactory.CreateScopedService<IApplicationService>();
                
                // Получаем все Android приложения из базы данных
                var allApps = await applicationService.GetAllApplicationsAsync();
                var androidApps = allApps.Where(app => app.Type == ApplicationType.Android).ToList();

                _logger.LogDebug("Found {Count} Android applications in database", androidApps.Count);

                foreach (var app in androidApps)
                {
                    // Проверяем ApkPackageName если доступно
                    if (!string.IsNullOrEmpty(app.ApkPackageName) && 
                        app.ApkPackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Проверяем ApkFilePath первым приоритетом
                        if (!string.IsNullOrEmpty(app.ApkFilePath) && File.Exists(app.ApkFilePath))
                        {
                            _logger.LogDebug("Found APK via ApkFilePath for {PackageName}: {ApkPath}", packageName, app.ApkFilePath);
                            return app.ApkFilePath;
                        }

                        // Если ApkFilePath не задан или файл не существует, проверяем ExecutablePath
                        if (!string.IsNullOrEmpty(app.ExecutablePath) && 
                            app.ExecutablePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) && 
                            File.Exists(app.ExecutablePath))
                        {
                            _logger.LogDebug("Found APK via ExecutablePath for {PackageName}: {ApkPath}", packageName, app.ExecutablePath);
                            return app.ExecutablePath;
                        }
                    }

                    // Fallback: если package name не совпадает, попробуем извлечь его из APK/XAPK файла
                    string? apkPath = null;
                    if (!string.IsNullOrEmpty(app.ApkFilePath) && File.Exists(app.ApkFilePath))
                    {
                        apkPath = app.ApkFilePath;
                    }
                    else if (!string.IsNullOrEmpty(app.ExecutablePath) && File.Exists(app.ExecutablePath))
                    {
                        var executablePath = app.ExecutablePath.ToLowerInvariant();
                        if (executablePath.EndsWith(".apk") || executablePath.EndsWith(".xapk"))
                        {
                            apkPath = app.ExecutablePath;
                        }
                    }

                    if (!string.IsNullOrEmpty(apkPath))
                    {
                        try
                        {
                            // Извлекаем метаданные чтобы проверить package name
                            var metadata = await ExtractApkMetadataAsync(apkPath);
                            if (metadata != null && metadata.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogDebug("Found APK via metadata extraction for {PackageName}: {ApkPath}", packageName, apkPath);
                                return apkPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to extract metadata from APK {ApkPath} during search for {PackageName}", apkPath, packageName);
                        }
                    }
                }

                _logger.LogWarning("APK file not found for package {PackageName}", packageName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while searching for APK file for package {PackageName}", packageName);
                return null;
            }
        }
    }
}
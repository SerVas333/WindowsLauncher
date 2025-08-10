using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис для низкоуровневой интеграции с Windows Subsystem for Android (WSA)
    /// </summary>
    public class WSAIntegrationService : IWSAIntegrationService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<WSAIntegrationService> _logger;
        private bool? _wsaAvailable;
        private bool? _adbAvailable;
        private string? _adbPath;
        private string? _aaptPath;

        public WSAIntegrationService(IProcessExecutor processExecutor, ILogger<WSAIntegrationService> logger)
        {
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsWSAAvailableAsync()
        {
            if (_wsaAvailable.HasValue)
            {
                return _wsaAvailable.Value;
            }

            try
            {
                // Проверка через PowerShell - наличие WSA пакета
                var result = await _processExecutor.ExecutePowerShellAsync(
                    "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 10000);

                _wsaAvailable = result.IsSuccess && !string.IsNullOrEmpty(result.StandardOutput);
                
                if (_wsaAvailable.Value)
                {
                    _logger.LogInformation("Windows Subsystem for Android is available");
                }
                else
                {
                    _logger.LogWarning("Windows Subsystem for Android is not installed");
                }

                return _wsaAvailable.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check WSA availability");
                _wsaAvailable = false;
                return false;
            }
        }

        public async Task<bool> IsWSARunningAsync()
        {
            try
            {
                // Проверяем запущен ли процесс WSA
                var result = await _processExecutor.ExecutePowerShellAsync(
                    "Get-Process WsaClient -ErrorAction SilentlyContinue", 5000);

                bool isRunning = result.IsSuccess && !string.IsNullOrEmpty(result.StandardOutput);
                
                _logger.LogDebug("WSA running status: {IsRunning}", isRunning);
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to check WSA running status");
                return false;
            }
        }

        public async Task<bool> StartWSAAsync()
        {
            try
            {
                if (await IsWSARunningAsync())
                {
                    _logger.LogDebug("WSA is already running");
                    return true;
                }

                _logger.LogInformation("Starting Windows Subsystem for Android");

                // Попытка запустить WSA через PowerShell
                var result = await _processExecutor.ExecutePowerShellAsync(
                    "Start-Process -FilePath 'wsa://system' -WindowStyle Hidden", 15000);

                if (result.IsSuccess)
                {
                    // Ждем немного для инициализации
                    await Task.Delay(3000);
                    
                    bool started = await IsWSARunningAsync();
                    if (started)
                    {
                        _logger.LogInformation("WSA started successfully");
                        return true;
                    }
                }

                _logger.LogWarning("Failed to start WSA via PowerShell, trying alternative method");

                // Альтернативный метод через WsaClient
                var wsaClientResult = await _processExecutor.ExecuteAsync("WsaClient", "/launch wsa://system", 15000);
                
                if (wsaClientResult.IsSuccess)
                {
                    await Task.Delay(3000);
                    return await IsWSARunningAsync();
                }

                _logger.LogError("Failed to start WSA using all available methods");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while starting WSA");
                return false;
            }
        }

        public async Task<bool> StopWSAAsync()
        {
            try
            {
                if (!await IsWSARunningAsync())
                {
                    _logger.LogDebug("WSA is not running");
                    return true;
                }

                _logger.LogInformation("Stopping Windows Subsystem for Android");

                var result = await _processExecutor.ExecutePowerShellAsync(
                    "Stop-Process -Name 'WsaClient' -Force -ErrorAction SilentlyContinue", 10000);

                // Ждем завершения процесса
                await Task.Delay(2000);
                
                bool stopped = !await IsWSARunningAsync();
                if (stopped)
                {
                    _logger.LogInformation("WSA stopped successfully");
                }
                else
                {
                    _logger.LogWarning("WSA may not have stopped completely");
                }

                return stopped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while stopping WSA");
                return false;
            }
        }

        public async Task<bool> IsAdbAvailableAsync()
        {
            if (_adbAvailable.HasValue)
            {
                return _adbAvailable.Value;
            }

            try
            {
                // Сначала ищем ADB в PATH
                _adbPath = await _processExecutor.GetCommandPathAsync("adb");
                
                if (string.IsNullOrEmpty(_adbPath))
                {
                    _logger.LogWarning("ADB command not found in PATH");
                    
                    // Ищем ADB в стандартных локациях WindowsLauncher
                    await EnsureAdbAvailableAsync();
                }

                // Если ADB найден, проверяем его работоспособность
                if (!string.IsNullOrEmpty(_adbPath))
                {
                    var result = await _processExecutor.ExecuteAsync(_adbPath, "version", 5000);
                    
                    _adbAvailable = result.IsSuccess && result.StandardOutput.Contains("Android Debug Bridge");
                    
                    if (_adbAvailable.Value)
                    {
                        _logger.LogInformation("ADB is available at: {AdbPath}", _adbPath);
                    }
                    else
                    {
                        _logger.LogWarning("ADB command found but not working properly");
                    }
                }
                else
                {
                    _adbAvailable = false;
                }

                return _adbAvailable.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check ADB availability");
                _adbAvailable = false;
                return false;
            }
        }

        public async Task<bool> ConnectToWSAAsync()
        {
            try
            {
                if (!await IsAdbAvailableAsync())
                {
                    _logger.LogError("ADB is not available, cannot connect to WSA");
                    return false;
                }

                // Подключаемся к WSA по локальному адресу
                var connectResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", "connect 127.0.0.1:58526", 10000);
                
                if (!connectResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to connect to WSA: {Error}", connectResult.StandardError);
                    return false;
                }

                // Проверяем подключение
                var devicesResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", "devices", 5000);
                
                bool connected = devicesResult.IsSuccess && 
                               devicesResult.StandardOutput.Contains("127.0.0.1:58526") &&
                               devicesResult.StandardOutput.Contains("device");

                if (connected)
                {
                    _logger.LogInformation("Successfully connected to WSA via ADB");
                }
                else
                {
                    _logger.LogWarning("ADB connection established but device not ready");
                }

                return connected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while connecting to WSA");
                return false;
            }
        }

        public async Task<bool> ValidateApkFileAsync(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
            {
                return false;
            }

            // Поддержка как APK так и XAPK файлов
            bool isApk = apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase);
            bool isXapk = apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase);
            
            if (!isApk && !isXapk)
            {
                _logger.LogWarning("File does not have .apk or .xapk extension: {ApkPath}", apkPath);
                return false;
            }

            if (!File.Exists(apkPath))
            {
                _logger.LogWarning("APK file does not exist: {ApkPath}", apkPath);
                return false;
            }

            try
            {
                // Для XAPK файлов проверяем как ZIP архив
                if (isXapk)
                {
                    return await ValidateXapkFileAsync(apkPath);
                }
                
                // Для APK файлов используем AAPT
                // Проверяем доступность AAPT
                await EnsureAaptAvailableAsync();
                
                if (string.IsNullOrEmpty(_aaptPath))
                {
                    _logger.LogWarning("AAPT not available, cannot validate APK structure");
                    return false;
                }

                // Проверяем структуру APK с помощью AAPT
                var result = await _processExecutor.ExecuteAsync(_aaptPath, $"dump badging \"{apkPath}\"", 15000);
                
                bool isValid = result.IsSuccess && 
                              result.StandardOutput.Contains("package:") &&
                              result.StandardOutput.Contains("versionCode");

                if (!isValid)
                {
                    _logger.LogWarning("APK validation failed: {ApkPath}. Error: {Error}", 
                        apkPath, result.StandardError);
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
            try
            {
                if (!await ValidateApkFileAsync(apkPath))
                {
                    return null;
                }

                // Для XAPK файлов используем специальную обработку
                if (apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExtractXapkMetadataAsync(apkPath);
                }

                await EnsureAaptAvailableAsync();
                
                ApkMetadata? metadata = null;
                
                // Попробуем извлечь метаданные через AAPT (предпочтительный метод)
                if (!string.IsNullOrEmpty(_aaptPath))
                {
                    metadata = await ExtractMetadataWithAaptAsync(apkPath);
                }
                
                // Fallback: читаем APK как ZIP архив
                if (metadata == null)
                {
                    _logger.LogInformation("Falling back to ZIP-based APK metadata extraction for: {ApkPath}", apkPath);
                    metadata = await ExtractMetadataFromZipAsync(apkPath);
                }
                
                if (metadata != null)
                {
                    // Добавляем информацию о файле
                    var fileInfo = new FileInfo(apkPath);
                    metadata.FileSizeBytes = fileInfo.Length;
                    metadata.LastModified = fileInfo.LastWriteTime;
                    metadata.FileHash = await CalculateFileHashAsync(apkPath);
                    
                    _logger.LogDebug("Successfully extracted metadata for APK: {PackageName} v{Version}", 
                        metadata.PackageName, metadata.GetVersionString());
                }
                else
                {
                    _logger.LogError("Failed to extract APK metadata using all available methods: {ApkPath}", apkPath);
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
            try
            {
                if (!await IsAdbAvailableAsync())
                {
                    return ApkInstallResult.CreateFailure("ADB is not available");
                }

                if (!await ConnectToWSAAsync())
                {
                    return ApkInstallResult.CreateFailure("Failed to connect to WSA");
                }

                // Проверяем, является ли файл XAPK
                if (apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Installing XAPK file: {XapkPath}", apkPath);
                    return await InstallXapkAsync(apkPath);
                }
                {
                    return ApkInstallResult.CreateFailure("Failed to connect to WSA");
                }

                var metadata = await ExtractApkMetadataAsync(apkPath);
                if (metadata == null)
                {
                    return ApkInstallResult.CreateFailure("Invalid APK file or failed to extract metadata");
                }

                _logger.LogInformation("Installing APK: {PackageName} from {ApkPath}", 
                    metadata.PackageName, apkPath);

                // Сначала пробуем обычную установку
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install \"{apkPath}\"", 60000);
                
                if (result.IsSuccess && result.StandardOutput.Contains("Success"))
                {
                    _logger.LogInformation("Successfully installed APK: {PackageName}", metadata.PackageName);
                    return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
                }
                
                // Если обычная установка не удалась и ошибка связана с missing split, пробуем split установку
                bool isMissingSplitError = (result.StandardError + " " + result.StandardOutput)
                    .Contains("INSTALL_FAILED_MISSING_SPLIT", StringComparison.OrdinalIgnoreCase);
                    
                if (isMissingSplitError)
                {
                    _logger.LogWarning("Standard install failed with MISSING_SPLIT, attempting split APK installation for {PackageName}", metadata.PackageName);
                    
                    // Пробуем установку с флагом -t (allow test APKs) и -r (replace existing)
                    var splitResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install -t -r \"{apkPath}\"", 60000);
                    
                    if (splitResult.IsSuccess && splitResult.StandardOutput.Contains("Success"))
                    {
                        _logger.LogInformation("Successfully installed APK using split installation: {PackageName}", metadata.PackageName);
                        return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
                    }
                    
                    // Если и это не помогло, пробуем установку с принудительной установкой всех разрешений
                    var forceResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install -g -t -r \"{apkPath}\"", 60000);
                    
                    if (forceResult.IsSuccess && forceResult.StandardOutput.Contains("Success"))
                    {
                        _logger.LogInformation("Successfully installed APK using forced installation: {PackageName}", metadata.PackageName);
                        return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
                    }
                    
                    // Все методы не сработали
                    var splitErrorMessage = ParseAdbInstallError(forceResult.StandardError + " " + forceResult.StandardOutput);
                    _logger.LogError("All installation methods failed for split APK: {PackageName}. Last error: {Error}", metadata.PackageName, splitErrorMessage);
                    
                    return ApkInstallResult.CreateFailure($"Split APK installation failed: {splitErrorMessage}. This APK may require Android App Bundle format or additional split files.");
                }
                else
                {
                    var errorMessage = ParseAdbInstallError(result.StandardError + " " + result.StandardOutput);
                    _logger.LogError("Failed to install APK: {PackageName}. Error: {Error}", 
                        metadata.PackageName, errorMessage);
                    return ApkInstallResult.CreateFailure(errorMessage, result.ExitCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing APK: {ApkPath}", apkPath);
                return ApkInstallResult.CreateFailure($"Installation exception: {ex.Message}");
            }
        }

        public async Task<bool> UninstallAppAsync(string packageName)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return false;
                }

                _logger.LogInformation("Uninstalling Android app: {PackageName}", packageName);

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"uninstall {packageName}", 30000);
                
                bool success = result.IsSuccess && result.StandardOutput.Contains("Success");
                
                if (success)
                {
                    _logger.LogInformation("Successfully uninstalled app: {PackageName}", packageName);
                }
                else
                {
                    _logger.LogWarning("Failed to uninstall app: {PackageName}. Error: {Error}", 
                        packageName, result.StandardError);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while uninstalling app: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<AppLaunchResult> LaunchAppAsync(string packageName)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return AppLaunchResult.CreateFailure(packageName, "Failed to connect to WSA");
                }

                _logger.LogInformation("Launching Android app: {PackageName}", packageName);

                // Используем monkey для запуска приложения
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
                    }

                    return AppLaunchResult.CreateSuccess(packageName, processId);
                }
                else
                {
                    var errorMessage = $"Failed to launch app: {result.StandardError}";
                    _logger.LogError("Failed to launch app: {PackageName}. Error: {Error}", 
                        packageName, errorMessage);
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
            try
            {
                if (!await ConnectToWSAAsync())
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

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync(bool includeSystemApps = false)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return Enumerable.Empty<InstalledAndroidApp>();
                }

                var command = includeSystemApps ? "shell pm list packages" : "shell pm list packages -3";
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", command, 15000);

                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to get installed apps list: {Error}", result.StandardError);
                    return Enumerable.Empty<InstalledAndroidApp>();
                }

                var apps = ParseInstalledApps(result.StandardOutput, includeSystemApps);
                _logger.LogDebug("Found {AppCount} installed Android apps", apps.Count());
                
                return apps;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting installed apps");
                return Enumerable.Empty<InstalledAndroidApp>();
            }
        }

        public async Task<InstalledAndroidApp?> GetAppInfoAsync(string packageName)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return null;
                }

                // Получаем информацию о приложении
                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell dumpsys package {packageName}", 10000);

                if (!result.IsSuccess)
                {
                    return null;
                }

                return ParseSingleAppInfo(packageName, result.StandardOutput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting app info: {PackageName}", packageName);
                return null;
            }
        }

        public async Task<bool> IsAppInstalledAsync(string packageName)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return false;
                }

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell pm list packages {packageName}", 5000);

                return result.IsSuccess && result.StandardOutput.Contains($"package:{packageName}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception occurred while checking if app is installed: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<string> GetAppLogsAsync(string packageName, int maxLines = 100)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return "Failed to connect to WSA";
                }

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"logcat -t {maxLines} | grep {packageName}", 10000);

                return result.IsSuccess ? result.StandardOutput : result.StandardError;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting app logs: {PackageName}", packageName);
                return $"Error getting logs: {ex.Message}";
            }
        }

        public async Task<bool> ClearAppDataAsync(string packageName)
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return false;
                }

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    $"shell pm clear {packageName}", 10000);

                bool success = result.IsSuccess && result.StandardOutput.Contains("Success");
                
                if (success)
                {
                    _logger.LogInformation("Successfully cleared app data: {PackageName}", packageName);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while clearing app data: {PackageName}", packageName);
                return false;
            }
        }

        public async Task<string?> GetAndroidVersionAsync()
        {
            try
            {
                if (!await ConnectToWSAAsync())
                {
                    return null;
                }

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                    "shell getprop ro.build.version.release", 5000);

                return result.IsSuccess ? result.StandardOutput.Trim() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting Android version");
                return null;
            }
        }

        public async Task<Dictionary<string, object>> GetWSAStatusAsync()
        {
            var status = new Dictionary<string, object>();

            try
            {
                status["WSAAvailable"] = await IsWSAAvailableAsync();
                status["WSARunning"] = await IsWSARunningAsync();
                status["ADBAvailable"] = await IsAdbAvailableAsync();
                status["AndroidVersion"] = await GetAndroidVersionAsync();
                
                if ((bool)status["WSARunning"])
                {
                    var apps = await GetInstalledAppsAsync();
                    status["InstalledAppsCount"] = apps.Count();
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting WSA status");
                status["Error"] = ex.Message;
                return status;
            }
        }

        #region Private Helper Methods

        private async Task EnsureAaptAvailableAsync()
        {
            if (!string.IsNullOrEmpty(_aaptPath))
            {
                return;
            }

            // Попробуем найти AAPT в PATH
            _aaptPath = await _processExecutor.GetCommandPathAsync("aapt");
            
            if (string.IsNullOrEmpty(_aaptPath))
            {
                // Поищем AAPT в стандартных локациях WindowsLauncher  
                var androidToolsPath = @"C:\WindowsLauncher\Tools\Android";
                if (Directory.Exists(androidToolsPath))
                {
                    // Ищем aapt.exe во всех подпапках
                    var aaptFiles = Directory.GetFiles(androidToolsPath, "aapt.exe", SearchOption.AllDirectories);
                    if (aaptFiles.Length > 0)
                    {
                        _aaptPath = aaptFiles[0];
                        _logger.LogInformation("Found AAPT at: {AaptPath}", _aaptPath);
                        return;
                    }
                }
                
                // Дополнительные локации для поиска
                var possiblePaths = new[]
                {
                    @"C:\WindowsLauncher\Tools\Android\android-14\aapt.exe",
                    @"C:\WindowsLauncher\Tools\Android\android-13\aapt.exe",
                    @"C:\WindowsLauncher\Tools\Android\aapt.exe", 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        @"WindowsLauncher\Tools\Android\aapt.exe")
                };
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        _aaptPath = path;
                        _logger.LogInformation("Found AAPT at: {AaptPath}", _aaptPath);
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(_aaptPath))
            {
                _logger.LogWarning("AAPT not found in PATH or standard locations. APK metadata extraction may not work properly.");
                _logger.LogInformation("To install AAPT automatically, run: .\\Scripts\\Install-AndroidTools.ps1");
            }
            else
            {
                _logger.LogDebug("Using AAPT from: {AaptPath}", _aaptPath);
            }
        }

        private async Task EnsureAdbAvailableAsync()
        {
            if (!string.IsNullOrEmpty(_adbPath))
            {
                return;
            }

            // Попробуем найти ADB в PATH
            _adbPath = await _processExecutor.GetCommandPathAsync("adb");
            
            if (string.IsNullOrEmpty(_adbPath))
            {
                // Поищем ADB в стандартных локациях WindowsLauncher  
                var androidToolsPath = @"C:\WindowsLauncher\Tools\Android";
                if (Directory.Exists(androidToolsPath))
                {
                    // Ищем adb.exe во всех подпапках
                    var adbFiles = Directory.GetFiles(androidToolsPath, "adb.exe", SearchOption.AllDirectories);
                    if (adbFiles.Length > 0)
                    {
                        _adbPath = adbFiles[0];
                        _logger.LogInformation("Found ADB at: {AdbPath}", _adbPath);
                        return;
                    }
                }
                
                // Дополнительные локации для поиска (в порядке приоритета)
                var possiblePaths = new[]
                {
                    @"C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe", // Основной путь для Platform Tools
                    @"C:\WindowsLauncher\Tools\Android\adb.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        @"WindowsLauncher\Tools\Android\platform-tools\adb.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        @"WindowsLauncher\Tools\Android\adb.exe")
                };
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        _adbPath = path;
                        _logger.LogInformation("Found ADB at: {AdbPath}", _adbPath);
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(_adbPath))
            {
                _logger.LogWarning("ADB not found in PATH or standard locations. Android app installation and launching will not work.");
                _logger.LogInformation("To install ADB automatically, run: .\\Scripts\\Install-AndroidTools.ps1");
            }
            else
            {
                _logger.LogDebug("Using ADB from: {AdbPath}", _adbPath);
            }
        }

        /// <summary>
        /// Извлечение метаданных APK с помощью AAPT
        /// </summary>
        private async Task<ApkMetadata?> ExtractMetadataWithAaptAsync(string apkPath)
        {
            try
            {
                var result = await _processExecutor.ExecuteAsync(_aaptPath!, $"dump badging \"{apkPath}\"", 20000);
                
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("AAPT failed to extract APK metadata: {Error}", result.StandardError);
                    return null;
                }

                return ParseApkMetadata(result.StandardOutput);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during AAPT metadata extraction: {ApkPath}", apkPath);
                return null;
            }
        }
        
        /// <summary>
        /// Fallback метод: извлечение базовых метаданных APK путём чтения как ZIP архива
        /// </summary>
        private async Task<ApkMetadata?> ExtractMetadataFromZipAsync(string apkPath)
        {
            try
            {
                using var zip = new System.IO.Compression.ZipArchive(File.OpenRead(apkPath), System.IO.Compression.ZipArchiveMode.Read);
                
                // Ищем AndroidManifest.xml
                var manifestEntry = zip.GetEntry("AndroidManifest.xml");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("AndroidManifest.xml not found in APK: {ApkPath}", apkPath);
                    return null;
                }
                
                // Создаём базовые метаданные из имени файла
                var metadata = new ApkMetadata();
                
                // Извлекаем имя пакета из имени файла (очень примитивно)
                var fileName = Path.GetFileNameWithoutExtension(apkPath);
                var cleanName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\w\.]", ".");
                metadata.PackageName = $"com.unknown.{cleanName}";
                
                // Примитивное извлечение версии из имени файла
                var versionMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d+\.)+\d+");
                if (versionMatch.Success)
                {
                    metadata.VersionName = versionMatch.Value;
                    var versionParts = versionMatch.Value.Split('.');
                    if (versionParts.Length > 0 && int.TryParse(string.Join("", versionParts), out var versionCode))
                    {
                        metadata.VersionCode = Math.Max(1, versionCode % 1000000); // Ограничиваем размер
                    }
                }
                
                // Устанавливаем базовые значения SDK
                metadata.MinSdkVersion = 21; // Android 5.0 - разумный минимум
                metadata.TargetSdkVersion = 33; // Android 13 - современная цель
                metadata.AppName = fileName;
                
                _logger.LogInformation("Extracted basic metadata from APK filename: Package={Package}, Version={Version}", 
                    metadata.PackageName, metadata.VersionName);
                    
                return metadata.IsValid() ? metadata : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract metadata from APK as ZIP: {ApkPath}", apkPath);
                return null;
            }
        }

        private ApkMetadata? ParseApkMetadata(string aaptOutput)
        {
            if (string.IsNullOrWhiteSpace(aaptOutput))
                return null;

            try
            {
                var metadata = new ApkMetadata();
                var lines = aaptOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("package:"))
                    {
                        metadata.PackageName = ExtractValue(trimmedLine, "name='", "'") ?? "";
                        if (int.TryParse(ExtractValue(trimmedLine, "versionCode='", "'"), out int versionCode))
                        {
                            metadata.VersionCode = versionCode;
                        }
                        metadata.VersionName = ExtractValue(trimmedLine, "versionName='", "'");
                    }
                    else if (trimmedLine.StartsWith("application-label:"))
                    {
                        metadata.AppName = trimmedLine.Substring("application-label:".Length).Trim('\'', '"');
                    }
                    else if (trimmedLine.StartsWith("sdkVersion:"))
                    {
                        if (int.TryParse(ExtractValue(trimmedLine, "sdkVersion:'", "'"), out int minSdk))
                        {
                            metadata.MinSdkVersion = minSdk;
                        }
                    }
                    else if (trimmedLine.StartsWith("targetSdkVersion:"))
                    {
                        if (int.TryParse(ExtractValue(trimmedLine, "targetSdkVersion:'", "'"), out int targetSdk))
                        {
                            metadata.TargetSdkVersion = targetSdk;
                        }
                    }
                    else if (trimmedLine.StartsWith("uses-permission:"))
                    {
                        var permission = ExtractValue(trimmedLine, "name='", "'");
                        if (!string.IsNullOrEmpty(permission))
                        {
                            metadata.Permissions.Add(permission);
                        }
                    }
                }

                return metadata.IsValid() ? metadata : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse APK metadata from AAPT output");
                return null;
            }
        }

        private IEnumerable<InstalledAndroidApp> ParseInstalledApps(string adbOutput, bool includeSystemApps)
        {
            var apps = new List<InstalledAndroidApp>();

            if (string.IsNullOrWhiteSpace(adbOutput))
                return apps;

            var lines = adbOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("package:"))
                {
                    var packageName = trimmedLine.Substring("package:".Length).Trim();
                    
                    var app = new InstalledAndroidApp
                    {
                        PackageName = packageName,
                        IsSystemApp = IsSystemPackage(packageName),
                        IsEnabled = true
                    };

                    if (!app.IsSystemApp || includeSystemApps)
                    {
                        apps.Add(app);
                    }
                }
            }

            return apps;
        }

        private InstalledAndroidApp? ParseSingleAppInfo(string packageName, string dumpsysOutput)
        {
            try
            {
                var app = new InstalledAndroidApp
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
                        app.VersionName = trimmedLine.Substring("versionName=".Length);
                    }
                    else if (trimmedLine.StartsWith("versionCode="))
                    {
                        if (int.TryParse(trimmedLine.Substring("versionCode=".Length).Split(' ')[0], out int versionCode))
                        {
                            app.VersionCode = versionCode;
                        }
                    }
                }

                return app;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse app info from dumpsys output");
                return null;
            }
        }

        private static string? ExtractValue(string input, string startPattern, string endPattern)
        {
            var startIndex = input.IndexOf(startPattern);
            if (startIndex == -1) return null;
            
            startIndex += startPattern.Length;
            var endIndex = input.IndexOf(endPattern, startIndex);
            if (endIndex == -1) return null;
            
            return input.Substring(startIndex, endIndex - startIndex);
        }

        private static string ParseAdbInstallError(string errorOutput)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                return "Unknown installation error";

            if (errorOutput.Contains("INSTALL_FAILED_ALREADY_EXISTS"))
                return "Application already installed";
            if (errorOutput.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
                return "Insufficient storage space";
            if (errorOutput.Contains("INSTALL_FAILED_INVALID_APK"))
                return "Invalid APK file";
            if (errorOutput.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
                return "Cannot downgrade application version";
            if (errorOutput.Contains("INSTALL_FAILED_PERMISSION_MODEL"))
                return "Permission model incompatibility";

            return errorOutput.Length > 100 ? errorOutput.Substring(0, 100) + "..." : errorOutput;
        }

        private static bool IsSystemPackage(string packageName)
        {
            var systemPrefixes = new[]
            {
                "android.", "com.android.", "com.google.", "com.microsoft.",
                "com.samsung.", "com.qualcomm.", "org.chromium."
            };

            return systemPrefixes.Any(prefix => packageName.StartsWith(prefix));
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate hash for file: {FilePath}", filePath);
                return "";
            }
        }

        /// <summary>
        /// Устанавливает XAPK файл путем распаковки и установки всех APK файлов
        /// </summary>
        private async Task<ApkInstallResult> InstallXapkAsync(string xapkPath)
        {
            string? tempDir = null;
            try
            {
                // Создаем временную директорию для распаковки XAPK
                tempDir = Path.Combine(Path.GetTempPath(), $"xapk_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                _logger.LogDebug("Extracting XAPK to temporary directory: {TempDir}", tempDir);

                // Распаковываем XAPK (это ZIP архив)
                ZipFile.ExtractToDirectory(xapkPath, tempDir);

                // Ищем манифест XAPK
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    return ApkInstallResult.CreateFailure("Invalid XAPK: manifest.json not found");
                }

                // Читаем метаданные из манифеста
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var xapkMetadata = JsonSerializer.Deserialize<XapkMetadata>(manifestJson, options);

                if (xapkMetadata == null || !xapkMetadata.IsValid())
                {
                    return ApkInstallResult.CreateFailure("Invalid XAPK manifest");
                }

                _logger.LogInformation("Installing XAPK: {PackageName} from {XapkPath}", 
                    xapkMetadata.PackageName, xapkPath);

                // Ищем основной APK файл (обычно называется base.apk или имеет имя пакета)
                var apkFiles = Directory.GetFiles(tempDir, "*.apk");
                if (apkFiles.Length == 0)
                {
                    return ApkInstallResult.CreateFailure("No APK files found in XAPK");
                }

                // Если есть split APK файлы, используем пакетную установку
                if (apkFiles.Length > 1)
                {
                    return await InstallMultipleApksAsync(apkFiles, xapkMetadata);
                }
                else
                {
                    // Один APK файл - обычная установка
                    var singleApkPath = apkFiles[0];
                    _logger.LogDebug("Installing single APK from XAPK: {ApkPath}", singleApkPath);
                    
                    var result = await InstallSingleApkWithFallbackAsync(singleApkPath);
                    
                    if (result.Success)
                    {
                        // Конвертируем XAPK метаданные для результата
                        var apkMetadata = xapkMetadata.ToApkMetadata();
                        apkMetadata.FileSizeBytes = new FileInfo(xapkPath).Length;
                        return ApkInstallResult.CreateSuccess(apkMetadata.PackageName, apkMetadata.FileSizeBytes);
                    }
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing XAPK: {XapkPath}", xapkPath);
                return ApkInstallResult.CreateFailure($"XAPK installation exception: {ex.Message}");
            }
            finally
            {
                // Очищаем временную директорию
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        _logger.LogDebug("Cleaned up temporary XAPK directory: {TempDir}", tempDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary XAPK directory: {TempDir}", tempDir);
                    }
                }
            }
        }

        /// <summary>
        /// Устанавливает несколько APK файлов из XAPK пакета
        /// </summary>
        private async Task<ApkInstallResult> InstallMultipleApksAsync(string[] apkFiles, XapkMetadata xapkMetadata)
        {
            try
            {
                _logger.LogInformation("Installing {Count} APK files from XAPK package: {PackageName}", 
                    apkFiles.Length, xapkMetadata.PackageName);

                // Используем adb install-multiple для установки всех APK файлов одновременно
                var apkPathsQuoted = string.Join(" ", apkFiles.Select(path => $"\"{path}\""));
                var installCommand = $"install-multiple -r {apkPathsQuoted}";

                var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", installCommand, 120000); // 2 минуты

                if (result.IsSuccess && result.StandardOutput.Contains("Success"))
                {
                    _logger.LogInformation("Successfully installed XAPK with multiple APKs: {PackageName}", xapkMetadata.PackageName);
                    return ApkInstallResult.CreateSuccess(xapkMetadata.PackageName ?? "", 0);
                }
                else
                {
                    // Если пакетная установка не удалась, попробуем установить APK файлы по одному
                    _logger.LogWarning("Multiple APK install failed, trying individual installation for {PackageName}", xapkMetadata.PackageName);

                    foreach (var apkFile in apkFiles)
                    {
                        _logger.LogDebug("Installing individual APK: {ApkFile}", Path.GetFileName(apkFile));
                        var individualResult = await InstallSingleApkWithFallbackAsync(apkFile);
                        
                        if (!individualResult.Success)
                        {
                            _logger.LogError("Failed to install APK: {ApkFile}. Error: {Error}", 
                                Path.GetFileName(apkFile), individualResult.ErrorMessage);
                            return individualResult;
                        }
                    }

                    _logger.LogInformation("Successfully installed all APK files individually for XAPK: {PackageName}", xapkMetadata.PackageName);
                    return ApkInstallResult.CreateSuccess(xapkMetadata.PackageName ?? "", 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing multiple APKs from XAPK");
                return ApkInstallResult.CreateFailure($"Multiple APK installation exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Устанавливает один APK файл с различными методами fallback
        /// </summary>
        private async Task<ApkInstallResult> InstallSingleApkWithFallbackAsync(string apkPath)
        {
            // Сначала пробуем обычную установку
            var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install \"{apkPath}\"", 60000);
            
            if (result.IsSuccess && result.StandardOutput.Contains("Success"))
            {
                return ApkInstallResult.CreateSuccess("", 0);
            }
            
            // Проверяем, есть ли ошибка MISSING_SPLIT
            bool isMissingSplitError = (result.StandardError + " " + result.StandardOutput)
                .Contains("INSTALL_FAILED_MISSING_SPLIT", StringComparison.OrdinalIgnoreCase);
                
            if (isMissingSplitError)
            {
                _logger.LogDebug("Standard install failed with MISSING_SPLIT, trying fallback methods");
                
                // Пробуем установку с флагами
                var fallbackResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install -t -r \"{apkPath}\"", 60000);
                
                if (fallbackResult.IsSuccess && fallbackResult.StandardOutput.Contains("Success"))
                {
                    return ApkInstallResult.CreateSuccess("", 0);
                }
                
                // Финальная попытка с полными разрешениями
                var finalResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", $"install -g -t -r \"{apkPath}\"", 60000);
                
                if (finalResult.IsSuccess && finalResult.StandardOutput.Contains("Success"))
                {
                    return ApkInstallResult.CreateSuccess("", 0);
                }
                
                return ApkInstallResult.CreateFailure($"All installation methods failed: {finalResult.StandardError}");
            }
            
            return ApkInstallResult.CreateFailure($"Installation failed: {result.StandardError}");
        }

        /// <summary>
        /// Валидирует XAPK файл проверяя его структуру как ZIP архива
        /// </summary>
        private async Task<bool> ValidateXapkFileAsync(string xapkPath)
        {
            try
            {
                _logger.LogDebug("Validating XAPK file: {XapkPath}", xapkPath);

                // Проверяем что файл можно открыть как ZIP архив
                using var archive = ZipFile.OpenRead(xapkPath);
                
                // Проверяем наличие manifest.json
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("XAPK validation failed: manifest.json not found in {XapkPath}", xapkPath);
                    return false;
                }

                // Проверяем наличие хотя бы одного APK файла
                bool hasApkFiles = archive.Entries.Any(entry => entry.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
                if (!hasApkFiles)
                {
                    _logger.LogWarning("XAPK validation failed: no APK files found in {XapkPath}", xapkPath);
                    return false;
                }

                _logger.LogDebug("XAPK validation successful: {XapkPath}", xapkPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while validating XAPK: {XapkPath}", xapkPath);
                return false;
            }
        }

        /// <summary>
        /// Извлекает метаданные из XAPK файла
        /// </summary>
        private async Task<ApkMetadata?> ExtractXapkMetadataAsync(string xapkPath)
        {
            try
            {
                _logger.LogDebug("Extracting XAPK metadata: {XapkPath}", xapkPath);

                using var archive = ZipFile.OpenRead(xapkPath);
                
                // Читаем manifest.json
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("manifest.json not found in XAPK: {XapkPath}", xapkPath);
                    return null;
                }

                using var manifestStream = manifestEntry.Open();
                using var reader = new StreamReader(manifestStream);
                var manifestJson = await reader.ReadToEndAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var xapkMetadata = JsonSerializer.Deserialize<XapkMetadata>(manifestJson, options);
                if (xapkMetadata == null || !xapkMetadata.IsValid())
                {
                    _logger.LogWarning("Invalid XAPK manifest in: {XapkPath}", xapkPath);
                    return null;
                }

                // Конвертируем XAPK метаданные в APK метаданные
                var apkMetadata = xapkMetadata.ToApkMetadata();

                // Добавляем информацию о файле
                var fileInfo = new FileInfo(xapkPath);
                apkMetadata.FileSizeBytes = fileInfo.Length;
                apkMetadata.LastModified = fileInfo.LastWriteTime;

                _logger.LogInformation("Successfully extracted XAPK metadata: {PackageName} v{Version} ({SizeMB:F1} MB)",
                    apkMetadata.PackageName, apkMetadata.GetVersionString(), 
                    apkMetadata.FileSizeBytes / (1024.0 * 1024.0));

                return apkMetadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while extracting XAPK metadata: {XapkPath}", xapkPath);
                return null;
            }
        }

        #endregion
    }
}
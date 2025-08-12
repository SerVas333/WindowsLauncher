using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис для управления подключениями к Windows Subsystem for Android (WSA)
    /// Реализует retry механизмы, кэширование и event-based уведомления
    /// </summary>
    public class WSAConnectionService : IWSAConnectionService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<WSAConnectionService> _logger;
        private readonly ConcurrentDictionary<string, CacheItem<object>> _cache;
        private readonly Timer _statusCheckTimer;

        // Кэшированные значения для производительности
        private bool? _wsaAvailable;
        private bool? _adbAvailable;
        private string? _adbPath;

        public event EventHandler<WSAConnectionStatusEventArgs>? ConnectionStatusChanged;

        public WSAConnectionService(IProcessExecutor processExecutor, ILogger<WSAConnectionService> logger)
        {
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new ConcurrentDictionary<string, CacheItem<object>>();

            // Периодическая проверка статуса подключения (каждые 60 секунд)
            _statusCheckTimer = new Timer(async _ => await CheckConnectionStatusAsync(), 
                null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogDebug("WSAConnectionService initialized");
        }

        public async Task<bool> IsWSAAvailableAsync()
        {
            return await GetCachedValueAsync("wsa_available", 
                async () =>
                {
                    try
                    {
                        // Проверка через PowerShell - наличие WSA пакета
                        var result = await _processExecutor.ExecutePowerShellAsync(
                            "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 10000);

                        bool available = result.IsSuccess && !string.IsNullOrEmpty(result.StandardOutput);
                        
                        _logger.LogDebug("WSA availability check: {Available}", available);
                        
                        if (available)
                        {
                            _logger.LogInformation("Windows Subsystem for Android is available");
                        }
                        else
                        {
                            _logger.LogWarning("Windows Subsystem for Android is not installed");
                        }

                        return available;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to check WSA availability");
                        return false;
                    }
                }, 
                TimeSpan.FromMinutes(5)); // Кэшируем на 5 минут
        }

        public async Task<bool> IsWSARunningAsync()
        {
            return await GetCachedValueAsync("wsa_running", 
                async () =>
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
                }, 
                TimeSpan.FromSeconds(30)); // Кэшируем на 30 секунд
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

                // Инвалидируем кэш статуса
                _cache.TryRemove("wsa_running", out _);

                // Retry механизм для запуска WSA
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    _logger.LogDebug("WSA start attempt {Attempt}/{MaxAttempts}", attempt, maxRetries);

                    // Попытка запустить WSA через PowerShell
                    var result = await _processExecutor.ExecutePowerShellAsync(
                        "Start-Process -FilePath 'wsa://system' -WindowStyle Hidden", 15000);

                    if (result.IsSuccess)
                    {
                        // Ждем инициализации
                        await Task.Delay(TimeSpan.FromSeconds(3 + attempt)); // Увеличиваем время ожидания с каждой попыткой
                        
                        bool started = await IsWSARunningAsync();
                        if (started)
                        {
                            _logger.LogInformation("WSA started successfully on attempt {Attempt}", attempt);
                            OnConnectionStatusChanged(true, "WSA Started");
                            return true;
                        }
                    }

                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("WSA start attempt {Attempt} failed, retrying in {Delay}s", 
                            attempt, 2 * attempt);
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt)); // Exponential backoff
                    }
                }

                _logger.LogWarning("Failed to start WSA via PowerShell, trying alternative method");

                // Альтернативный метод через WsaClient
                var wsaClientResult = await _processExecutor.ExecuteAsync("WsaClient", "/launch wsa://system", 15000);
                
                if (wsaClientResult.IsSuccess)
                {
                    await Task.Delay(3000);
                    bool alternativeStarted = await IsWSARunningAsync();
                    if (alternativeStarted)
                    {
                        _logger.LogInformation("WSA started successfully using alternative method");
                        OnConnectionStatusChanged(true, "WSA Started (Alternative)");
                        return true;
                    }
                }

                _logger.LogError("Failed to start WSA using all available methods");
                OnConnectionStatusChanged(false, "Failed to Start WSA");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while starting WSA");
                OnConnectionStatusChanged(false, $"WSA Start Error: {ex.Message}");
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

                // Инвалидируем кэш статуса
                _cache.TryRemove("wsa_running", out _);

                var result = await _processExecutor.ExecutePowerShellAsync(
                    "Stop-Process -Name 'WsaClient' -Force -ErrorAction SilentlyContinue", 10000);

                // Ждем завершения процесса
                await Task.Delay(2000);
                
                bool stopped = !await IsWSARunningAsync();
                if (stopped)
                {
                    _logger.LogInformation("WSA stopped successfully");
                    OnConnectionStatusChanged(false, "WSA Stopped");
                }
                else
                {
                    _logger.LogWarning("WSA may not have stopped completely");
                    OnConnectionStatusChanged(false, "WSA Stop Uncertain");
                }

                return stopped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while stopping WSA");
                OnConnectionStatusChanged(false, $"WSA Stop Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsAdbAvailableAsync()
        {
            return await GetCachedValueAsync("adb_available", 
                async () =>
                {
                    try
                    {
                        // Сначала ищем ADB в PATH
                        _adbPath = await _processExecutor.GetCommandPathAsync("adb");
                        
                        if (string.IsNullOrEmpty(_adbPath))
                        {
                            _logger.LogDebug("ADB command not found in PATH, searching in standard locations");
                            
                            // Ищем ADB в стандартных локациях
                            await EnsureAdbAvailableAsync();
                        }

                        // Если ADB найден, проверяем его работоспособность
                        if (!string.IsNullOrEmpty(_adbPath))
                        {
                            var result = await _processExecutor.ExecuteAsync(_adbPath, "version", 5000);
                            
                            bool available = result.IsSuccess && result.StandardOutput.Contains("Android Debug Bridge");
                            
                            if (available)
                            {
                                _logger.LogInformation("ADB is available at: {AdbPath}", _adbPath);
                            }
                            else
                            {
                                _logger.LogWarning("ADB command found but not working properly");
                            }

                            return available;
                        }
                        else
                        {
                            _logger.LogWarning("ADB not found in PATH or standard locations");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to check ADB availability");
                        return false;
                    }
                }, 
                TimeSpan.FromMinutes(10)); // Кэшируем на 10 минут
        }

        public async Task<bool> ConnectToWSAAsync()
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                try
                {
                    if (!await IsAdbAvailableAsync())
                    {
                        _logger.LogError("ADB is not available, cannot connect to WSA");
                        return false;
                    }

                    _logger.LogDebug("Connecting to WSA via ADB");

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
                        OnConnectionStatusChanged(true, "ADB Connected");
                    }
                    else
                    {
                        _logger.LogWarning("ADB connection established but device not ready");
                        OnConnectionStatusChanged(false, "ADB Connection Incomplete");
                    }

                    return connected;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception occurred while connecting to WSA");
                    return false;
                }
            }, maxRetries: 3, baseDelay: TimeSpan.FromSeconds(2));
        }

        public async Task<string?> GetAndroidVersionAsync()
        {
            return await GetCachedValueAsync("android_version", 
                async () =>
                {
                    try
                    {
                        if (!await ConnectToWSAAsync())
                        {
                            return null;
                        }

                        var result = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", 
                            "shell getprop ro.build.version.release", 5000);

                        string? version = result.IsSuccess ? result.StandardOutput.Trim() : null;
                        
                        if (!string.IsNullOrEmpty(version))
                        {
                            _logger.LogInformation("Android version in WSA: {Version}", version);
                        }

                        return version;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception occurred while getting Android version");
                        return null;
                    }
                }, 
                TimeSpan.FromMinutes(30)); // Версия Android не меняется часто
        }

        public async Task<Dictionary<string, object>> GetConnectionStatusAsync()
        {
            var status = new Dictionary<string, object>();

            try
            {
                status["WSAAvailable"] = await IsWSAAvailableAsync();
                status["WSARunning"] = await IsWSARunningAsync();
                status["ADBAvailable"] = await IsAdbAvailableAsync();
                status["ADBPath"] = _adbPath ?? "Not Found";
                status["AndroidVersion"] = await GetAndroidVersionAsync() ?? "Unknown";
                status["LastStatusCheck"] = DateTime.Now;

                // Проверяем ADB подключение
                if ((bool)status["WSARunning"] && (bool)status["ADBAvailable"])
                {
                    var devicesResult = await _processExecutor.ExecuteAsync(_adbPath ?? "adb", "devices", 5000);
                    status["ADBConnected"] = devicesResult.IsSuccess && 
                                           devicesResult.StandardOutput.Contains("127.0.0.1:58526");
                }
                else
                {
                    status["ADBConnected"] = false;
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting connection status");
                status["Error"] = ex.Message;
                return status;
            }
        }

        #region Private Methods

        private async Task<T> GetCachedValueAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                return (T)cached.Value;
            }

            var newValue = await factory();
            _cache[key] = new CacheItem<object>(newValue!, DateTime.UtcNow.Add(ttl));
            return newValue;
        }

        private async Task<bool> ExecuteWithRetryAsync(Func<Task<bool>> operation, int maxRetries = 3, TimeSpan? baseDelay = null)
        {
            var delay = baseDelay ?? TimeSpan.FromSeconds(1);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();
                    if (result)
                    {
                        return true;
                    }

                    if (attempt < maxRetries)
                    {
                        var currentDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                        _logger.LogWarning("Operation attempt {Attempt} failed, retrying in {Delay}ms", 
                            attempt, currentDelay.TotalMilliseconds);
                        await Task.Delay(currentDelay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception during retry attempt {Attempt}", attempt);
                    
                    if (attempt == maxRetries)
                    {
                        throw;
                    }
                }
            }

            return false;
        }

        private async Task EnsureAdbAvailableAsync()
        {
            if (!string.IsNullOrEmpty(_adbPath))
            {
                return;
            }

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
                @"C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe",
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

        private async Task CheckConnectionStatusAsync()
        {
            try
            {
                var status = await GetConnectionStatusAsync();
                var isConnected = (bool)(status.GetValueOrDefault("WSARunning", false)) && 
                                (bool)(status.GetValueOrDefault("ADBConnected", false));
                
                OnConnectionStatusChanged(isConnected, isConnected ? "Connected" : "Disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during periodic connection status check");
            }
        }

        private void OnConnectionStatusChanged(bool isConnected, string status)
        {
            try
            {
                ConnectionStatusChanged?.Invoke(this, new WSAConnectionStatusEventArgs
                {
                    IsConnected = isConnected,
                    Status = status,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error firing ConnectionStatusChanged event");
            }
        }

        #endregion

        public void Dispose()
        {
            _statusCheckTimer?.Dispose();
            _cache.Clear();
        }
    }

    /// <summary>
    /// Элемент кэша с временем истечения
    /// </summary>
    internal class CacheItem<T>
    {
        public T Value { get; }
        public DateTime ExpiresAt { get; }

        public CacheItem(T value, DateTime expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}
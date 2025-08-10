using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Configuration;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис управления жизненным циклом Android подсистемы (WSA)
    /// </summary>
    public class AndroidSubsystemService : IAndroidSubsystemService, IHostedService
    {
        private readonly ILogger<AndroidSubsystemService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWSAIntegrationService _wsaService;
        private readonly AndroidSubsystemConfiguration _config;
        private readonly Timer? _idleTimer;
        private DateTime _lastActivity = DateTime.Now;
        private bool _isPreloading = false;
        
        public AndroidMode CurrentMode => _config.Mode;
        public string WSAStatus { get; private set; } = "Unknown";

        public event EventHandler<string>? StatusChanged;

        public AndroidSubsystemService(
            ILogger<AndroidSubsystemService> logger,
            IConfiguration configuration,
            IWSAIntegrationService wsaService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _wsaService = wsaService ?? throw new ArgumentNullException(nameof(wsaService));

            // Загрузка конфигурации из appsettings.json
            _config = new AndroidSubsystemConfiguration();
            _configuration.GetSection("AndroidSubsystem").Bind(_config);

            _logger.LogInformation("Android subsystem configured in {Mode} mode", _config.Mode);

            // Настройка таймера для отслеживания простоя WSA
            if (_config.ResourceOptimization.StopWSAOnIdle)
            {
                _idleTimer = new Timer(CheckIdleTimeoutCallback, null, 
                    TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            }
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing Android subsystem in {Mode} mode", _config.Mode);

            try
            {
                switch (_config.Mode)
                {
                    case AndroidMode.Disabled:
                        WSAStatus = "Disabled";
                        _logger.LogInformation("Android subsystem is disabled");
                        break;

                    case AndroidMode.OnDemand:
                        WSAStatus = await _wsaService.IsWSAAvailableAsync() ? "Available" : "Unavailable";
                        _logger.LogInformation("Android subsystem configured for on-demand usage, WSA: {Status}", WSAStatus);
                        break;

                    case AndroidMode.Preload:
                        WSAStatus = "Initializing";
                        _logger.LogInformation("Android subsystem configured for preload mode, starting delayed initialization");
                        
                        // Запускаем предзагрузку в фоне с задержкой
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(_config.PreloadDelaySeconds));
                            await PreloadWSAAsync();
                        });
                        break;
                }

                OnStatusChanged(WSAStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Android subsystem");
                WSAStatus = "Error";
                OnStatusChanged(WSAStatus);
            }
        }

        public async Task<bool> IsAndroidAvailableAsync()
        {
            switch (_config.Mode)
            {
                case AndroidMode.Disabled:
                    return false;

                case AndroidMode.OnDemand:
                    return await _wsaService.IsWSAAvailableAsync();

                case AndroidMode.Preload:
                    return await _wsaService.IsWSAAvailableAsync() && await _wsaService.IsWSARunningAsync();

                default:
                    return false;
            }
        }

        public async Task<bool> PreloadWSAAsync()
        {
            if (_config.Mode != AndroidMode.Preload || _isPreloading)
            {
                return false;
            }

            _isPreloading = true;
            _logger.LogInformation("Starting WSA preload process");

            try
            {
                WSAStatus = "Starting";
                OnStatusChanged(WSAStatus);

                // Проверяем доступность WSA
                if (!await _wsaService.IsWSAAvailableAsync())
                {
                    WSAStatus = "Unavailable";
                    _logger.LogWarning("WSA is not available for preloading");
                    OnStatusChanged(WSAStatus);
                    return false;
                }

                // Проверяем доступную память
                if (_config.Fallback.DisableOnLowMemory && !HasSufficientMemory())
                {
                    WSAStatus = "Insufficient Memory";
                    _logger.LogWarning("Insufficient memory for WSA preload, available memory below {Threshold}MB", 
                        _config.Fallback.MemoryThresholdMB);
                    OnStatusChanged(WSAStatus);
                    return false;
                }

                // Запускаем WSA если он не запущен
                bool isRunning = await _wsaService.IsWSARunningAsync();
                if (!isRunning && _config.AutoStartWSA)
                {
                    _logger.LogInformation("Starting WSA for preload mode");
                    isRunning = await _wsaService.StartWSAAsync();
                }

                if (isRunning)
                {
                    WSAStatus = "Ready";
                    _logger.LogInformation("WSA preload completed successfully");
                }
                else
                {
                    WSAStatus = "Failed to Start";
                    _logger.LogError("Failed to start WSA for preload mode");
                }

                OnStatusChanged(WSAStatus);
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during WSA preload");
                WSAStatus = "Error";
                OnStatusChanged(WSAStatus);
                return false;
            }
            finally
            {
                _isPreloading = false;
            }
        }

        public async Task<bool> StopWSAAsync()
        {
            if (_config.Mode == AndroidMode.Disabled)
            {
                return true;
            }

            try
            {
                _logger.LogInformation("Stopping WSA");
                WSAStatus = "Stopping";
                OnStatusChanged(WSAStatus);

                bool stopped = await _wsaService.StopWSAAsync();
                
                if (stopped)
                {
                    WSAStatus = "Stopped";
                    _logger.LogInformation("WSA stopped successfully");
                }
                else
                {
                    WSAStatus = "Failed to Stop";
                    _logger.LogWarning("Failed to stop WSA");
                }

                OnStatusChanged(WSAStatus);
                return stopped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while stopping WSA");
                WSAStatus = "Error";
                OnStatusChanged(WSAStatus);
                return false;
            }
        }

        public async Task OptimizeResourceUsageAsync()
        {
            if (_config.Mode == AndroidMode.Disabled)
            {
                return;
            }

            try
            {
                _logger.LogDebug("Optimizing Android subsystem resource usage");

                // Проверяем потребление памяти
                if (_config.Fallback.DisableOnLowMemory && !HasSufficientMemory())
                {
                    _logger.LogWarning("Low memory detected, temporarily disabling Android functions");
                    WSAStatus = "Suspended (Low Memory)";
                    OnStatusChanged(WSAStatus);
                    
                    if (await _wsaService.IsWSARunningAsync())
                    {
                        await StopWSAAsync();
                    }
                }

                // Проверяем простой WSA
                if (_config.ResourceOptimization.StopWSAOnIdle && 
                    (DateTime.Now - _lastActivity).TotalMinutes > _config.ResourceOptimization.IdleTimeoutMinutes)
                {
                    if (await _wsaService.IsWSARunningAsync())
                    {
                        _logger.LogInformation("WSA idle timeout reached, stopping WSA");
                        await StopWSAAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during resource optimization");
            }
        }

        public async Task<Dictionary<string, object>> GetDetailedStatusAsync()
        {
            var status = new Dictionary<string, object>
            {
                ["Mode"] = _config.Mode.ToString(),
                ["Status"] = WSAStatus,
                ["ConfiguredMode"] = _config.Mode,
                ["AutoStartEnabled"] = _config.AutoStartWSA,
                ["DiagnosticsEnabled"] = _config.EnableDiagnostics,
                ["LastActivity"] = _lastActivity,
                ["IsPreloading"] = _isPreloading
            };

            try
            {
                if (_config.Mode != AndroidMode.Disabled)
                {
                    status["WSAAvailable"] = await _wsaService.IsWSAAvailableAsync();
                    status["WSARunning"] = await _wsaService.IsWSARunningAsync();
                    
                    if (_config.EnableDiagnostics)
                    {
                        var wsaStatus = await _wsaService.GetWSAStatusAsync();
                        foreach (var kvp in wsaStatus)
                        {
                            status[$"WSA_{kvp.Key}"] = kvp.Value;
                        }
                    }
                }

                status["AvailableMemoryMB"] = GetAvailableMemoryMB();
                status["MemoryThresholdMB"] = _config.Fallback.MemoryThresholdMB;
                status["HasSufficientMemory"] = HasSufficientMemory();
            }
            catch (Exception ex)
            {
                status["Error"] = ex.Message;
                _logger.LogError(ex, "Exception occurred while collecting detailed status");
            }

            return status;
        }

        // Методы IHostedService
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Android Subsystem Service");
            await InitializeAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Android Subsystem Service");

            _idleTimer?.Dispose();

            if (_config.Mode == AndroidMode.Preload && await _wsaService.IsWSARunningAsync())
            {
                _logger.LogInformation("Gracefully stopping WSA on service shutdown");
                await StopWSAAsync();
            }
        }

        // Приватные методы

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void CheckIdleTimeoutCallback(object? state)
        {
            _ = Task.Run(OptimizeResourceUsageAsync);
        }

        private bool HasSufficientMemory()
        {
            try
            {
                return GetAvailableMemoryMB() >= _config.Fallback.MemoryThresholdMB;
            }
            catch
            {
                return true; // Если не можем определить, считаем что памяти достаточно
            }
        }

        private long GetAvailableMemoryMB()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "OS get TotalVisibleMemorySize /value",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Парсим вывод WMIC для получения общей памяти
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("TotalVisibleMemorySize="))
                    {
                        var memoryKB = long.Parse(line.Split('=')[1].Trim());
                        return memoryKB / 1024; // Конвертируем в MB
                    }
                }

                return 8192; // Fallback значение 8GB
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get available memory, using fallback value");
                return 8192; // Fallback значение 8GB
            }
        }

        public void MarkActivity()
        {
            _lastActivity = DateTime.Now;
        }
    }
}
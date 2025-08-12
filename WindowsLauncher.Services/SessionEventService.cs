using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Services;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для отслеживания системных событий смены сессии и автоматического закрытия приложений
    /// с поддержкой Shell режима
    /// </summary>
    public class SessionEventService : IDisposable
    {
        private readonly ILogger<SessionEventService> _logger;
        private readonly IApplicationLifecycleService _lifecycleService;
        private readonly ShellModeDetectionService _shellModeDetectionService;
        private bool _disposed = false;
        private bool _eventsRegistered = false;
        private ShellMode _currentShellMode = ShellMode.Normal;
        private bool _shellModeInitialized = false;

        public SessionEventService(
            ILogger<SessionEventService> logger,
            IApplicationLifecycleService lifecycleService,
            ShellModeDetectionService shellModeDetectionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _shellModeDetectionService = shellModeDetectionService ?? throw new ArgumentNullException(nameof(shellModeDetectionService));
        }

        /// <summary>
        /// Начать отслеживание системных событий сессии
        /// </summary>
        public void StartMonitoring()
        {
            try
            {
                if (_eventsRegistered) return;

                _logger.LogInformation("Starting Windows session event monitoring");

                // Инициализируем режим Shell при первом запуске
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeShellModeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing shell mode");
                    }
                });

                // Регистрируем обработчики системных событий Windows
                SystemEvents.SessionEnding += OnSessionEnding;
                SystemEvents.SessionEnded += OnSessionEnded;
                SystemEvents.SessionSwitch += OnSessionSwitch;
                
                _eventsRegistered = true;
                _logger.LogInformation("Windows session event monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start session event monitoring");
            }
        }

        /// <summary>
        /// Инициализировать определение режима Shell
        /// </summary>
        private async Task InitializeShellModeAsync()
        {
            try
            {
                if (_shellModeInitialized) return;

                _currentShellMode = await _shellModeDetectionService.DetectShellModeAsync();
                var modeDescription = _shellModeDetectionService.GetModeDescription(_currentShellMode);
                
                _logger.LogInformation("SessionEventService initialized in: {ModeDescription}", modeDescription);
                _shellModeInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize shell mode detection, defaulting to Normal mode");
                _currentShellMode = ShellMode.Normal;
                _shellModeInitialized = true;
            }
        }

        /// <summary>
        /// Остановить отслеживание системных событий сессии
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                if (!_eventsRegistered) return;

                _logger.LogInformation("Stopping Windows session event monitoring");

                // Отписываемся от системных событий
                SystemEvents.SessionEnding -= OnSessionEnding;
                SystemEvents.SessionEnded -= OnSessionEnded;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                
                _eventsRegistered = false;
                _logger.LogInformation("Windows session event monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping session event monitoring");
            }
        }

        /// <summary>
        /// Обработчик события завершения сессии (перед закрытием)
        /// </summary>
        private async void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            try
            {
                var reasonText = e.Reason switch
                {
                    SessionEndReasons.Logoff => "User logoff",
                    SessionEndReasons.SystemShutdown => "System shutdown",
                    _ => $"Unknown reason ({e.Reason})"
                };

                // Убеждаемся что режим Shell инициализирован
                if (!_shellModeInitialized)
                {
                    await InitializeShellModeAsync();
                }

                var modeDescription = _shellModeDetectionService.GetModeDescription(_currentShellMode);
                _logger.LogWarning("Windows session ending: {Reason} in {Mode}", reasonText, modeDescription);

                // ВАЖНО: Разное поведение в зависимости от режима
                if (_currentShellMode == ShellMode.Shell && e.Reason == SessionEndReasons.Logoff)
                {
                    // В Shell режиме при logoff пользователя:
                    // 1. Закрываем только приложения пользователя
                    // 2. НЕ завершаем лаунчер - он остается работать как shell
                    // 3. Логику показа LoginWindow оставляем App.xaml.cs HandleMainWindowClosedAsync
                    
                    _logger.LogInformation("Shell mode logoff: closing user applications but keeping launcher running");
                    
                    var shutdownResult = await _lifecycleService.ShutdownAllAsync(
                        gracefulTimeoutMs: 3000, 
                        finalTimeoutMs: 1000);

                    _logger.LogInformation("Shell mode user applications shutdown: {Success}, {Total} apps, {Duration}ms", 
                        shutdownResult.Success, 
                        shutdownResult.TotalApplications,
                        (int)shutdownResult.Duration.TotalMilliseconds);
                        
                    // НЕ отменяем системное событие, но лаунчер должен остаться работать
                    // App.xaml.cs HandleMainWindowClosedAsync обработает показ LoginWindow
                }
                else
                {
                    // Normal режим или system shutdown: закрываем все приложения как обычно
                    _logger.LogWarning("Standard mode or system shutdown: closing all applications");
                    
                    var shutdownResult = await _lifecycleService.ShutdownAllAsync(
                        gracefulTimeoutMs: 2000, 
                        finalTimeoutMs: 500);

                    _logger.LogWarning("Emergency application shutdown completed: {Success}, {Total} apps, {Duration}ms", 
                        shutdownResult.Success, 
                        shutdownResult.TotalApplications,
                        (int)shutdownResult.Duration.TotalMilliseconds);
                }

                // НЕ отменяем событие - позволяем системе продолжить завершение работы
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session ending handling");
            }
        }

        /// <summary>
        /// Обработчик события завершения сессии (после закрытия)
        /// </summary>
        private void OnSessionEnded(object? sender, SessionEndedEventArgs e)
        {
            try
            {
                var reasonText = e.Reason switch
                {
                    SessionEndReasons.Logoff => "User logoff",
                    SessionEndReasons.SystemShutdown => "System shutdown", 
                    _ => $"Unknown reason ({e.Reason})"
                };

                _logger.LogInformation("Windows session ended: {Reason}", reasonText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session ended handling");
            }
        }

        /// <summary>
        /// Обработчик событий переключения сессии (смена пользователя, блокировка/разблокировка)
        /// </summary>
        private async void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            try
            {
                var reasonText = e.Reason switch
                {
                    SessionSwitchReason.ConsoleConnect => "Console connect",
                    SessionSwitchReason.ConsoleDisconnect => "Console disconnect",
                    SessionSwitchReason.RemoteConnect => "Remote connect",
                    SessionSwitchReason.RemoteDisconnect => "Remote disconnect",
                    SessionSwitchReason.SessionLogon => "Session logon",
                    SessionSwitchReason.SessionLogoff => "Session logoff",
                    SessionSwitchReason.SessionLock => "Session lock",
                    SessionSwitchReason.SessionUnlock => "Session unlock",
                    SessionSwitchReason.SessionRemoteControl => "Session remote control",
                    _ => $"Unknown switch ({e.Reason})"
                };

                // Убеждаемся что режим Shell инициализирован
                if (!_shellModeInitialized)
                {
                    await InitializeShellModeAsync();
                }

                var modeDescription = _shellModeDetectionService.GetModeDescription(_currentShellMode);
                _logger.LogInformation("Windows session switch: {Reason} in {Mode}", reasonText, modeDescription);

                // Закрываем приложения при определенных событиях
                bool shouldCloseApplications = e.Reason switch
                {
                    SessionSwitchReason.SessionLogoff => true,     // Выход пользователя
                    SessionSwitchReason.ConsoleDisconnect => true, // Отключение консоли
                    SessionSwitchReason.RemoteDisconnect => true,  // Отключение удаленного подключения
                    _ => false
                };

                if (shouldCloseApplications)
                {
                    if (_currentShellMode == ShellMode.Shell && e.Reason == SessionSwitchReason.SessionLogoff)
                    {
                        // В Shell режиме при logoff: закрываем приложения, но лаунчер остается
                        _logger.LogInformation("Shell mode session switch: closing user applications but keeping launcher");
                    }
                    else
                    {
                        // Normal режим: стандартное поведение
                        _logger.LogWarning("Closing applications due to session switch: {Reason}", reasonText);
                    }
                    
                    var shutdownResult = await _lifecycleService.ShutdownAllAsync(
                        gracefulTimeoutMs: 3000, 
                        finalTimeoutMs: 1000);

                    _logger.LogInformation("Session switch application shutdown: {Success}, {Total} apps", 
                        shutdownResult.Success, shutdownResult.TotalApplications);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session switch handling");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                _disposed = true;
            }
        }
    }
}
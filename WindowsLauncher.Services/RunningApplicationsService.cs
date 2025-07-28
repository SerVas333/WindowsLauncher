using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using Timer = System.Timers.Timer;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления запущенными из лаунчера приложениями
    /// </summary>
    public class RunningApplicationsService : IRunningApplicationsService, IDisposable
    {
        private readonly ILogger<RunningApplicationsService> _logger;
        private readonly ConcurrentDictionary<int, RunningApplication> _runningApplications;
        private readonly Timer _monitoringTimer;
        private readonly SemaphoreSlim _semaphore;
        private bool _isMonitoring;
        private bool _disposed;

        #region Windows API

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOW = 5;
        private const uint WM_CLOSE = 0x0010;

        #endregion

        public event EventHandler<RunningApplicationEventArgs>? ApplicationStarted;
        public event EventHandler<RunningApplicationEventArgs>? ApplicationExited;
        public event EventHandler<RunningApplicationEventArgs>? ApplicationStatusChanged;

        public RunningApplicationsService(ILogger<RunningApplicationsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runningApplications = new ConcurrentDictionary<int, RunningApplication>();
            _semaphore = new SemaphoreSlim(1, 1);
            
            // Таймер для мониторинга процессов каждые 5 секунд
            _monitoringTimer = new Timer(5000);
            _monitoringTimer.Elapsed += OnMonitoringTimer;
            _monitoringTimer.AutoReset = true;
        }

        /// <summary>
        /// Зарегистрировать запущенное приложение
        /// </summary>
        public async Task RegisterApplicationAsync(Application application, Process process, string launchedBy)
        {
            await _semaphore.WaitAsync();
            try
            {
                var runningApp = RunningApplication.FromApplication(application, process, launchedBy);
                
                if (_runningApplications.TryAdd(process.Id, runningApp))
                {
                    _logger.LogInformation("Registered running application: {Name} (PID: {ProcessId})", 
                        application.Name, process.Id);

                    // Запускаем мониторинг если еще не запущен
                    if (!_isMonitoring)
                    {
                        await StartMonitoringAsync();
                    }

                    // Уведомляем о запуске
                    ApplicationStarted?.Invoke(this, new RunningApplicationEventArgs
                    {
                        Application = runningApp,
                        Action = "Started"
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to register application, PID {ProcessId} already exists", process.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering application {Name}", application.Name);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Получить все запущенные приложения
        /// </summary>
        public async Task<IReadOnlyList<RunningApplication>> GetRunningApplicationsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.Values.ToList().AsReadOnly();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Получить запущенные приложения пользователя
        /// </summary>
        public async Task<IReadOnlyList<RunningApplication>> GetUserRunningApplicationsAsync(string username)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.Values
                    .Where(app => app.LaunchedBy.Equals(username, StringComparison.OrdinalIgnoreCase))
                    .ToList().AsReadOnly();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Получить запущенное приложение по ID процесса
        /// </summary>
        public async Task<RunningApplication?> GetRunningApplicationByProcessIdAsync(int processId)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.TryGetValue(processId, out var app) ? app : null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Получить запущенные приложения по ID приложения
        /// </summary>
        public async Task<IReadOnlyList<RunningApplication>> GetRunningApplicationsByAppIdAsync(int applicationId)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.Values
                    .Where(app => app.ApplicationId == applicationId)
                    .ToList().AsReadOnly();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Переключиться на приложение (активировать окно)
        /// </summary>
        public async Task<bool> SwitchToApplicationAsync(int processId)
        {
            try
            {
                var runningApp = await GetRunningApplicationByProcessIdAsync(processId);
                if (runningApp == null)
                {
                    _logger.LogWarning("Cannot switch to application - PID {ProcessId} not found", processId);
                    return false;
                }

                if (runningApp.MainWindowHandle == IntPtr.Zero)
                {
                    _logger.LogWarning("Cannot switch to application {Name} - no main window handle", runningApp.Name);
                    return false;
                }

                // Если окно свернуто, разворачиваем
                if (IsIconic(runningApp.MainWindowHandle))
                {
                    ShowWindow(runningApp.MainWindowHandle, SW_RESTORE);
                }
                else
                {
                    ShowWindow(runningApp.MainWindowHandle, SW_SHOW);
                }

                // Активируем окно
                bool success = SetForegroundWindow(runningApp.MainWindowHandle);

                if (success)
                {
                    _logger.LogInformation("Successfully switched to application: {Name}", runningApp.Name);
                    
                    // Обновляем статус
                    runningApp.IsActive = true;
                    runningApp.IsMinimized = false;
                    
                    ApplicationStatusChanged?.Invoke(this, new RunningApplicationEventArgs
                    {
                        Application = runningApp,
                        Action = "Activated"
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to switch to application: {Name}", runningApp.Name);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to application PID {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Свернуть приложение
        /// </summary>
        public async Task<bool> MinimizeApplicationAsync(int processId)
        {
            try
            {
                var runningApp = await GetRunningApplicationByProcessIdAsync(processId);
                if (runningApp?.MainWindowHandle == IntPtr.Zero)
                    return false;

                bool success = ShowWindow(runningApp.MainWindowHandle, SW_MINIMIZE);
                
                if (success)
                {
                    runningApp.IsMinimized = true;
                    runningApp.IsActive = false;
                    
                    ApplicationStatusChanged?.Invoke(this, new RunningApplicationEventArgs
                    {
                        Application = runningApp,
                        Action = "Minimized"
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error minimizing application PID {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Развернуть приложение
        /// </summary>
        public async Task<bool> RestoreApplicationAsync(int processId)
        {
            try
            {
                var runningApp = await GetRunningApplicationByProcessIdAsync(processId);
                if (runningApp?.MainWindowHandle == IntPtr.Zero)
                    return false;

                bool success = ShowWindow(runningApp.MainWindowHandle, SW_RESTORE);
                
                if (success)
                {
                    runningApp.IsMinimized = false;
                    runningApp.IsActive = true;
                    
                    ApplicationStatusChanged?.Invoke(this, new RunningApplicationEventArgs
                    {
                        Application = runningApp,
                        Action = "Restored"
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring application PID {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Закрыть приложение корректно
        /// </summary>
        public async Task<bool> CloseApplicationAsync(int processId)
        {
            try
            {
                var runningApp = await GetRunningApplicationByProcessIdAsync(processId);
                if (runningApp == null)
                    return false;

                // Пытаемся закрыть корректно через сообщение WM_CLOSE
                if (runningApp.MainWindowHandle != IntPtr.Zero)
                {
                    PostMessage(runningApp.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    
                    // Ждем некоторое время завершения
                    await Task.Delay(1000);
                    
                    // Проверяем, завершился ли процесс
                    try
                    {
                        var process = Process.GetProcessById(processId);
                        if (process.HasExited)
                        {
                            _logger.LogInformation("Application {Name} closed gracefully", runningApp.Name);
                            return true;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Процесс уже завершен
                        _logger.LogInformation("Application {Name} closed gracefully", runningApp.Name);
                        return true;
                    }
                }

                // Если корректное закрытие не сработало, пробуем Process.CloseMainWindow
                try
                {
                    var process = Process.GetProcessById(processId);
                    bool closed = process.CloseMainWindow();
                    
                    if (closed)
                    {
                        // Ждем завершения
                        bool exited = process.WaitForExit(5000);
                        if (exited)
                        {
                            _logger.LogInformation("Application {Name} closed via CloseMainWindow", runningApp.Name);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error using CloseMainWindow for PID {ProcessId}", processId);
                }

                _logger.LogWarning("Failed to close application {Name} gracefully", runningApp.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing application PID {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Принудительно завершить приложение
        /// </summary>
        public async Task<bool> KillApplicationAsync(int processId)
        {
            try
            {
                var runningApp = await GetRunningApplicationByProcessIdAsync(processId);
                if (runningApp == null)
                    return false;

                var process = Process.GetProcessById(processId);
                process.Kill();
                
                // Ждем завершения
                bool exited = process.WaitForExit(3000);
                
                if (exited)
                {
                    _logger.LogInformation("Application {Name} killed forcefully", runningApp.Name);
                    return true;
                }

                _logger.LogWarning("Failed to kill application {Name}", runningApp.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error killing application PID {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Обновить статус всех приложений
        /// </summary>
        public async Task RefreshApplicationStatusAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var processesToRemove = new List<int>();

                foreach (var kvp in _runningApplications)
                {
                    var processId = kvp.Key;
                    var runningApp = kvp.Value;

                    try
                    {
                        var process = Process.GetProcessById(processId);
                        
                        if (process.HasExited)
                        {
                            processesToRemove.Add(processId);
                            continue;
                        }

                        // Обновляем статус
                        var oldStatus = $"{runningApp.IsActive}|{runningApp.IsMinimized}|{runningApp.IsResponding}";
                        
                        runningApp.UpdateFromProcess(process);
                        runningApp.IsMinimized = runningApp.MainWindowHandle != IntPtr.Zero && IsIconic(runningApp.MainWindowHandle);
                        runningApp.IsActive = runningApp.MainWindowHandle != IntPtr.Zero && 
                                            IsWindowVisible(runningApp.MainWindowHandle) && 
                                            !runningApp.IsMinimized;

                        var newStatus = $"{runningApp.IsActive}|{runningApp.IsMinimized}|{runningApp.IsResponding}";
                        
                        // Уведомляем об изменении статуса
                        if (oldStatus != newStatus)
                        {
                            ApplicationStatusChanged?.Invoke(this, new RunningApplicationEventArgs
                            {
                                Application = runningApp,
                                Action = "StatusChanged",
                                Details = $"Active:{runningApp.IsActive}, Minimized:{runningApp.IsMinimized}, Responding:{runningApp.IsResponding}"
                            });
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Процесс больше не существует
                        processesToRemove.Add(processId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error updating status for PID {ProcessId}", processId);
                    }
                }

                // Удаляем завершенные процессы
                foreach (var processId in processesToRemove)
                {
                    if (_runningApplications.TryRemove(processId, out var removedApp))
                    {
                        _logger.LogInformation("Application {Name} (PID: {ProcessId}) has exited", 
                            removedApp.Name, processId);

                        ApplicationExited?.Invoke(this, new RunningApplicationEventArgs
                        {
                            Application = removedApp,
                            Action = "Exited"
                        });
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Получить количество запущенных приложений
        /// </summary>
        public async Task<int> GetRunningApplicationsCountAsync()
        {
            await Task.CompletedTask;
            return _runningApplications.Count;
        }

        /// <summary>
        /// Получить общее использование памяти
        /// </summary>
        public async Task<long> GetTotalMemoryUsageAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.Values.Sum(app => app.MemoryUsageMB);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Проверить, запущено ли приложение
        /// </summary>
        public async Task<bool> IsApplicationRunningAsync(int applicationId)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _runningApplications.Values.Any(app => app.ApplicationId == applicationId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Запустить мониторинг процессов
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            await Task.CompletedTask;
            
            if (!_isMonitoring)
            {
                _isMonitoring = true;
                _monitoringTimer.Start();
                _logger.LogInformation("Started monitoring running applications");
            }
        }

        /// <summary>
        /// Остановить мониторинг процессов
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            await Task.CompletedTask;
            
            if (_isMonitoring)
            {
                _isMonitoring = false;
                _monitoringTimer.Stop();
                _logger.LogInformation("Stopped monitoring running applications");
            }
        }

        private async void OnMonitoringTimer(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await RefreshApplicationStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monitoring refresh");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Stop();
                _monitoringTimer?.Dispose();
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
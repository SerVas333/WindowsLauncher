using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
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
        private readonly ConcurrentDictionary<string, RunningApplication> _chromeApps; // Chrome Apps: ключ = "PID_AppName"
        private readonly Timer _monitoringTimer;
        private readonly SemaphoreSlim _semaphore;
        private bool _isMonitoring;
        private bool _disposed;
        private readonly bool _isWindows11;

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
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const byte VK_MENU = 0x12; // Alt key
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

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
            _chromeApps = new ConcurrentDictionary<string, RunningApplication>(); // Инициализируем коллекцию Chrome Apps
            _semaphore = new SemaphoreSlim(1, 1);
            
            // Определяем версию Windows для адаптации логики
            _isWindows11 = DetectWindows11();
            _logger.LogInformation("Detected OS: {OSVersion}", _isWindows11 ? "Windows 11" : "Windows 10 or earlier");
            
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
                // Специальная задержка для Notepad и других UWP приложений
                // для полной инициализации главного окна (рекомендация из статьи)
                int delayMs = GetApplicationInitializationDelay(application.Name);
                if (delayMs > 0)
                {
                    _logger.LogDebug("Waiting for {AppName} window initialization ({Delay}ms delay, OS: {OS})", 
                        application.Name, delayMs, _isWindows11 ? "Windows 11" : "Windows 10");
                    await Task.Delay(delayMs);
                }

                // Проверяем, не завершился ли процесс за время задержки
                bool processExitedDuringDelay = false;
                try 
                {
                    processExitedDuringDelay = process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    processExitedDuringDelay = true;
                }

                // Если исходный процесс завершился, пытаемся найти связанный процесс приложения
                // НО НЕ для Chrome Apps - они обрабатываются через Windows API отдельно
                int actualProcessId = process.Id;
                if (processExitedDuringDelay && !IsChromeApp(application))
                {
                    _logger.LogDebug("Original process {ProcessId} for {AppName} exited during delay, searching for related process", 
                        process.Id, application.Name);
                    
                    var relatedProcessId = await FindRelatedProcessAsync(application.Name);
                    if (relatedProcessId.HasValue)
                    {
                        actualProcessId = relatedProcessId.Value;
                        _logger.LogInformation("Found related process {NewProcessId} for {AppName} (original: {OriginalProcessId})", 
                            actualProcessId, application.Name, process.Id);
                    }
                    else
                    {
                        _logger.LogWarning("No related process found for {AppName} after original process {ProcessId} exited", 
                            application.Name, process.Id);
                        return; // Не регистрируем приложение если не нашли связанный процесс
                    }
                }
                else if (processExitedDuringDelay && IsChromeApp(application))
                {
                    _logger.LogDebug("Chrome App {AppName} launcher process {ProcessId} exited - this is expected behavior", 
                        application.Name, process.Id);
                    // Для Chrome Apps используем исходный PID процесса-лаунчера как fallback
                    // Реальный процесс будет найден через ApplicationService
                }

                var runningApp = RunningApplication.FromApplication(application, process, launchedBy);
                // Обновляем ProcessId если нашли связанный процесс
                if (actualProcessId != process.Id)
                {
                    runningApp.ProcessId = actualProcessId;
                }

                // Определяем тип регистрации: Chrome App или обычное приложение
                bool isRegistered = false;
                if (IsChromeApp(application))
                {
                    // ✅ ИСПРАВЛЕНИЕ: Для Chrome Apps используем уникальный ключ с timestamp
                    // чтобы поддерживать несколько экземпляров одного приложения
                    var timestamp = DateTime.Now.Ticks;
                    string chromeAppKey = $"{actualProcessId}_{application.Name}_{timestamp}";
                    
                    _logger.LogInformation("=== CHROME APP REGISTRATION START ===\n" +
                                         "App Name: {Name}\n" +
                                         "Process ID: {ProcessId}\n" +
                                         "Chrome App Key: {Key}\n" +
                                         "Current Chrome Apps count: {CurrentCount}",
                                         application.Name, actualProcessId, chromeAppKey, _chromeApps.Count);
                    
                    // Всегда должно успешно добавляться, так как ключ уникальный
                    if (_chromeApps.TryAdd(chromeAppKey, runningApp))
                    {
                        _logger.LogInformation("✅ SUCCESSFULLY registered Chrome App: {Name} (PID: {ProcessId}, Key: {Key})\n" +
                                             "New Chrome Apps count: {NewCount}\n" +
                                             "=== CHROME APP REGISTRATION END ===", 
                            application.Name, actualProcessId, chromeAppKey, _chromeApps.Count);
                        isRegistered = true;
                    }
                    else
                    {
                        _logger.LogError("❌ FAILED to register Chrome App with supposedly unique key {Key}\n" +
                                       "This should not happen! Existing Chrome Apps: {ExistingKeys}\n" +
                                       "=== CHROME APP REGISTRATION END ===", 
                                       chromeAppKey, string.Join(", ", _chromeApps.Keys));
                    }
                }
                else
                {
                    // Для обычных приложений используем стандартную регистрацию по PID
                    if (_runningApplications.TryAdd(actualProcessId, runningApp))
                    {
                        _logger.LogInformation("Registered running application: {Name} (PID: {ProcessId})", 
                            application.Name, actualProcessId);
                        isRegistered = true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to register application, PID {ProcessId} already exists", actualProcessId);
                    }
                }

                if (isRegistered)
                {
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
                // Объединяем обычные приложения и Chrome Apps
                var allApps = new List<RunningApplication>();
                allApps.AddRange(_runningApplications.Values);
                
                // ✅ ИСПРАВЛЕНИЕ: Для Chrome Apps создаем уникальные отображаемые имена
                // чтобы в UI показывались все экземпляры отдельно
                var chromeAppsList = _chromeApps.Values.ToList();
                var chromeAppGroups = chromeAppsList.GroupBy(app => app.Name).ToList();
                
                foreach (var group in chromeAppGroups)
                {
                    if (group.Count() == 1)
                    {
                        // Если Chrome App один - добавляем как есть
                        allApps.Add(group.First());
                    }
                    else
                    {
                        // Если несколько экземпляров одного Chrome App - добавляем с номерами
                        int instanceNumber = 1;
                        foreach (var app in group.OrderBy(a => a.StartTime))
                        {
                            var uniqueApp = new RunningApplication
                            {
                                ApplicationId = app.ApplicationId,
                                Name = $"{app.Name} ({instanceNumber})",
                                Description = app.Description,
                                Category = app.Category,
                                IconText = app.IconText,
                                Type = app.Type,
                                ProcessId = app.ProcessId,
                                ProcessName = app.ProcessName,
                                MainWindowHandle = app.MainWindowHandle,
                                MainWindowTitle = app.MainWindowTitle,
                                StartTime = app.StartTime,
                                LaunchedBy = app.LaunchedBy,
                                ExecutablePath = app.ExecutablePath,
                                Arguments = app.Arguments,
                                WorkingDirectory = app.WorkingDirectory,
                                IsActive = app.IsActive,
                                IsMinimized = app.IsMinimized,
                                IsResponding = app.IsResponding,
                                MemoryUsageMB = app.MemoryUsageMB,
                                LastStatusUpdate = app.LastStatusUpdate
                            };
                            allApps.Add(uniqueApp);
                            instanceNumber++;
                        }
                    }
                }
                
                _logger.LogDebug("GetRunningApplicationsAsync: Returning {TotalCount} apps " +
                               "(Regular: {RegularCount}, Chrome Apps: {ChromeCount})",
                               allApps.Count, _runningApplications.Count, _chromeApps.Count);
                
                // Для диагностики Chrome Apps
                if (_chromeApps.Count > 0)
                {
                    _logger.LogDebug("Chrome Apps in collection:");
                    foreach (var kvp in _chromeApps.Take(5)) // Показываем первые 5
                    {
                        _logger.LogDebug("  Key: {Key}, App: {Name}, PID: {ProcessId}", 
                            kvp.Key, kvp.Value.Name, kvp.Value.ProcessId);
                    }
                }
                
                return allApps.AsReadOnly();
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
                // Объединяем обычные приложения и Chrome Apps для пользователя
                var userApps = new List<RunningApplication>();
                
                userApps.AddRange(_runningApplications.Values
                    .Where(app => app.LaunchedBy.Equals(username, StringComparison.OrdinalIgnoreCase)));
                    
                userApps.AddRange(_chromeApps.Values
                    .Where(app => app.LaunchedBy.Equals(username, StringComparison.OrdinalIgnoreCase)));
                    
                return userApps.AsReadOnly();
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

                IntPtr windowHandle = runningApp.MainWindowHandle;
                
                // Если главное окно не найдено, пытаемся найти любое окно процесса
                if (windowHandle == IntPtr.Zero)
                {
                    windowHandle = FindWindowForProcess(runningApp.ProcessId);
                }
                
                // Альтернативный метод через обновленное главное окно процесса
                if (windowHandle == IntPtr.Zero)
                {
                    windowHandle = GetUpdatedMainWindowHandle(runningApp.ProcessId);
                }
                
                // Метод через поиск процессов по имени (для Notepad особенно эффективен)
                if (windowHandle == IntPtr.Zero && IsNotepadApplication(runningApp.Name))
                {
                    windowHandle = FindNotepadWindowByProcessName();
                }
                
                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogWarning("Cannot switch to application {Name} - no window handle found", runningApp.Name);
                    return false;
                }

                // Если окно свернуто, разворачиваем
                if (IsIconic(windowHandle))
                {
                    ShowWindow(windowHandle, SW_RESTORE);
                }
                else
                {
                    ShowWindow(windowHandle, SW_SHOW);
                }

                // Логируем детали найденного окна
                _logger.LogDebug("Found window handle {Handle:X} for application {Name} (PID: {ProcessId})", 
                    (long)windowHandle, runningApp.Name, runningApp.ProcessId);

                // Сначала пытаемся переместить окно на передний план более агрессивно
                bool success = BringWindowToForeground(windowHandle);

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
                    _logger.LogWarning("Failed to switch to application: {Name} (Handle: {Handle:X})", 
                        runningApp.Name, (long)windowHandle);
                    
                    // Попробуем альтернативный метод через SendMessage
                    const int WM_SYSCOMMAND = 0x0112;
                    const int SC_RESTORE = 0xF120;
                    
                    SendMessage(windowHandle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                    success = SetForegroundWindow(windowHandle);
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully switched to application via alternative method: {Name}", runningApp.Name);
                        runningApp.IsActive = true;
                        runningApp.IsMinimized = false;
                    }
                    else
                    {
                        _logger.LogWarning("Alternative method also failed for: {Name}", runningApp.Name);
                    }
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
                var chromeAppsToRemove = new List<string>();

                // Сначала обрабатываем Chrome Apps
                foreach (var kvp in _chromeApps)
                {
                    var chromeAppKey = kvp.Key;
                    var runningApp = kvp.Value;

                    try
                    {
                        var process = Process.GetProcessById(runningApp.ProcessId);
                        
                        if (process.HasExited)
                        {
                            chromeAppsToRemove.Add(chromeAppKey);
                            continue;
                        }

                        // Обновляем статус Chrome App
                        var oldStatus = $"{runningApp.IsActive}|{runningApp.IsMinimized}|{runningApp.IsResponding}";
                        
                        runningApp.UpdateFromProcess(process);
                        runningApp.IsMinimized = runningApp.MainWindowHandle != IntPtr.Zero && IsIconic(runningApp.MainWindowHandle);
                        runningApp.IsActive = runningApp.MainWindowHandle != IntPtr.Zero && 
                                            IsWindowVisible(runningApp.MainWindowHandle) && 
                                            !runningApp.IsMinimized;
                        
                        var newStatus = $"{runningApp.IsActive}|{runningApp.IsMinimized}|{runningApp.IsResponding}";
                        
                        // Уведомляем об изменении статуса Chrome App
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
                        // Chrome App процесс завершился
                        _logger.LogDebug("Chrome App process {ProcessId} ({Name}) has exited", runningApp.ProcessId, runningApp.Name);
                        chromeAppsToRemove.Add(chromeAppKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error monitoring Chrome App {Name} (PID: {ProcessId})", runningApp.Name, runningApp.ProcessId);
                    }
                }

                // Удаляем завершившиеся Chrome Apps
                foreach (var key in chromeAppsToRemove)
                {
                    if (_chromeApps.TryRemove(key, out var removedApp))
                    {
                        _logger.LogInformation("Chrome App {Name} (PID: {ProcessId}) has been removed from tracking", 
                            removedApp.Name, removedApp.ProcessId);
                        
                        ApplicationExited?.Invoke(this, new RunningApplicationEventArgs
                        {
                            Application = removedApp,
                            Action = "Exited"
                        });
                    }
                }

                // Теперь обрабатываем обычные приложения
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
                    catch (ArgumentException ex)
                    {
                        _logger.LogDebug("ArgumentException for process {ProcessId} ({Name}): {Message}", processId, runningApp.Name, ex.Message);
                        
                        // Дополнительная проверка - может ли процесс все еще существовать?
                        // Иногда ArgumentException возникает временно, если процесс недоступен для чтения
                        bool processStillExists = false;
                        bool windowStillExists = false;
                        
                        try
                        {
                            // Проверяем через Process.GetProcesses() - более надежный способ
                            var allProcesses = Process.GetProcesses();
                            processStillExists = allProcesses.Any(p => {
                                try 
                                {
                                    return p.Id == processId && !p.HasExited;
                                }
                                catch 
                                {
                                    return false;
                                }
                            });
                            
                            // Проверяем через поиск окна процесса
                            var windowHandle = FindWindowForProcess(processId);
                            windowStillExists = windowHandle != IntPtr.Zero;
                            
                            if (windowStillExists)
                            {
                                _logger.LogDebug("Process {ProcessId} ({Name}) has active window, keeping alive", processId, runningApp.Name);
                            }
                            
                            // Для UWP приложений ищем связанные процессы только если это действительно UWP
                            if (!processStillExists && !windowStillExists && IsUWPApplication(runningApp))
                            {
                                _logger.LogDebug("UWP application {Name} (PID: {ProcessId}) needs process migration", runningApp.Name, processId);
                                
                                var relatedProcesses = FindUWPRelatedProcesses(runningApp.Name);
                                if (relatedProcesses.Any())
                                {
                                    var newProcessId = relatedProcesses.First();
                                    _logger.LogInformation("UWP application {Name} migrating from PID {OldPID} to {NewPID}", 
                                        runningApp.Name, processId, newProcessId);
                                    
                                    // Перерегистрируем с новым PID
                                    if (_runningApplications.TryRemove(processId, out var app) && 
                                        !_runningApplications.ContainsKey(newProcessId))
                                    {
                                        app.ProcessId = newProcessId;
                                        _runningApplications.TryAdd(newProcessId, app);
                                        continue;
                                    }
                                }
                                else
                                {
                                    _logger.LogDebug("No related UWP processes found for {Name}", runningApp.Name);
                                }
                            }
                            
                            if (processStillExists || windowStillExists)
                            {
                                _logger.LogDebug("Process {ProcessId} ({Name}) still exists (Process: {ProcessExists}, Window: {WindowExists})", 
                                    processId, runningApp.Name, processStillExists, windowStillExists);
                                continue; // Не удаляем, попробуем на следующем цикле
                            }
                        }
                        catch (Exception innerEx)
                        {
                            _logger.LogDebug(innerEx, "Error during additional process check for PID {ProcessId}", processId);
                        }
                        
                        // Для браузерных приложений (включая Chrome --app) проверяем наличие связанных процессов
                        if (await IsBrowserApplicationWithRelatedProcessesAsync(runningApp))
                        {
                            _logger.LogDebug("Browser application {Name} main process exited, but related processes still running", runningApp.Name);
                            
                            // Специальная логика для Chrome --app режима
                            if (IsChromeAppApplication(runningApp))
                            {
                                var chromeAppProcess = await FindChromeAppProcessAsync(runningApp);
                                if (chromeAppProcess.HasValue && chromeAppProcess.Value != processId)
                                {
                                    // Перерегистрируем приложение с новым Chrome app процессом
                                    if (_runningApplications.TryRemove(processId, out var app) && 
                                        !_runningApplications.ContainsKey(chromeAppProcess.Value))
                                    {
                                        app.ProcessId = chromeAppProcess.Value;
                                        _runningApplications.TryAdd(chromeAppProcess.Value, app);
                                        _logger.LogInformation("Chrome app {Name} reregistered with new PID: {OldPID} -> {NewPID}", 
                                            app.Name, processId, chromeAppProcess.Value);
                                        continue;
                                    }
                                }
                            }
                            
                            // Общая логика для браузерных приложений
                            var newProcessId = await FindBrowserMainProcessAsync(runningApp);
                            if (newProcessId.HasValue && newProcessId.Value != processId)
                            {
                                // Перерегистрируем приложение с новым PID
                                if (_runningApplications.TryRemove(processId, out var app) && 
                                    !_runningApplications.ContainsKey(newProcessId.Value))
                                {
                                    app.ProcessId = newProcessId.Value;
                                    _runningApplications.TryAdd(newProcessId.Value, app);
                                    _logger.LogInformation("Browser application {Name} reregistered with new PID: {OldPID} -> {NewPID}", 
                                        app.Name, processId, newProcessId.Value);
                                    continue;
                                }
                            }
                        }
                        
                        // Процесс больше не существует
                        _logger.LogDebug("Process {ProcessId} ({Name}) confirmed as exited", processId, runningApp.Name);
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
            return _runningApplications.Count + _chromeApps.Count; // Учитываем и Chrome Apps
        }

        /// <summary>
        /// Получить общее использование памяти
        /// </summary>
        public async Task<long> GetTotalMemoryUsageAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                long totalMemory = _runningApplications.Values.Sum(app => app.MemoryUsageMB);
                totalMemory += _chromeApps.Values.Sum(app => app.MemoryUsageMB); // Добавляем Chrome Apps
                return totalMemory;
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
                // Проверяем и обычные приложения, и Chrome Apps
                return _runningApplications.Values.Any(app => app.ApplicationId == applicationId) ||
                       _chromeApps.Values.Any(app => app.ApplicationId == applicationId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Определить, является ли приложение Chrome App
        /// </summary>
        private bool IsChromeApp(Application application)
        {
            return application.Type == ApplicationType.Web && 
                   !string.IsNullOrEmpty(application.ExecutablePath) &&
                   application.ExecutablePath.ToLowerInvariant().Contains("chrome");
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

        /// <summary>
        /// Проверяет, является ли приложение браузерным и имеет ли связанные активные процессы
        /// </summary>
        private async Task<bool> IsBrowserApplicationWithRelatedProcessesAsync(RunningApplication app)
        {
            await Task.CompletedTask;
            
            // Определяем браузерные приложения по их именам или URL
            var browserNames = new[] { "chrome", "firefox", "edge", "opera", "brave", "vivaldi", "google" };
            var isBrowser = browserNames.Any(name => 
                app.Name.ToLowerInvariant().Contains(name) || 
                (app.ExecutablePath?.ToLowerInvariant().Contains("http") == true) ||
                (app.ExecutablePath?.ToLowerInvariant().Contains("chrome") == true));
                
            if (!isBrowser)
                return false;

            // Для Chrome --app приложений всегда возвращаем true
            if (IsChromeAppApplication(app))
            {
                return true;
            }

            // Ищем связанные процессы браузера
            return await FindBrowserMainProcessAsync(app).ConfigureAwait(false) != null;
        }

        /// <summary>
        /// Проверяет, является ли приложение Chrome --app приложением
        /// </summary>
        private bool IsChromeAppApplication(RunningApplication app)
        {
            // Приоритет: проверяем тип приложения
            if (app.Type == ApplicationType.ChromeApp)
            {
                return true;
            }

            // Fallback: проверяем по пути и аргументам (для обратной совместимости)
            if (app.ExecutablePath?.ToLowerInvariant().Contains("chrome") != true)
                return false;

            var allArgs = $"{app.Arguments ?? ""} {app.ExecutablePath ?? ""}".ToLowerInvariant();
            return allArgs.Contains("--app=") || allArgs.Contains("--kiosk");
        }

        /// <summary>
        /// Находит Chrome app процесс по заголовку окна
        /// </summary>
        private async Task<int?> FindChromeAppProcessAsync(RunningApplication app)
        {
            await Task.CompletedTask;
            
            try
            {
                // Извлекаем ожидаемый заголовок из --app аргумента
                var expectedTitle = ExtractTitleFromChromeAppArgs(app);
                _logger.LogDebug("Looking for Chrome app process with title containing: '{ExpectedTitle}'", expectedTitle);

                // Получаем все процессы chrome безопасно
                var allProcesses = Process.GetProcesses();
                var chromeProcesses = new List<Process>();
                
                // Безопасная фильтрация Chrome процессов
                foreach (var p in allProcesses)
                {
                    try
                    {
                        if (p.HasExited) continue;
                        
                        var processName = p.ProcessName?.ToLowerInvariant() ?? "";
                        if (processName.Contains("chrome") && p.MainWindowHandle != IntPtr.Zero)
                        {
                            chromeProcesses.Add(p);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс недоступен, пропускаем молча
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error checking process {ProcessId} for Chrome search", p.Id);
                    }
                }

                _logger.LogDebug("Found {Count} Chrome processes with windows", chromeProcesses.Count);

                // Ищем процесс с подходящим заголовком окна
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        process.Refresh();
                        string windowTitle = "";
                        try 
                        {
                            windowTitle = process.MainWindowTitle ?? "";
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // MainWindowTitle недоступен, пропускаем процесс
                            continue;
                        }
                        
                        _logger.LogDebug("Chrome process {ProcessId} has title: '{Title}'", process.Id, windowTitle);
                        
                        // Проверяем соответствие заголовка
                        if (!string.IsNullOrEmpty(windowTitle) && !string.IsNullOrEmpty(expectedTitle))
                        {
                            if (windowTitle.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Found matching Chrome app process {ProcessId} with title '{Title}'", 
                                    process.Id, windowTitle);
                                
                                // Освобождаем ресурсы
                                foreach (var p in allProcesses)
                                {
                                    try { p.Dispose(); } catch { }
                                }
                                
                                return process.Id;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error checking Chrome process {ProcessId}", process.Id);
                    }
                }

                // Освобождаем ресурсы
                foreach (var process in allProcesses)
                {
                    try { process.Dispose(); } catch { }
                }

                _logger.LogWarning("No matching Chrome app process found for expected title: '{ExpectedTitle}'", expectedTitle);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding Chrome app process for {AppName}", app.Name);
                return null;
            }
        }

        /// <summary>
        /// Извлекает ожидаемый заголовок из аргументов Chrome --app
        /// </summary>
        private string ExtractTitleFromChromeAppArgs(RunningApplication app)
        {
            try
            {
                var allArgs = $"{app.ExecutablePath ?? ""} {app.Arguments ?? ""}";
                
                // Ищем --app=file:/// или --app=http(s)://
                var appArgMatch = System.Text.RegularExpressions.Regex.Match(
                    allArgs, @"--app=(?:file:///|https?://)([^/\s]+(?:/[^\s]*)?)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (appArgMatch.Success)
                {
                    var url = appArgMatch.Groups[1].Value;
                    
                    // Для файлов извлекаем имя файла без расширения
                    if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(url);
                        _logger.LogDebug("Extracted title from file: '{FileName}'", fileName);
                        return fileName;
                    }
                    
                    // Для URL возвращаем домен
                    _logger.LogDebug("Extracted title from URL: '{Url}'", url);
                    return url.Split('/')[0];
                }
                
                // Fallback на имя приложения
                return app.Name;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting title from Chrome app args");
                return app.Name;
            }
        }

        /// <summary>
        /// Находит главный процесс браузера для веб-приложения
        /// </summary>
        private async Task<int?> FindBrowserMainProcessAsync(RunningApplication app)
        {
            await Task.CompletedTask;
            
            try
            {
                // Получаем все процессы браузеров безопасно
                var allProcesses = Process.GetProcesses();
                var browserProcessNames = new[] { "chrome", "firefox", "msedge", "opera", "brave", "vivaldi" };
                var browserProcesses = new List<Process>();
                
                // Безопасная фильтрация процессов браузеров
                foreach (var p in allProcesses)
                {
                    try
                    {
                        if (p.HasExited) continue;
                        
                        var processName = p.ProcessName?.ToLowerInvariant() ?? "";
                        if (browserProcessNames.Any(name => processName.Contains(name)) && 
                            p.MainWindowHandle != IntPtr.Zero)
                        {
                            browserProcesses.Add(p);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс недоступен, пропускаем молча
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error checking process {ProcessId} for browser search", p.Id);
                    }
                }
                
                // Сортируем по времени запуска и возвращаем самый новый
                browserProcesses = browserProcesses.OrderByDescending(p => {
                    try { return p.StartTime; }
                    catch { return DateTime.MinValue; }
                }).ToList();

                // Возвращаем PID самого нового процесса с окном
                var mainProcess = browserProcesses.FirstOrDefault();
                return mainProcess?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error finding browser main process for {AppName}", app.Name);
                return null;
            }
        }

        /// <summary>
        /// Найти окно процесса по PID с учетом особенностей Windows 11 Notepad
        /// </summary>
        private IntPtr FindWindowForProcess(int processId)
        {
            IntPtr foundWindow = IntPtr.Zero;
            var candidateWindows = new List<(IntPtr handle, string title, bool visible, string className)>();
            
            // Сначала проверим, существует ли процесс
            bool processExists = false;
            string processName = "Unknown";
            try
            {
                var process = Process.GetProcessById(processId);
                processExists = !process.HasExited;
                processName = processExists ? process.ProcessName : "Exited";
                process.Dispose();
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Process {ProcessId} does not exist or has no access", processId);
                return IntPtr.Zero;
            }
            
            if (!processExists)
            {
                _logger.LogWarning("Process {ProcessId} ({ProcessName}) has exited", processId, processName);
                return IntPtr.Zero;
            }
            
            _logger.LogDebug("Searching windows for process {ProcessId} ({ProcessName})", processId, processName);
            
            // Поиск всех окон системы
            int totalWindowsChecked = 0;
            bool enumSuccess = EnumWindows((hWnd, lParam) =>
            {
                totalWindowsChecked++;
                
                try
                {
                    uint windowProcessId = 0;
                    
                    // Безопасный вызов GetWindowThreadProcessId с проверкой
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out windowProcessId);
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Окно недоступно или больше не существует - пропускаем молча
                        return true;
                    }
                    
                    if (windowProcessId == processId)
                    {
                        bool isVisible = false;
                        string windowTitle = "";
                        string className = "";
                        
                        // Безопасное получение информации об окне
                        try
                        {
                            isVisible = IsWindowVisible(hWnd);
                            windowTitle = GetWindowTitle(hWnd);
                            className = GetWindowClassName(hWnd);
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // Окно стало недоступно во время запроса - используем безопасные значения
                            isVisible = false;
                            windowTitle = "Unavailable";
                            className = "Unknown";
                        }
                        
                        candidateWindows.Add((hWnd, windowTitle, isVisible, className));
                        
                        _logger.LogDebug("Found window for process {ProcessId}: handle={Handle:X}, class='{Class}', title='{Title}', visible={Visible}", 
                            processId, (long)hWnd, className, windowTitle, isVisible);
                        
                        // Специальная логика для Notepad
                        if (IsNotepadMainWindow(hWnd, className, windowTitle))
                        {
                            foundWindow = hWnd;
                            _logger.LogDebug("Selected Notepad main window: handle={Handle:X}", (long)hWnd);
                        }
                        // Общая приоритетная логика: видимые окна с заголовком
                        else if (isVisible && !string.IsNullOrEmpty(windowTitle) && foundWindow == IntPtr.Zero)
                        {
                            foundWindow = hWnd;
                            _logger.LogDebug("Selected visible window with title: handle={Handle:X}", (long)hWnd);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Логируем только неожиданные исключения как Trace, не Warning
                    _logger.LogTrace(ex, "Unexpected error checking window {Handle:X}", (long)hWnd);
                }
                
                return true; // Продолжаем поиск
            }, IntPtr.Zero);
            
            _logger.LogDebug("EnumWindows completed: success={Success}, totalChecked={Total}, candidatesFound={Candidates}", 
                enumSuccess, totalWindowsChecked, candidateWindows.Count);
            
            // Детальное логирование найденных окон
            if (candidateWindows.Any())
            {
                _logger.LogInformation("Process {ProcessId} ({ProcessName}) has {Count} windows: {Windows}", 
                    processId, processName, candidateWindows.Count,
                    string.Join(", ", candidateWindows.Select(w => $"[{w.handle:X}] '{w.title}' class='{w.className}' (visible: {w.visible})")));
            }
            else
            {
                _logger.LogWarning("No windows found for process {ProcessId} ({ProcessName}) - EnumWindows checked {Total} total windows", 
                    processId, processName, totalWindowsChecked);
            }
            
            // Если не нашли окно через специальную логику, применяем общие правила
            if (foundWindow == IntPtr.Zero && candidateWindows.Any())
            {
                // Приоритет: видимые окна с заголовком
                var visibleWithTitle = candidateWindows.FirstOrDefault(w => w.visible && !string.IsNullOrEmpty(w.title));
                if (visibleWithTitle.handle != IntPtr.Zero)
                {
                    foundWindow = visibleWithTitle.handle;
                    _logger.LogDebug("Selected visible window with title as fallback: handle={Handle:X}", (long)foundWindow);
                }
                
                // Если все еще не нашли, берем любое видимое
                if (foundWindow == IntPtr.Zero)
                {
                    var visibleWindow = candidateWindows.FirstOrDefault(w => w.visible);
                    if (visibleWindow.handle != IntPtr.Zero)
                    {
                        foundWindow = visibleWindow.handle;
                        _logger.LogDebug("Selected any visible window as fallback: handle={Handle:X}", (long)foundWindow);
                    }
                }
                
                // В крайнем случае, берем любое окно
                if (foundWindow == IntPtr.Zero)
                {
                    foundWindow = candidateWindows.First().handle;
                    _logger.LogDebug("Selected any window as last resort: handle={Handle:X}", (long)foundWindow);
                }
            }
            
            if (foundWindow != IntPtr.Zero)
            {
                _logger.LogInformation("Selected window {Handle:X} for process {ProcessId} ({ProcessName})", 
                    (long)foundWindow, processId, processName);
            }
            else
            {
                _logger.LogWarning("No suitable window found for process {ProcessId} ({ProcessName})", processId, processName);
            }
            
            return foundWindow;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                var title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            try
            {
                var className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                return className.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Проверяет, является ли окно главным окном Notepad (Windows 10/11)
        /// </summary>
        private bool IsNotepadMainWindow(IntPtr hWnd, string className, string windowTitle)
        {
            // Windows 10: обычно класс "Notepad"
            // Windows 11: может быть "Notepad" или "ApplicationFrameWindow" (UWP версия)
            var win10NotepadClasses = new[] { "Notepad" };
            var win11UwpNotepadClasses = new[] { "ApplicationFrameWindow" };
            
            // Проверяем заголовок окна на соответствие Notepad
            bool hasNotepadTitle = !string.IsNullOrEmpty(windowTitle) && 
                (windowTitle.Contains("Блокнот") || windowTitle.Contains("Notepad") || 
                 windowTitle.Contains("- Notepad") || windowTitle.Contains("— Блокнот") ||
                 windowTitle.EndsWith(".txt - Notepad") || windowTitle.EndsWith(".txt — Блокнот"));
            
            // Windows 10 классический Notepad - класс "Notepad"
            if (win10NotepadClasses.Any(cls => className.Equals(cls, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Detected Windows 10 classic Notepad: class='{ClassName}', title='{Title}'", className, windowTitle);
                return true; // Для классического Notepad достаточно класса
            }
            
            // Windows 11 UWP Notepad - класс "ApplicationFrameWindow" + заголовок
            if (win11UwpNotepadClasses.Any(cls => className.Equals(cls, StringComparison.OrdinalIgnoreCase)))
            {
                if (hasNotepadTitle)
                {
                    _logger.LogDebug("Detected Windows 11 UWP Notepad: class='{ClassName}', title='{Title}'", className, windowTitle);
                    return true;
                }
                else
                {
                    _logger.LogDebug("ApplicationFrameWindow found but title doesn't match Notepad: '{Title}'", windowTitle);
                    return false;
                }
            }
            
            // Дополнительная проверка для других возможных классов с Notepad заголовком
            if (hasNotepadTitle && IsWindowVisible(hWnd))
            {
                _logger.LogDebug("Found potential Notepad window with unusual class: class='{ClassName}', title='{Title}'", className, windowTitle);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Проверить, является ли приложение UWP
        /// </summary>
        private bool IsUWPApplication(RunningApplication app)
        {
            // UWP приложения часто имеют специфические пути или характеристики
            var uwpApps = new[] { "calculator", "notepad", "photos", "mail", "calendar", "store" };
            return uwpApps.Any(name => app.Name.ToLowerInvariant().Contains(name));
        }

        /// <summary>
        /// Перегрузка для проверки UWP по Application
        /// </summary>
        private bool IsUWPApplication(Application app)
        {
            var uwpApps = new[] { "calculator", "notepad", "photos", "mail", "calendar", "store" };
            return uwpApps.Any(name => app.Name.ToLowerInvariant().Contains(name));
        }

        /// <summary>
        /// Проверить, является ли приложение Notepad
        /// </summary>
        private bool IsNotepadApplication(string appName)
        {
            return appName.ToLowerInvariant().Contains("notepad") || 
                   appName.ToLowerInvariant().Contains("блокнот");
        }

        /// <summary>
        /// Найти связанные процессы UWP приложения
        /// </summary>
        private List<int> FindUWPRelatedProcesses(string appName)
        {
            var relatedProcesses = new List<int>();
            
            try
            {
                // Ограничиваем поиск только Calculator и связанными процессами
                var searchTerms = new[] { appName.ToLowerInvariant() };
                if (appName.ToLowerInvariant().Contains("calculator"))
                {
                    searchTerms = new[] { "calculator", "calc", "windowsapps" };
                }
                
                var allProcesses = Process.GetProcesses();
                var candidateProcesses = new List<Process>();
                
                // Безопасная фильтрация процессов с минимизацией Win32Exception
                foreach (var p in allProcesses)
                {
                    try 
                    {
                        // Проверяем HasExited первым - это самая частая причина Win32Exception
                        if (p.HasExited) 
                        {
                            continue;
                        }
                        
                        // Получаем имя процесса безопасно
                        var processName = p.ProcessName?.ToLowerInvariant() ?? "";
                        if (searchTerms.Any(term => processName.Contains(term)))
                        {
                            candidateProcesses.Add(p);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - нормальное поведение для недоступных процессов
                        // Не логируем, чтобы не засорять логи и не показывать в Visual Studio
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // Только неожиданные исключения логируем
                        _logger.LogTrace(ex, "Unexpected error accessing process information for PID {ProcessId}", p.Id);
                    }
                }
                
                _logger.LogDebug("Found {Count} candidate processes for UWP app {AppName}", candidateProcesses.Count, appName);
                
                // Проверяем только кандидатов, а не все процессы
                foreach (var process in candidateProcesses)
                {
                    try
                    {
                        // Проверяем наличие окна только для подходящих процессов
                        var windowHandle = FindWindowForProcess(process.Id);
                        if (windowHandle != IntPtr.Zero)
                        {
                            relatedProcesses.Add(process.Id);
                            _logger.LogDebug("Found UWP process {ProcessId} ({ProcessName}) with window", process.Id, process.ProcessName);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс стал недоступен, это нормально
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Unexpected error checking process {ProcessId}", process.Id);
                    }
                }
                
                // Освобождаем ресурсы
                foreach (var process in allProcesses)
                {
                    try { process.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error finding UWP related processes for {AppName}", appName);
            }
            
            return relatedProcesses;
        }

        /// <summary>
        /// Более надежное переключение окна на передний план с обходом ограничений Windows
        /// </summary>
        private bool BringWindowToForeground(IntPtr windowHandle)
        {
            try
            {
                // Метод 1: Стандартный SetForegroundWindow
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogDebug("Window brought to foreground via SetForegroundWindow");
                    return true;
                }

                // Метод 2: Симуляция Alt-клавиши для обхода ограничений SetForegroundWindow
                _logger.LogDebug("Using Alt key simulation technique");
                keybd_event(VK_MENU, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(VK_MENU, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                ShowWindow(windowHandle, SW_RESTORE);
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogDebug("Window brought to foreground via Alt simulation");
                    return true;
                }

                // Метод 3: AttachThreadInput для связывания потоков
                _logger.LogDebug("Using AttachThreadInput technique");
                uint currentThreadId = GetCurrentThreadId();
                uint windowThreadId = GetWindowThreadProcessId(windowHandle, out _);
                
                if (windowThreadId != 0 && windowThreadId != currentThreadId)
                {
                    if (AttachThreadInput(currentThreadId, windowThreadId, true))
                    {
                        ShowWindow(windowHandle, SW_RESTORE);
                        SetForegroundWindow(windowHandle);
                        BringWindowToTop(windowHandle);
                        AttachThreadInput(currentThreadId, windowThreadId, false);
                        
                        _logger.LogDebug("Window brought to foreground via AttachThreadInput");
                        return true;
                    }
                }

                // Метод 4: Через SetWindowPos с TOPMOST
                SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogDebug("Window brought to foreground via SetWindowPos + SetForegroundWindow");
                    return true;
                }

                // Метод 5: Через SendMessage с WM_SYSCOMMAND
                const int WM_SYSCOMMAND = 0x0112;
                const int SC_RESTORE = 0xF120;
                
                SendMessage(windowHandle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                ShowWindow(windowHandle, SW_RESTORE);
                
                bool finalResult = SetForegroundWindow(windowHandle);
                _logger.LogDebug("Final attempt result: {Result}", finalResult);
                
                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bringing window to foreground");
                return false;
            }
        }

        /// <summary>
        /// Найти окно Notepad через Process.GetProcessesByName (более надежный метод)
        /// </summary>
        private IntPtr FindNotepadWindowByProcessName()
        {
            try
            {
                // Поиск всех процессов Notepad
                var notepadProcesses = Process.GetProcessesByName("notepad");
                
                _logger.LogDebug("Found {Count} notepad processes via GetProcessesByName", notepadProcesses.Length);
                
                foreach (var process in notepadProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            // Обновляем информацию о процессе
                            process.Refresh();
                            
                            var mainWindow = process.MainWindowHandle;
                            if (mainWindow != IntPtr.Zero)
                            {
                                string title = "";
                                try { title = process.MainWindowTitle ?? ""; } catch { title = "Unavailable"; }
                                
                                _logger.LogInformation("Found Notepad window via process name: PID={ProcessId}, Handle={Handle:X}, Title='{Title}'", 
                                    process.Id, (long)mainWindow, title);
                                
                                // Освобождаем остальные процессы
                                foreach (var p in notepadProcesses)
                                {
                                    try { p.Dispose(); } catch { }
                                }
                                
                                return mainWindow;
                            }
                            else
                            {
                                // Если MainWindowHandle пустой, пытаемся найти через EnumWindows
                                var windowHandle = FindWindowForProcess(process.Id);
                                if (windowHandle != IntPtr.Zero)
                                {
                                    _logger.LogInformation("Found Notepad window via EnumWindows fallback: PID={ProcessId}, Handle={Handle:X}", 
                                        process.Id, (long)windowHandle);
                                    
                                    // Освобождаем ресурсы
                                    foreach (var p in notepadProcesses)
                                    {
                                        try { p.Dispose(); } catch { }
                                    }
                                    
                                    return windowHandle;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error checking notepad process {ProcessId}", process.Id);
                    }
                }
                
                // Освобождаем ресурсы
                foreach (var process in notepadProcesses)
                {
                    try { process.Dispose(); } catch { }
                }
                
                _logger.LogWarning("No valid Notepad windows found via process name search");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FindNotepadWindowByProcessName");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Определить версию Windows 11
        /// </summary>
        private bool DetectWindows11()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                // Windows 11 имеет версию 10.0.22000 и выше
                return version.Major == 10 && version.Build >= 22000;
            }
            catch
            {
                return false; // По умолчанию считаем Windows 10
            }
        }

        /// <summary>
        /// Получить задержку инициализации для приложения в зависимости от ОС
        /// </summary>
        private int GetApplicationInitializationDelay(string appName)
        {
            if (IsNotepadApplication(appName))
            {
                // Windows 11 UWP Notepad требует больше времени для инициализации
                return _isWindows11 ? 1500 : 800;
            }
            
            var appNameLower = appName.ToLowerInvariant();
            if (appNameLower.Contains("calculator"))
            {
                return _isWindows11 ? 1200 : 600;
            }
            
            if (appNameLower.Contains("photos") || appNameLower.Contains("mail") || appNameLower.Contains("calendar"))
            {
                return _isWindows11 ? 1000 : 500;
            }
            
            return 0; // Без задержки для остальных приложений
        }

        /// <summary>
        /// Получить обновленный MainWindowHandle процесса
        /// </summary>
        private IntPtr GetUpdatedMainWindowHandle(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    _logger.LogDebug("Process {ProcessId} has exited, cannot get main window", processId);
                    process.Dispose();
                    return IntPtr.Zero;
                }
                
                // Обновляем информацию о процессе
                process.Refresh();
                var mainWindow = process.MainWindowHandle;
                
                _logger.LogDebug("Updated MainWindowHandle for process {ProcessId} ({ProcessName}): {Handle:X}", 
                    processId, process.ProcessName, (long)mainWindow);
                
                process.Dispose();
                return mainWindow;
            }
            catch (ArgumentException ex)
            {
                _logger.LogDebug("Process {ProcessId} not found: {Message}", processId, ex.Message);
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting updated main window for process {ProcessId}", processId);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Найти связанный процесс приложения если основной процесс завершился
        /// </summary>
        private async Task<int?> FindRelatedProcessAsync(string appName)
        {
            await Task.CompletedTask;
            
            try
            {
                // Для UWP приложений используем специальную логику
                if (IsNotepadApplication(appName))
                {
                    var relatedProcesses = FindUWPRelatedProcesses(appName);
                    return relatedProcesses.FirstOrDefault();
                }
                
                if (appName.ToLowerInvariant().Contains("calculator"))
                {
                    var relatedProcesses = FindUWPRelatedProcesses("calculator");
                    return relatedProcesses.FirstOrDefault();
                }
                
                // Для обычных desktop приложений ищем по имени процесса
                var allProcesses = Process.GetProcesses();
                Process? matchingProcess = null;
                var searchName = appName.Replace(" ", "");
                
                // Безопасный поиск процесса с минимизацией Win32Exception
                foreach (var p in allProcesses)
                {
                    try
                    {
                        // Проверяем HasExited первым
                        if (p.HasExited) 
                            continue;
                            
                        // Проверяем имя процесса
                        if (!p.ProcessName.Contains(searchName, StringComparison.OrdinalIgnoreCase))
                            continue;
                            
                        // Проверяем наличие окна
                        if (FindWindowForProcess(p.Id) != IntPtr.Zero)
                        {
                            matchingProcess = p;
                            break;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс недоступен, это нормально
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Unexpected error checking process {ProcessId} for app {AppName}", p.Id, appName);
                    }
                }
                
                // Освобождаем ресурсы
                foreach (var process in allProcesses)
                {
                    try { process.Dispose(); } catch { }
                }
                
                return matchingProcess?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error finding related process for {AppName}", appName);
                return null;
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
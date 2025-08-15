using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Лаунчер для Chrome Apps с поддержкой множественных экземпляров
    /// Решает проблемы с многопроцессной архитектурой Chrome и регистрацией экземпляров
    /// </summary>
    public class ChromeAppLauncher : IApplicationLauncher
    {
        private readonly ILogger<ChromeAppLauncher> _logger;
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        
        public ApplicationType SupportedType => ApplicationType.ChromeApp;
        public int Priority => 5; // Низкий приоритет - fallback для WebView2ApplicationLauncher

        /// <summary>
        /// Событие активации окна приложения (не используется в данном лаунчере)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowActivated;

        /// <summary>  
        /// Событие закрытия окна приложения (не используется в данном лаунчере)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowClosed;
        
        // Регулярные выражения для поиска Chrome App процессов
        private static readonly Regex ChromeProcessRegex = new(@"chrome\.exe", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ChromeAppArgumentRegex = new(@"--app=([^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        public ChromeAppLauncher(
            ILogger<ChromeAppLauncher> logger,
            IWindowManager windowManager,
            IProcessMonitor processMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _processMonitor = processMonitor ?? throw new ArgumentNullException(nameof(processMonitor));
        }
        
        #region IApplicationLauncher Implementation
        
        public bool CanLaunch(Application application)
        {
            if (application?.Type != ApplicationType.ChromeApp)
                return false;
            
            if (string.IsNullOrEmpty(application.ExecutablePath))
            {
                _logger.LogWarning("Chrome App {AppName} has empty path", application.Name);
                return false;
            }
            
            // Проверяем что путь выглядит как URL или chrome app path
            var path = application.ExecutablePath.Trim();
            
            // URL для web app
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // Локальный путь к Chrome App
            if (path.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // Путь к локальной папке приложения
            if (Directory.Exists(path))
            {
                var manifestPath = Path.Combine(path, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    return true;
                }
            }
            
            _logger.LogWarning("Chrome App {AppName} has invalid path format: {Path}", 
                application.Name, application.ExecutablePath);
            return false;
        }
        
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (!CanLaunch(application))
            {
                var error = $"Cannot launch Chrome App {application.Name}";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            var startTime = DateTime.Now;
            _logger.LogInformation("Launching Chrome App: {AppName} at {Path}", 
                application.Name, application.ExecutablePath);
            
            try
            {
                // Создаем ProcessStartInfo для Chrome
                var startInfo = CreateChromeProcessStartInfo(application);
                
                // Запускаем Chrome процесс
                var process = await LaunchChromeProcessAsync(startInfo);
                if (process == null)
                {
                    var error = $"Failed to start Chrome process for {application.Name}";
                    _logger.LogError(error);
                    return LaunchResult.Failure(error);
                }
                
                _logger.LogDebug("Chrome process started with PID {ProcessId} for app {AppName}", 
                    process.Id, application.Name);
                
                // Ждем появления окна Chrome App
                var chromeAppProcess = await WaitForChromeAppProcessAsync(application, process, TimeSpan.FromSeconds(15));
                
                if (chromeAppProcess == null)
                {
                    var error = $"Chrome App window for {application.Name} did not appear within timeout";
                    _logger.LogWarning(error);
                    
                    // Все равно создаем экземпляр с исходным процессом
                    var fallbackInstance = ApplicationInstance.CreateFromProcess(application, process, launchedBy);
                    fallbackInstance.State = ApplicationState.Starting;
                    fallbackInstance.Metadata["LaunchType"] = "ChromeApp";
                    fallbackInstance.Metadata["ChromePath"] = application.ExecutablePath;
                    
                    await _processMonitor.DisposeProcessSafelyAsync(process);
                    
                    var fallbackDuration = DateTime.Now - startTime;
                    return LaunchResult.Success(fallbackInstance, fallbackDuration);
                }
                
                // Создаем ApplicationInstance с найденным Chrome App процессом
                var instance = ApplicationInstance.CreateFromProcess(application, chromeAppProcess.Process, launchedBy);
                instance.State = ApplicationState.Running;
                instance.MainWindow = chromeAppProcess.MainWindow;
                
                // Добавляем метаданные Chrome App
                instance.Metadata["LaunchType"] = "ChromeApp";
                instance.Metadata["ChromePath"] = application.ExecutablePath;
                instance.Metadata["ParentChromeProcessId"] = process.Id;
                instance.Metadata["WindowTitle"] = chromeAppProcess.MainWindow?.Title ?? string.Empty;
                
                _logger.LogInformation("Successfully launched Chrome App {AppName} with window PID {ProcessId} (Parent: {ParentPid})", 
                    application.Name, chromeAppProcess.Process.Id, process.Id);
                
                // Освобождаем Process объекты
                await _processMonitor.DisposeProcessSafelyAsync(process);
                await _processMonitor.DisposeProcessSafelyAsync(chromeAppProcess.Process);
                
                var launchDuration = DateTime.Now - startTime;
                return LaunchResult.Success(instance, launchDuration);
            }
            catch (Win32Exception ex)
            {
                var error = $"Win32Exception launching Chrome App {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Win32Exception launching Chrome App {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
            catch (Exception ex)
            {
                var error = $"Error launching Chrome App {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Error launching Chrome App {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
        }
        
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            if (application?.Type != ApplicationType.ChromeApp)
                return null;
            
            try
            {
                _logger.LogDebug("Searching for existing Chrome App instances of {AppName}", application.Name);
                
                // Ищем все Chrome процессы
                var chromeProcessIds = await _processMonitor.FindProcessesByNameAsync("chrome");
                var matchingInstances = new List<ChromeAppProcessInfo>();
                
                foreach (var processId in chromeProcessIds)
                {
                    try
                    {
                        var processInfo = await _processMonitor.GetProcessInfoAsync(processId);
                        if (processInfo == null || !processInfo.IsAlive)
                            continue;
                        
                        // Проверяем что это Chrome App процесс с нужным URL
                        if (IsChromeAppProcess(processInfo, application))
                        {
                            var process = await _processMonitor.GetProcessSafelyAsync(processId);
                            if (process != null)
                            {
                                var mainWindow = await FindChromeAppWindowAsync(processId, application);
                                
                                matchingInstances.Add(new ChromeAppProcessInfo
                                {
                                    Process = process,
                                    MainWindow = mainWindow,
                                    ProcessInfo = processInfo
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error checking Chrome process {ProcessId}", processId);
                    }
                }
                
                if (matchingInstances.Count == 0)
                {
                    _logger.LogDebug("No existing Chrome App instances found for {AppName}", application.Name);
                    return null;
                }
                
                // Выбираем лучший экземпляр (с окном и активный)
                var bestInstance = matchingInstances
                    .OrderByDescending(i => i.MainWindow != null && i.MainWindow.IsVisible)
                    .ThenByDescending(i => i.MainWindow?.IsActive ?? false)
                    .ThenByDescending(i => i.ProcessInfo.StartTime)
                    .First();
                
                var instance = ApplicationInstance.CreateFromProcess(application, bestInstance.Process, "system");
                instance.State = ApplicationState.Running;
                instance.MainWindow = bestInstance.MainWindow;
                instance.Metadata["LaunchType"] = "ChromeApp";
                instance.Metadata["ChromePath"] = application.ExecutablePath;
                
                // Освобождаем ресурсы неиспользуемых процессов
                foreach (var unused in matchingInstances.Where(i => i != bestInstance))
                {
                    await _processMonitor.DisposeProcessSafelyAsync(unused.Process);
                }
                
                await _processMonitor.DisposeProcessSafelyAsync(bestInstance.Process);
                
                _logger.LogDebug("Found existing Chrome App instance for {AppName} with PID {ProcessId}", 
                    application.Name, instance.ProcessId);
                
                return instance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing Chrome App instance for {AppName}", application.Name);
                return null;
            }
        }
        
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                return await FindChromeAppWindowAsync(processId, application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for Chrome App {AppName} (PID: {ProcessId})", 
                    application.Name, processId);
                return null;
            }
        }
        
        #endregion
        
        #region Chrome-специфичные методы
        
        private ProcessStartInfo CreateChromeProcessStartInfo(Application application)
        {
            // Пытаемся найти Chrome в стандартных местах
            var chromePath = FindChromeExecutable();
            
            var startInfo = new ProcessStartInfo
            {
                FileName = chromePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Maximized // Открываем Chrome App в полноэкранном режиме
            };
            
            // Строим аргументы для Chrome App
            var args = new List<string>
            {
                $"--app={application.ExecutablePath}",  // Главный аргумент для Chrome App
                "--start-maximized",         // Запускаем в полноэкранном режиме
                "--no-first-run",            // Пропускаем первоначальную настройку
                "--no-default-browser-check", // Не проверяем браузер по умолчанию
                "--disable-default-apps",    // Отключаем дефолтные приложения
                "--disable-extensions",      // Отключаем расширения для стабильности
                "--disable-plugins",         // Отключаем плагины
                "--disable-background-mode", // Отключаем фоновый режим
                "--disable-background-timer-throttling", // Не ограничиваем таймеры
                "--disable-renderer-backgrounding",      // Не переводим рендерер в фон
                "--disable-backgrounding-occluded-windows" // Не блокируемфигипи
            };
            
            // Добавляем пользовательские аргументы если есть
            if (!string.IsNullOrEmpty(application.Arguments))
            {
                args.Add(application.Arguments);
            }
            
            startInfo.Arguments = string.Join(" ", args);
            
            _logger.LogDebug("Chrome command line for {AppName}: {FileName} {Arguments}", 
                application.Name, startInfo.FileName, startInfo.Arguments);
            
            return startInfo;
        }
        
        private string FindChromeExecutable()
        {
            // Стандартные пути установки Chrome
            var possiblePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES(X86)%\Google\Chrome\Application\chrome.exe")
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("Found Chrome executable at: {Path}", path);
                    return path;
                }
            }
            
            // Последняя попытка - из PATH
            _logger.LogWarning("Chrome executable not found in standard locations, trying 'chrome'");
            return "chrome";
        }
        
        private async Task<Process?> LaunchChromeProcessAsync(ProcessStartInfo startInfo)
        {
            try
            {
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    _logger.LogError("Process.Start returned null for Chrome");
                    return null;
                }
                
                // Ждем немного чтобы Chrome успел запуститься
                await Task.Delay(1000);
                
                // Проверяем что процесс еще жив
                bool isAlive = await _processMonitor.IsProcessAliveAsync(process.Id);
                if (!isAlive)
                {
                    _logger.LogError("Chrome process {ProcessId} exited immediately", process.Id);
                    return null;
                }
                
                return process;
            }
            catch (Win32Exception ex)
            {
                _logger.LogError(ex, "Win32Exception starting Chrome process: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Chrome process");
                throw;
            }
        }
        
        private async Task<ChromeAppProcessInfo?> WaitForChromeAppProcessAsync(
            Application application, 
            Process parentProcess, 
            TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var lastLogTime = DateTime.Now;
            
            _logger.LogDebug("Waiting for Chrome App window to appear for {AppName} (timeout: {Timeout})", 
                application.Name, timeout);
            
            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    // Ищем все Chrome процессы
                    var chromeProcessIds = await _processMonitor.FindProcessesByNameAsync("chrome");
                    
                    foreach (var processId in chromeProcessIds)
                    {
                        // Пропускаем родительский процесс
                        if (processId == parentProcess.Id)
                            continue;
                        
                        var processInfo = await _processMonitor.GetProcessInfoAsync(processId);
                        if (processInfo == null || !processInfo.IsAlive)
                            continue;
                        
                        // Проверяем что это наш Chrome App процесс
                        if (IsChromeAppProcess(processInfo, application))
                        {
                            // Ищем главное окно
                            var mainWindow = await FindChromeAppWindowAsync(processId, application);
                            if (mainWindow != null && mainWindow.IsVisible)
                            {
                                var process = await _processMonitor.GetProcessSafelyAsync(processId);
                                if (process != null)
                                {
                                    _logger.LogDebug("Found Chrome App process {ProcessId} with window for {AppName}", 
                                        processId, application.Name);
                                    
                                    return new ChromeAppProcessInfo
                                    {
                                        Process = process,
                                        MainWindow = mainWindow,
                                        ProcessInfo = processInfo
                                    };
                                }
                            }
                        }
                    }
                    
                    // Проверяем что родительский процесс еще жив
                    bool parentAlive = await _processMonitor.IsProcessAliveAsync(parentProcess.Id);
                    if (!parentAlive)
                    {
                        _logger.LogWarning("Parent Chrome process {ProcessId} exited while waiting for app window", 
                            parentProcess.Id);
                        break;
                    }
                    
                    // Логируем прогресс каждые 3 секунды
                    if (DateTime.Now - lastLogTime > TimeSpan.FromSeconds(3))
                    {
                        _logger.LogDebug("Still waiting for Chrome App window... ({Elapsed:F1}s elapsed)", 
                            (DateTime.Now - startTime).TotalSeconds);
                        lastLogTime = DateTime.Now;
                    }
                    
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error during Chrome App process search");
                    await Task.Delay(500);
                }
            }
            
            _logger.LogWarning("Chrome App window for {AppName} did not appear within {Timeout}", 
                application.Name, timeout);
            return null;
        }
        
        private bool IsChromeAppProcess(ProcessInfo processInfo, Application application)
        {
            // Проверяем что это Chrome процесс
            if (!ChromeProcessRegex.IsMatch(processInfo.ProcessName) && 
                !ChromeProcessRegex.IsMatch(processInfo.ExecutablePath))
            {
                return false;
            }
            
            // Проверяем аргументы командной строки
            if (string.IsNullOrEmpty(processInfo.CommandLine))
                return false;
            
            // Должен содержать --app=наш_путь
            var appMatch = ChromeAppArgumentRegex.Match(processInfo.CommandLine);
            if (!appMatch.Success)
                return false;
            
            var appUrl = appMatch.Groups[1].Value;
            
            // Проверяем что URL соответствует нашему приложению
            return string.Equals(appUrl, application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        
        private async Task<WindowInfo?> FindChromeAppWindowAsync(int processId, Application application)
        {
            try
            {
                // Сначала пытаемся найти окно по заголовку (имя приложения)
                var window = await _windowManager.FindMainWindowAsync(processId, expectedTitle: application.Name);
                if (window != null && window.IsVisible)
                {
                    return window;
                }
                
                // Затем ищем любое видимое окно этого процесса
                var allWindows = await _windowManager.GetAllWindowsForProcessAsync(processId);
                var visibleWindow = allWindows.FirstOrDefault(w => w.IsVisible && !string.IsNullOrEmpty(w.Title));
                
                if (visibleWindow != null)
                {
                    _logger.LogDebug("Found Chrome App window by visibility: '{Title}' for {AppName}", 
                        visibleWindow.Title, application.Name);
                    return visibleWindow;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding Chrome App window for process {ProcessId}", processId);
                return null;
            }
        }
        
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // Chrome Apps требуют больше времени для инициализации
            return 15000; // 15 секунд
        }
        
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance?.ProcessId > 0)
                {
                    _logger.LogDebug("Cleaning up Chrome App {AppName} (PID: {ProcessId})", 
                        instance.Application.Name, instance.ProcessId);
                    
                    // Для Chrome Apps может потребоваться специальная очистка
                    await _processMonitor.CleanupProcessAsync(instance.ProcessId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of Chrome App {AppName}", 
                    instance?.Application.Name);
            }
        }

        public Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                _logger.LogDebug("Switching to Chrome App instance {InstanceId}", instanceId);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to Chrome App instance {InstanceId}", instanceId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                _logger.LogDebug("Terminating Chrome App instance {InstanceId} (Force: {Force})", instanceId, force);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating Chrome App instance {InstanceId}", instanceId);
                return Task.FromResult(false);
            }
        }

        public Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            try
            {
                return Task.FromResult<IReadOnlyList<ApplicationInstance>>(new List<ApplicationInstance>().AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active Chrome App instances");
                return Task.FromResult<IReadOnlyList<ApplicationInstance>>(new List<ApplicationInstance>().AsReadOnly());
            }
        }
        
        #endregion
        
        #region Вспомогательные классы
        
        private class ChromeAppProcessInfo
        {
            public Process Process { get; set; } = null!;
            public WindowInfo? MainWindow { get; set; }
            public ProcessInfo ProcessInfo { get; set; } = null!;
        }
        
        #endregion
    }
}
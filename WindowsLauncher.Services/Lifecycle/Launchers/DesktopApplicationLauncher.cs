using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Современный лаунчер для desktop приложений с правильной single-instance логикой
    /// Архитектура основана на лучших практиках AppSwitcher-ов:
    /// 1. Безопасный поиск процессов текущего пользователя
    /// 2. Надежное определение окон через Process.MainWindowHandle с fallback
    /// 3. Четкое разделение ответственности между компонентами
    /// 4. Правильная single-instance логика без множественных запусков
    /// </summary>
    public class DesktopApplicationLauncher : IApplicationLauncher
    {
        private readonly ILogger<DesktopApplicationLauncher> _logger;
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        private readonly string _currentUserSid;
        
        public ApplicationType SupportedType => ApplicationType.Desktop;
        public int Priority => 10;

        /// <summary>
        /// Событие активации окна приложения (не используется в данном лаунчере)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowActivated;

        /// <summary>  
        /// Событие закрытия окна приложения (не используется в данном лаунчере)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowClosed;
        
        public DesktopApplicationLauncher(
            ILogger<DesktopApplicationLauncher> logger,
            IWindowManager windowManager,
            IProcessMonitor processMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _processMonitor = processMonitor ?? throw new ArgumentNullException(nameof(processMonitor));
            _currentUserSid = GetCurrentUserSid();
        }
        
        #region IApplicationLauncher Implementation
        
        public bool CanLaunch(Application application)
        {
            if (application?.Type != ApplicationType.Desktop)
                return false;
            
            if (string.IsNullOrEmpty(application.ExecutablePath))
            {
                _logger.LogWarning("Desktop application {AppName} has empty path", application.Name);
                return false;
            }
            
            // Проверяем что файл существует и это исполняемый файл
            try
            {
                string resolvedPath = ResolveExecutablePath(application.ExecutablePath);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    _logger.LogWarning("Desktop application executable not found: {Path}", application.ExecutablePath);
                    return false;
                }
                
                var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
                var isExecutable = extension == ".exe" || extension == ".com" || extension == ".bat" || 
                                 extension == ".cmd" || extension == ".msi";
                
                if (!isExecutable)
                {
                    _logger.LogWarning("Desktop application {AppName} path is not executable: {Path}", 
                        application.Name, resolvedPath);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if can launch desktop application {AppName}: {Path}", 
                    application.Name, application.ExecutablePath);
                return false;
            }
        }
        
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (!CanLaunch(application))
            {
                var error = $"Cannot launch desktop application {application.Name}";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            var startTime = DateTime.Now;
            
            try
            {
                // КРИТИЧЕСКИ ВАЖНО: Сначала проверяем single-instance логику
                var existingInstance = await FindExistingInstanceAsync(application);
                if (existingInstance != null)
                {
                    _logger.LogInformation("Found existing instance of {AppName}, returning it instead of launching new", 
                        application.Name);
                    
                    // Возвращаем существующий экземпляр - переключение будет в ApplicationLifecycleService
                    var duration = DateTime.Now - startTime;
                    return LaunchResult.Success(existingInstance, duration);
                }
                
                _logger.LogInformation("No existing instance found, launching new: {AppName}", application.Name);
                
                // Запускаем новый процесс
                var process = await StartNewProcessAsync(application);
                if (process == null)
                {
                    return LaunchResult.Failure($"Failed to start process for {application.Name}");
                }
                
                // Создаем новый экземпляр (регистрация будет в ApplicationLifecycleService)
                var instance = await CreateApplicationInstanceAsync(application, process, launchedBy);
                
                var launchDuration = DateTime.Now - startTime;
                _logger.LogInformation("Successfully launched {AppName} in {Duration}ms", 
                    application.Name, launchDuration.TotalMilliseconds);
                
                return LaunchResult.Success(instance, launchDuration);
            }
            catch (Exception ex)
            {
                var error = $"Error launching {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Launch failed for {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
        }
        
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            if (application?.Type != ApplicationType.Desktop)
                return null;
            
            try
            {
                // ИСПРАВЛЕНИЕ: Для UWP приложений используем специальную логику поиска
                var isUwpLauncher = IsUwpLauncherApplication(application);
                
                if (isUwpLauncher)
                {
                    _logger.LogDebug("Searching for existing UWP application instance: {AppName}", application.Name);
                    
                    // Ищем уже запущенные UWP процессы
                    var candidateProcesses = await FindUwpCandidateProcessesAsync(application);
                    
                    foreach (var processId in candidateProcesses)
                    {
                        var window = await _windowManager.FindMainWindowAsync(processId);
                        if (window != null && window.IsVisible && IsCorrectUwpWindow(application, window))
                        {
                            var instance = ApplicationInstance.CreateFromProcess(application, processId, "system");
                            instance.State = ApplicationState.Running;
                            instance.MainWindow = window;
                            
                            _logger.LogInformation("Found existing UWP instance: {AppName} (PID: {ProcessId}, Title: '{Title}')", 
                                application.Name, processId, window.Title);
                            return instance;
                        }
                    }
                    
                    _logger.LogDebug("No existing UWP instance found for {AppName}", application.Name);
                    return null;
                }
                
                // Обычная логика для не-UWP приложений
                var processName = GetProcessNameFromPath(application.ExecutablePath);
                if (string.IsNullOrEmpty(processName))
                    return null;
                
                _logger.LogDebug("Searching for existing instances of {ProcessName} for current user", processName);
                
                // КРИТИЧЕСКИ ВАЖНО: Получаем только процессы текущего пользователя
                var userProcesses = await GetCurrentUserProcessesByNameAsync(processName);
                
                foreach (var processId in userProcesses)
                {
                    try
                    {
                        // Проверяем что процесс еще жив
                        if (!await _processMonitor.IsProcessAliveAsync(processId))
                            continue;
                        
                        // Получаем безопасный доступ к процессу
                        var process = await _processMonitor.GetProcessSafelyAsync(processId);
                        if (process == null)
                            continue;
                        
                        using (process)
                        {
                            // Проверяем путь исполняемого файла для точного совпадения
                            if (!IsProcessExecutableMatch(process, application.ExecutablePath))
                                continue;
                            
                            // Создаем экземпляр приложения
                            var instance = ApplicationInstance.CreateFromProcess(application, process, "system");
                            instance.State = ApplicationState.Running;
                            
                            // Ищем главное окно
                            var mainWindow = await FindMainWindowForProcessAsync(processId);
                            if (mainWindow != null)
                            {
                                instance.MainWindow = mainWindow;
                                _logger.LogInformation("Found existing instance: {AppName} (PID: {ProcessId}) with window '{WindowTitle}'", 
                                    application.Name, processId, mainWindow.Title);
                            }
                            else
                            {
                                _logger.LogDebug("Found existing instance: {AppName} (PID: {ProcessId}) without main window", 
                                    application.Name, processId);
                            }
                            
                            return instance;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error checking process {ProcessId} for existing instance", processId);
                        continue;
                    }
                }
                
                _logger.LogDebug("No existing instances found for {AppName}", application.Name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing instance of {AppName}", application.Name);
                return null;
            }
        }
        
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            return await FindMainWindowForProcessAsync(processId);
        }
        
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // Desktop приложения обычно инициализируются быстро
            return 10000; // 10 секунд
        }
        
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance?.ProcessId > 0)
                {
                    _logger.LogDebug("Cleaning up desktop application {AppName} (PID: {ProcessId})", 
                        instance.Application.Name, instance.ProcessId);
                    
                    // Для desktop приложений просто логируем - очистка будет выполнена ProcessMonitor
                    await _processMonitor.CleanupProcessAsync(instance.ProcessId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of desktop application {AppName}", 
                    instance?.Application.Name);
            }
        }

        public async Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                // Не реализуем здесь - это должно быть в ApplicationLifecycleService
                // который имеет доступ к ApplicationInstanceManager
                _logger.LogDebug("SwitchToAsync called for {InstanceId} - delegating to ApplicationLifecycleService", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to instance {InstanceId}", instanceId);
                return false;
            }
        }

        public async Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                // Не реализуем здесь - это должно быть в ApplicationLifecycleService
                _logger.LogDebug("TerminateAsync called for {InstanceId} - delegating to ApplicationLifecycleService", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating instance {InstanceId}", instanceId);
                return false;
            }
        }

        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            try
            {
                // Не реализуем здесь - это должно быть в ApplicationInstanceManager
                _logger.LogDebug("GetActiveInstancesAsync called - delegating to ApplicationInstanceManager");
                return new List<ApplicationInstance>().AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active instances");
                return new List<ApplicationInstance>().AsReadOnly();
            }
        }
        
        #endregion
        
        #region Вспомогательные методы
        
        private ProcessStartInfo CreateProcessStartInfo(Application application)
        {
            // Разрешаем путь к исполняемому файлу
            string resolvedPath = ResolveExecutablePath(application.ExecutablePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                UseShellExecute = true, // Важно для desktop приложений
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Maximized // Открываем приложения в полноэкранном режиме
            };
            
            // Добавляем аргументы если есть
            if (!string.IsNullOrEmpty(application.Arguments))
            {
                startInfo.Arguments = application.Arguments;
                _logger.LogDebug("Using arguments for {AppName}: {Arguments}", 
                    application.Name, application.Arguments);
            }
            
            // Устанавливаем рабочую директорию
            var workingDirectory = GetWorkingDirectory(application);
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
                _logger.LogDebug("Using working directory for {AppName}: {WorkingDirectory}", 
                    application.Name, workingDirectory);
            }
            
            // Настройки безопасности и среды
            startInfo.LoadUserProfile = false; // Ускоряет запуск
            startInfo.ErrorDialog = false; // Не показываем диалоги ошибок
            
            return startInfo;
        }
        
        private string GetWorkingDirectory(Application application)
        {
            try
            {
                // Используем директорию исполняемого файла как рабочую
                var directory = Path.GetDirectoryName(application.ExecutablePath);
                
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error getting working directory for {AppName}", application.Name);
                return string.Empty;
            }
        }
        
        private string GetProcessNameFromPath(string executablePath)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath))
                    return string.Empty;
                
                // Получаем имя файла без расширения
                var fileName = Path.GetFileNameWithoutExtension(executablePath);
                return fileName ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error extracting process name from path: {Path}", executablePath);
                return string.Empty;
            }
        }
        
        private string ResolveExecutablePath(string executablePath)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath))
                    return string.Empty;
                
                // Если это уже полный путь и файл существует
                if (Path.IsPathRooted(executablePath) && File.Exists(executablePath))
                {
                    return executablePath;
                }
                
                // Если это только имя файла (например, "notepad.exe"), ищем в PATH
                if (!Path.IsPathRooted(executablePath))
                {
                    // Пытаемся найти в системных папках Windows
                    var systemPaths = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.System), // C:\Windows\System32
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), // C:\Windows
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64") // C:\Windows\SysWOW64
                    };
                    
                    foreach (var systemPath in systemPaths)
                    {
                        var fullPath = Path.Combine(systemPath, executablePath);
                        if (File.Exists(fullPath))
                        {
                            _logger.LogDebug("Resolved executable path: {Original} -> {Resolved}", executablePath, fullPath);
                            return fullPath;
                        }
                    }
                    
                    // Ищем в переменной PATH
                    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    var pathDirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var dir in pathDirs)
                    {
                        try
                        {
                            var fullPath = Path.Combine(dir, executablePath);
                            if (File.Exists(fullPath))
                            {
                                _logger.LogDebug("Resolved executable path via PATH: {Original} -> {Resolved}", executablePath, fullPath);
                                return fullPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogTrace(ex, "Error checking path {Dir} for executable {Exe}", dir, executablePath);
                        }
                    }
                }
                
                // Если ничего не найдено, возвращаем исходный путь для дальнейшей обработки
                return executablePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving executable path: {Path}", executablePath);
                return executablePath;
            }
        }
        
        #region Новые методы для правильной архитектуры
        
        /// <summary>
        /// Получает SID текущего пользователя для безопасной фильтрации процессов
        /// </summary>
        private string GetCurrentUserSid()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.User?.Value ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get current user SID");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Безопасно получает процессы текущего пользователя по имени
        /// КРИТИЧЕСКИ ВАЖНО: Избегает Process.GetProcessesByName() который может возвращать процессы других пользователей
        /// </summary>
        private async Task<List<int>> GetCurrentUserProcessesByNameAsync(string processName)
        {
            var userProcessIds = new List<int>();
            
            try
            {
                // Получаем все процессы системы
                var allProcesses = Process.GetProcesses();
                
                foreach (var process in allProcesses)
                {
                    try
                    {
                        // Проверяем имя процесса
                        if (!string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        
                        // КРИТИЧЕСКИ ВАЖНО: Проверяем что процесс принадлежит текущему пользователю
                        if (!string.IsNullOrEmpty(_currentUserSid) && !IsCurrentUserProcess(process))
                        {
                            _logger.LogTrace("Skipping process {ProcessName} PID {ProcessId} - belongs to different user", 
                                processName, process.Id);
                            continue;
                        }
                        
                        userProcessIds.Add(process.Id);
                        _logger.LogTrace("Found user process: {ProcessName} PID {ProcessId}", processName, process.Id);
                    }
                    catch (Exception ex)
                    {
                        // Игнорируем процессы к которым нет доступа (другие пользователи, система)
                        _logger.LogTrace(ex, "Cannot access process {ProcessName} PID {ProcessId}", 
                            process.ProcessName, process.Id);
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
                
                _logger.LogDebug("Found {Count} user processes named {ProcessName}", userProcessIds.Count, processName);
                return userProcessIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user processes by name {ProcessName}", processName);
                return userProcessIds;
            }
        }
        
        /// <summary>
        /// Проверяет принадлежит ли процесс текущему пользователю
        /// </summary>
        private bool IsCurrentUserProcess(Process process)
        {
            try
            {
                // Простая проверка - если можем получить MainModule, скорее всего это наш процесс
                var module = process.MainModule;
                return module != null;
            }
            catch (Exception)
            {
                // Если не можем получить MainModule, скорее всего это процесс другого пользователя
                return false;
            }
        }
        
        /// <summary>
        /// Проверяет совпадает ли исполняемый файл процесса с ожидаемым путем
        /// </summary>
        private bool IsProcessExecutableMatch(Process process, string expectedPath)
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(processPath))
                    return false;
                
                var resolvedExpectedPath = ResolveExecutablePath(expectedPath);
                return string.Equals(processPath, resolvedExpectedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error checking executable match for process {ProcessId}", process.Id);
                return false;
            }
        }
        
        /// <summary>
        /// Запускает новый процесс приложения
        /// </summary>
        private async Task<Process?> StartNewProcessAsync(Application application)
        {
            try
            {
                var startInfo = CreateProcessStartInfo(application);
                var process = Process.Start(startInfo);
                
                if (process == null)
                {
                    _logger.LogError("Failed to start process for {AppName}", application.Name);
                    return null;
                }
                
                _logger.LogDebug("Started new process: {AppName} PID {ProcessId}", application.Name, process.Id);
                
                // Ждем инициализации процесса
                await Task.Delay(500);
                
                // Проверяем что процесс жив
                if (!await _processMonitor.IsProcessAliveAsync(process.Id))
                {
                    _logger.LogError("Process {AppName} exited immediately after start", application.Name);
                    process.Dispose();
                    return null;
                }
                
                return process;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new process for {AppName}", application.Name);
                return null;
            }
        }
        
        /// <summary>
        /// Создает ApplicationInstance из процесса с поиском главного окна
        /// </summary>
        private async Task<ApplicationInstance> CreateApplicationInstanceAsync(Application application, Process process, string launchedBy)
        {
            var instance = ApplicationInstance.CreateFromProcess(application, process, launchedBy);
            instance.State = ApplicationState.Starting;
            
            try
            {
                // ИСПРАВЛЕНИЕ: Проверяем является ли это UWP лаунчером (например calc.exe)
                var isUwpLauncher = IsUwpLauncherApplication(application);
                
                if (isUwpLauncher)
                {
                    _logger.LogDebug("Detected UWP launcher application: {AppName}, searching for actual UWP process", application.Name);
                    
                    // Для UWP лаунчеров ищем фактический процесс приложения
                    var uwpWindow = await FindUwpApplicationWindowAsync(application, process.Id);
                    if (uwpWindow != null)
                    {
                        // Обновляем instance с данными фактического UWP процесса
                        instance.ProcessId = (int)uwpWindow.ProcessId;
                        instance.MainWindow = uwpWindow;
                        instance.State = ApplicationState.Running;
                        _logger.LogInformation("Found UWP application window: {AppName} (Launcher PID: {LauncherPid}, App PID: {AppPid}, Title: '{Title}')", 
                            application.Name, process.Id, (int)uwpWindow.ProcessId, uwpWindow.Title);
                        
                        // Принудительно максимизируем UWP окно через Windows API
                        try
                        {
                            var maximized = await _windowManager.MaximizeWindowAsync(uwpWindow.Handle);
                            if (maximized)
                            {
                                _logger.LogDebug("Successfully maximized UWP window for {AppName}", application.Name);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to maximize UWP window for {AppName}", application.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error maximizing UWP window for {AppName}", application.Name);
                        }
                        
                        return instance;
                    }
                    else
                    {
                        _logger.LogWarning("UWP launcher {AppName} started but could not find actual application window", application.Name);
                    }
                }
                
                // Обычная логика поиска окна для не-UWP приложений
                var mainWindow = await FindMainWindowForProcessAsync(process.Id);
                if (mainWindow != null)
                {
                    instance.MainWindow = mainWindow;
                    instance.State = ApplicationState.Running;
                    _logger.LogDebug("Created instance with main window: {AppName} '{WindowTitle}'", 
                        application.Name, mainWindow.Title);
                    
                    // Принудительно максимизируем окно через Windows API
                    try
                    {
                        var maximized = await _windowManager.MaximizeWindowAsync(mainWindow.Handle);
                        if (maximized)
                        {
                            _logger.LogDebug("Successfully maximized window for {AppName}", application.Name);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to maximize window for {AppName}", application.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error maximizing window for {AppName}", application.Name);
                    }
                }
                else
                {
                    instance.State = ApplicationState.Running; // Даже без окна считаем запущенным
                    _logger.LogDebug("Created instance without main window: {AppName}", application.Name);
                }
            }
            finally
            {
                // Безопасно освобождаем Process объект
                await _processMonitor.DisposeProcessSafelyAsync(process);
            }
            
            return instance;
        }
        
        /// <summary>
        /// Ищет главное окно для процесса используя лучшие практики AppSwitcher-ов
        /// </summary>
        private async Task<WindowInfo?> FindMainWindowForProcessAsync(int processId)
        {
            try
            {
                // ИСПРАВЛЕНИЕ: Определяем тип процесса для оптимизации поиска окна
                var process = await _processMonitor.GetProcessSafelyAsync(processId);
                if (process == null)
                    return null;
                
                bool isConsoleApp = false;
                try
                {
                    using (process)
                    {
                        var processName = process.ProcessName?.ToLowerInvariant() ?? "";
                        isConsoleApp = processName == "cmd" || processName == "powershell" || processName == "pwsh" ||
                                     processName.EndsWith("console") || processName.Contains("terminal");
                    }
                }
                catch
                {
                    // Если не можем определить тип, считаем обычным приложением
                }
                
                var timeout = isConsoleApp ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(10);
                var startTime = DateTime.Now;
                var retryDelay = isConsoleApp ? 1000 : 500; // Для консоли реже проверяем
                
                _logger.LogDebug("Searching for main window: PID {ProcessId}, ConsoleApp: {IsConsole}, Timeout: {Timeout}s", 
                    processId, isConsoleApp, timeout.TotalSeconds);
                
                while (DateTime.Now - startTime < timeout)
                {
                    // Проверяем что процесс еще жив
                    if (!await _processMonitor.IsProcessAliveAsync(processId))
                    {
                        _logger.LogDebug("Process {ProcessId} exited while searching for window", processId);
                        break;
                    }
                    
                    // Для консольных приложений сразу пробуем fallback (WindowManager энумерация)
                    if (isConsoleApp)
                    {
                        var consoleWindow = await _windowManager.FindMainWindowAsync(processId);
                        if (consoleWindow != null && consoleWindow.IsVisible)
                        {
                            _logger.LogDebug("Found console window via WindowManager: PID {ProcessId}, Title '{Title}'", 
                                processId, consoleWindow.Title);
                            return consoleWindow;
                        }
                    }
                    else
                    {
                        // Для обычных приложений сначала пробуем Process API
                        var window = await TryFindWindowViaProcessApi(processId);
                        if (window != null && window.IsVisible)
                        {
                            _logger.LogDebug("Found main window via Process API: PID {ProcessId}, Title '{Title}'", 
                                processId, window.Title);
                            return window;
                        }
                    }
                    
                    await Task.Delay(retryDelay);
                }
                
                // Финальный fallback: Enumeration через WindowManager (если еще не пробовали)
                if (!isConsoleApp)
                {
                    _logger.LogDebug("Trying fallback window enumeration for PID {ProcessId}", processId);
                    return await _windowManager.FindMainWindowAsync(processId);
                }
                
                _logger.LogDebug("No main window found for PID {ProcessId} after {Timeout}s timeout", 
                    processId, timeout.TotalSeconds);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for PID {ProcessId}", processId);
                return null;
            }
        }
        
        /// <summary>
        /// Пытается найти окно через Process.MainWindowHandle
        /// </summary>
        private async Task<WindowInfo?> TryFindWindowViaProcessApi(int processId)
        {
            try
            {
                var process = await _processMonitor.GetProcessSafelyAsync(processId);
                if (process == null)
                    return null;
                
                using (process)
                {
                    if (process.HasExited)
                        return null;
                    
                    process.Refresh();
                    
                    if (process.HasExited)
                        return null;
                    
                    // Ждем инициализации UI если нужно
                    IntPtr mainWindowHandle = IntPtr.Zero;
                    try
                    {
                        mainWindowHandle = process.MainWindowHandle;
                    }
                    catch (InvalidOperationException)
                    {
                        // MainWindowHandle недоступно для консольных приложений - используем fallback
                        return null;
                    }
                    
                    if (mainWindowHandle == IntPtr.Zero)
                    {
                        try
                        {
                            if (process.WaitForInputIdle(2000))
                            {
                                process.Refresh();
                                try
                                {
                                    mainWindowHandle = process.MainWindowHandle;
                                }
                                catch (InvalidOperationException)
                                {
                                    // MainWindowHandle все еще недоступно
                                    return null;
                                }
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // WaitForInputIdle не поддерживается для консольных приложений
                            return null;
                        }
                    }
                    
                    if (process.HasExited || mainWindowHandle == IntPtr.Zero)
                        return null;
                    
                    string mainWindowTitle = string.Empty;
                    try
                    {
                        mainWindowTitle = process.MainWindowTitle ?? string.Empty;
                    }
                    catch (InvalidOperationException)
                    {
                        // MainWindowTitle недоступно для консольных приложений
                        mainWindowTitle = string.Empty;
                    }
                    
                    var windowInfo = WindowInfo.CreateDetailed(
                        mainWindowHandle,
                        mainWindowTitle,
                        string.Empty,
                        (uint)processId,
                        0,
                        true);
                    
                    // Обновляем полную информацию через WindowManager
                    return await _windowManager.RefreshWindowInfoAsync(windowInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error in TryFindWindowViaProcessApi for PID {ProcessId}", processId);
                return null;
            }
        }
        
        /// <summary>
        /// Определяет является ли приложение UWP лаунчером
        /// </summary>
        private bool IsUwpLauncherApplication(Application application)
        {
            var executablePath = application.ExecutablePath?.ToLowerInvariant() ?? "";
            var executableName = Path.GetFileName(executablePath).ToLowerInvariant();
            
            // Известные UWP лаунчеры
            var uwpLaunchers = new[]
            {
                "calc.exe",           // Калькулятор Windows 10/11
                "ms-calculator:",     // Протокол калькулятора
                "notepad.exe"         // Блокнот Windows 11 (может быть UWP)
            };
            
            return uwpLaunchers.Any(launcher => 
                executableName.Contains(launcher) || executablePath.Contains(launcher));
        }
        
        /// <summary>
        /// Ищет окно фактического UWP приложения после запуска лаунчера
        /// </summary>
        private async Task<WindowInfo?> FindUwpApplicationWindowAsync(Application application, int launcherProcessId)
        {
            try
            {
                var timeout = TimeSpan.FromSeconds(8); // UWP приложения могут дольше запускаться
                var startTime = DateTime.Now;
                
                _logger.LogDebug("Searching for UWP application window, launcher PID: {LauncherPid}", launcherProcessId);
                
                while (DateTime.Now - startTime < timeout)
                {
                    // Ищем процессы-кандидаты для UWP приложения
                    var candidateProcesses = await FindUwpCandidateProcessesAsync(application);
                    
                    foreach (var processId in candidateProcesses)
                    {
                        // Пропускаем сам лаунчер
                        if (processId == launcherProcessId)
                            continue;
                            
                        var window = await _windowManager.FindMainWindowAsync(processId);
                        if (window != null && window.IsVisible && !string.IsNullOrEmpty(window.Title))
                        {
                            // Проверяем что это правильное приложение по заголовку окна
                            if (IsCorrectUwpWindow(application, window))
                            {
                                _logger.LogDebug("Found UWP window: PID {ProcessId}, Title '{Title}'", processId, window.Title);
                                return window;
                            }
                        }
                    }
                    
                    // Ждем перед следующей попыткой
                    await Task.Delay(500);
                }
                
                _logger.LogDebug("UWP application window not found within timeout");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding UWP application window");
                return null;
            }
        }
        
        /// <summary>
        /// Находит процессы-кандидаты для UWP приложения
        /// </summary>
        private async Task<List<int>> FindUwpCandidateProcessesAsync(Application application)
        {
            var candidates = new List<int>();
            
            try
            {
                var executableName = Path.GetFileNameWithoutExtension(application.ExecutablePath ?? "").ToLowerInvariant();
                
                // Известные имена процессов для UWP приложений
                var uwpProcessNames = new List<string>();
                
                if (executableName.Contains("calc"))
                {
                    uwpProcessNames.AddRange(new[] { "calculator", "calculatorapp", "windowscalculator" });
                }
                else if (executableName.Contains("notepad"))
                {
                    uwpProcessNames.AddRange(new[] { "notepad", "texteditor", "windowsnotepad" });
                }
                
                // Также ищем общие UWP контейнеры
                uwpProcessNames.AddRange(new[] 
                { 
                    "applicationframehost",    // Основной UWP контейнер
                    "runtimebroker",          // UWP runtime
                    "wwahostnowindow"         // UWP web apps
                });
                
                // Ищем процессы по именам
                foreach (var processName in uwpProcessNames)
                {
                    var processIds = await GetCurrentUserProcessesByNameAsync(processName);
                    candidates.AddRange(processIds);
                }
                
                _logger.LogDebug("Found {Count} UWP candidate processes", candidates.Count);
                return candidates.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding UWP candidate processes");
                return candidates;
            }
        }
        
        /// <summary>
        /// Проверяет соответствует ли окно ожидаемому UWP приложению
        /// </summary>
        private bool IsCorrectUwpWindow(Application application, WindowInfo window)
        {
            var appName = application.Name?.ToLowerInvariant() ?? "";
            var windowTitle = window.Title?.ToLowerInvariant() ?? "";
            var executableName = Path.GetFileNameWithoutExtension(application.ExecutablePath ?? "").ToLowerInvariant();
            
            // Проверяем соответствие по заголовку окна
            if (executableName.Contains("calc") && 
                (windowTitle.Contains("calculator") || windowTitle.Contains("калькулятор")))
            {
                return true;
            }
            
            if (executableName.Contains("notepad") && 
                (windowTitle.Contains("notepad") || windowTitle.Contains("блокнот") || windowTitle.Contains("text")))
            {
                return true;
            }
            
            // Общая проверка по имени приложения
            if (!string.IsNullOrEmpty(appName) && windowTitle.Contains(appName))
            {
                return true;
            }
            
            return false;
        }
        
        
        #endregion
        
        #endregion
    }
}
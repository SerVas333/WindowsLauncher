using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Лаунчер для папок через проводник Windows
    /// Открывает папки в новых окнах проводника с возможностью мониторинга
    /// </summary>
    public class FolderLauncher : IApplicationLauncher
    {
        private readonly ILogger<FolderLauncher> _logger;
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        
        public ApplicationType SupportedType => ApplicationType.Folder;
        public int Priority => 5; // Низкий приоритет для папок
        
        public FolderLauncher(
            ILogger<FolderLauncher> logger,
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
            if (application?.Type != ApplicationType.Folder)
                return false;
            
            if (string.IsNullOrEmpty(application.ExecutablePath))
            {
                _logger.LogWarning("Folder application {AppName} has empty path", application.Name);
                return false;
            }
            
            var folderPath = application.ExecutablePath.Trim();
            
            // Проверяем что это существующая папка
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Folder application {AppName} path does not exist: {Path}", 
                    application.Name, folderPath);
                return false;
            }
            
            // Проверяем что у нас есть доступ к папке
            try
            {
                var _ = Directory.GetDirectories(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("No access to folder {Path} for application {AppName}", 
                    folderPath, application.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking folder access for {AppName}: {Path}", 
                    application.Name, folderPath);
                return false;
            }
        }
        
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (!CanLaunch(application))
            {
                var error = $"Cannot open folder {application.Name}";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            var startTime = DateTime.Now;
            _logger.LogInformation("Opening folder: {AppName} at {Path}", 
                application.Name, application.ExecutablePath);
            
            try
            {
                var normalizedPath = Path.GetFullPath(application.ExecutablePath);
                
                // Создаем ProcessStartInfo для запуска проводника
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{normalizedPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Maximized // Открываем проводник в полноэкранном режиме
                };
                
                // Запускаем проводник
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    var error = $"Failed to start explorer for folder {application.Name}";
                    _logger.LogError(error);
                    return LaunchResult.Failure(error);
                }
                
                _logger.LogDebug("Explorer started with PID {ProcessId} for folder {AppName}", 
                    process.Id, application.Name);
                
                // Ждем появления окна проводника
                await Task.Delay(1500);
                
                // Проверяем что процесс еще жив
                bool processAlive = await _processMonitor.IsProcessAliveAsync(process.Id);
                if (!processAlive)
                {
                    _logger.LogWarning("Explorer process {ProcessId} exited quickly for folder {AppName}", 
                        process.Id, application.Name);
                    
                    // Пытаемся найти окно проводника с нашей папкой в других процессах
                    var explorerWindow = await FindExplorerWindowWithFolderAsync(application, TimeSpan.FromSeconds(5));
                    if (explorerWindow != null)
                    {
                        var explorerProcess = await _processMonitor.GetProcessSafelyAsync((int)explorerWindow.ProcessId);
                        if (explorerProcess != null)
                        {
                            var instance = ApplicationInstance.CreateFromProcess(application, explorerProcess, launchedBy);
                            instance.State = ApplicationState.Running;
                            instance.MainWindow = explorerWindow;
                            instance.Metadata["LaunchType"] = "Folder";
                            instance.Metadata["FolderPath"] = normalizedPath;
                            
                            await _processMonitor.DisposeProcessSafelyAsync(process);
                            await _processMonitor.DisposeProcessSafelyAsync(explorerProcess);
                            
                            var duration = DateTime.Now - startTime;
                            _logger.LogInformation("Folder {AppName} opened in existing explorer window", 
                                application.Name);
                            
                            return LaunchResult.Success(instance, duration);
                        }
                    }
                    
                    var error = $"Explorer window for folder {application.Name} not found";
                    await _processMonitor.DisposeProcessSafelyAsync(process);
                    return LaunchResult.Failure(error);
                }
                
                // Создаем ApplicationInstance
                var normalInstance = ApplicationInstance.CreateFromProcess(application, process, launchedBy);
                normalInstance.State = ApplicationState.Starting;
                
                // Пытаемся найти главное окно проводника
                var mainWindow = await FindExplorerWindowAsync(process.Id, application);
                if (mainWindow != null)
                {
                    normalInstance.MainWindow = mainWindow;
                    normalInstance.State = ApplicationState.Running;
                    
                    _logger.LogDebug("Found explorer window for folder {AppName}: '{WindowTitle}'", 
                        application.Name, mainWindow.Title);
                }
                else
                {
                    _logger.LogDebug("No explorer window found for folder {AppName} yet", application.Name);
                }
                
                normalInstance.Metadata["LaunchType"] = "Folder";
                normalInstance.Metadata["FolderPath"] = normalizedPath;
                
                await _processMonitor.DisposeProcessSafelyAsync(process);
                
                var finalDuration = DateTime.Now - startTime;
                _logger.LogInformation("Successfully opened folder {AppName} in {Duration}ms", 
                    application.Name, finalDuration.TotalMilliseconds);
                
                return LaunchResult.Success(normalInstance, finalDuration);
            }
            catch (Win32Exception ex)
            {
                var error = $"Win32Exception opening folder {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Win32Exception opening folder {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
            catch (Exception ex)
            {
                var error = $"Error opening folder {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Error opening folder {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
        }
        
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            if (application?.Type != ApplicationType.Folder)
                return null;
            
            try
            {
                // Для папок ищем окна проводника с подходящим заголовком
                var explorerWindow = await FindExplorerWindowWithFolderAsync(application, TimeSpan.FromSeconds(3));
                
                if (explorerWindow != null)
                {
                    var process = await _processMonitor.GetProcessSafelyAsync((int)explorerWindow.ProcessId);
                    if (process != null)
                    {
                        var instance = ApplicationInstance.CreateFromProcess(application, process, "system");
                        instance.State = ApplicationState.Running;
                        instance.MainWindow = explorerWindow;
                        instance.Metadata["LaunchType"] = "Folder";
                        instance.Metadata["FolderPath"] = Path.GetFullPath(application.ExecutablePath);
                        
                        await _processMonitor.DisposeProcessSafelyAsync(process);
                        
                        _logger.LogDebug("Found existing folder instance for {AppName} in process {ProcessId}", 
                            application.Name, explorerWindow.ProcessId);
                        
                        return instance;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing folder instance for {AppName}", application.Name);
                return null;
            }
        }
        
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                return await FindExplorerWindowAsync(processId, application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for folder {AppName} (PID: {ProcessId})", 
                    application.Name, processId);
                return null;
            }
        }
        
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // Папки открываются в проводнике быстро
            return 8000; // 8 секунд
        }
        
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance?.ProcessId > 0)
                {
                    _logger.LogDebug("Cleaning up folder application {AppName} (PID: {ProcessId})", 
                        instance.Application.Name, instance.ProcessId);
                    
                    // Для папок обычно не требуется принудительное закрытие explorer.exe
                    // ProcessMonitor сам решит нужно ли что-то делать
                    await _processMonitor.CleanupProcessAsync(instance.ProcessId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of folder application {AppName}", 
                    instance?.Application.Name);
            }
        }

        public Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                _logger.LogDebug("Switching to folder application instance {InstanceId}", instanceId);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to folder application instance {InstanceId}", instanceId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                _logger.LogDebug("Terminating folder application instance {InstanceId} (Force: {Force})", instanceId, force);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating folder application instance {InstanceId}", instanceId);
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
                _logger.LogError(ex, "Error getting active folder application instances");
                return Task.FromResult<IReadOnlyList<ApplicationInstance>>(new List<ApplicationInstance>().AsReadOnly());
            }
        }
        
        #endregion
        
        #region Вспомогательные методы
        
        private async Task<WindowInfo?> FindExplorerWindowWithFolderAsync(Application application, TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var folderPath = Path.GetFullPath(application.ExecutablePath);
            var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
            
            _logger.LogDebug("Searching for explorer window with folder '{FolderName}' for {AppName}", 
                folderName, application.Name);
            
            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    // Ищем окна проводника по названию папки
                    var searchTerms = new[] { folderName, application.Name, Path.GetDirectoryName(folderPath) };
                    
                    foreach (var searchTerm in searchTerms)
                    {
                        if (string.IsNullOrEmpty(searchTerm))
                            continue;
                        
                        var window = await _windowManager.FindWindowByTitleAsync(searchTerm, exactMatch: false);
                        if (window != null && window.IsVisible && IsExplorerWindow(window))
                        {
                            _logger.LogDebug("Found explorer window with title containing '{SearchTerm}': '{WindowTitle}'", 
                                searchTerm, window.Title);
                            return window;
                        }
                    }
                    
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error during explorer window search");
                    await Task.Delay(300);
                }
            }
            
            _logger.LogDebug("No explorer window found for folder {AppName} within timeout", application.Name);
            return null;
        }
        
        private async Task<WindowInfo?> FindExplorerWindowAsync(int processId, Application application)
        {
            try
            {
                var folderPath = Path.GetFullPath(application.ExecutablePath);
                var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
                
                // Сначала ищем по названию папки среди окон процесса
                var window = await _windowManager.FindMainWindowAsync(processId, expectedTitle: folderName);
                if (window != null && window.IsVisible)
                {
                    return window;
                }
                
                // Затем ищем по имени приложения
                window = await _windowManager.FindMainWindowAsync(processId, expectedTitle: application.Name);
                if (window != null && window.IsVisible)
                {
                    return window;
                }
                
                // В конце любое видимое окно процесса
                var allWindows = await _windowManager.GetAllWindowsForProcessAsync(processId);
                var explorerWindow = allWindows.FirstOrDefault(w => w.IsVisible && IsExplorerWindow(w));
                
                return explorerWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding explorer window for process {ProcessId}", processId);
                return null;
            }
        }
        
        private bool IsExplorerWindow(WindowInfo window)
        {
            // Проверяем что это окно проводника
            return window.ClassName.ToLowerInvariant().Contains("cabinetwclass") || 
                   window.ClassName.ToLowerInvariant().Contains("explorerframe") ||
                   window.Title.ToLowerInvariant().Contains("explorer") ||
                   (window.ProcessId > 0 && IsExplorerProcess((int)window.ProcessId));
        }
        
        private bool IsExplorerProcess(int processId)
        {
            try
            {
                var processName = _processMonitor.GetProcessNameAsync(processId).Result;
                return string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
    }
}
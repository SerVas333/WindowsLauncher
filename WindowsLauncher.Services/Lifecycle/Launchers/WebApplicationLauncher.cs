using System;
using System.Collections.Generic;
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
    /// Лаунчер для web приложений через браузер по умолчанию
    /// Поддерживает запуск URL в системном браузере с возможностью мониторинга
    /// </summary>
    public class WebApplicationLauncher : IApplicationLauncher
    {
        private readonly ILogger<WebApplicationLauncher> _logger;
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        
        public ApplicationType SupportedType => ApplicationType.Web;
        public int Priority => 10; // Средний приоритет - fallback для WebView2ApplicationLauncher
        
        public WebApplicationLauncher(
            ILogger<WebApplicationLauncher> logger,
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
            if (application?.Type != ApplicationType.Web)
                return false;
            
            if (string.IsNullOrEmpty(application.ExecutablePath))
            {
                _logger.LogWarning("Web application {AppName} has empty path", application.Name);
                return false;
            }
            
            var url = application.ExecutablePath.Trim();
            
            // Проверяем что это валидный URL
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // Проверяем относительные URL (могут начинаться с доменного имени)
            if (Uri.TryCreate($"https://{url}", UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString())
            {
                return true;
            }
            
            _logger.LogWarning("Web application {AppName} has invalid URL format: {Path}", 
                application.Name, application.ExecutablePath);
            return false;
        }
        
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (!CanLaunch(application))
            {
                var error = $"Cannot launch web application {application.Name}";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            var startTime = DateTime.Now;
            _logger.LogInformation("Launching web application: {AppName} at {Url}", 
                application.Name, application.ExecutablePath);
            
            try
            {
                // Нормализуем URL
                var normalizedUrl = NormalizeUrl(application.ExecutablePath);
                
                // Создаем ProcessStartInfo для запуска через системный браузер
                var startInfo = new ProcessStartInfo
                {
                    FileName = normalizedUrl,
                    UseShellExecute = true, // Важно для запуска через браузер по умолчанию
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                
                // Запускаем процесс
                var process = Process.Start(startInfo);
                
                // Для web приложений Process.Start может вернуть null если используется существующий браузер
                if (process == null)
                {
                    _logger.LogDebug("Process.Start returned null for web app {AppName} - likely using existing browser", 
                        application.Name);
                    
                    // Пытаемся найти окно браузера с нашим URL
                    var browserWindow = await FindBrowserWindowWithUrlAsync(application, TimeSpan.FromSeconds(10));
                    
                    if (browserWindow != null)
                    {
                        // Создаем ApplicationInstance с процессом браузера
                        var browserProcess = await _processMonitor.GetProcessSafelyAsync((int)browserWindow.ProcessId);
                        if (browserProcess != null)
                        {
                            var instance = ApplicationInstance.CreateFromProcess(application, browserProcess, launchedBy);
                            instance.State = ApplicationState.Running;
                            instance.MainWindow = browserWindow;
                            instance.Metadata["LaunchType"] = "WebApplication";
                            instance.Metadata["BrowserUrl"] = normalizedUrl;
                            instance.Metadata["WindowTitle"] = browserWindow.Title;
                            
                            await _processMonitor.DisposeProcessSafelyAsync(browserProcess);
                            
                            var duration = DateTime.Now - startTime;
                            _logger.LogInformation("Web application {AppName} opened in existing browser window", 
                                application.Name);
                            
                            return LaunchResult.Success(instance, duration);
                        }
                    }
                    
                    // Если не нашли окно, создаем "виртуальный" экземпляр
                    var virtualInstance = CreateVirtualWebInstance(application, launchedBy, normalizedUrl);
                    var virtualDuration = DateTime.Now - startTime;
                    
                    _logger.LogInformation("Web application {AppName} launched (virtual instance)", application.Name);
                    return LaunchResult.Success(virtualInstance, virtualDuration);
                }
                
                _logger.LogDebug("Web application {AppName} started new browser process with PID {ProcessId}", 
                    application.Name, process.Id);
                
                // Ждем появления окна браузера
                await Task.Delay(2000);
                
                // Проверяем что процесс еще жив
                bool processAlive = await _processMonitor.IsProcessAliveAsync(process.Id);
                if (!processAlive)
                {
                    _logger.LogWarning("Browser process {ProcessId} for web app {AppName} exited quickly", 
                        process.Id, application.Name);
                    
                    // Пытаемся найти окно браузера с нашим URL в других процессах
                    var browserWindow = await FindBrowserWindowWithUrlAsync(application, TimeSpan.FromSeconds(5));
                    if (browserWindow != null)
                    {
                        var browserProcess = await _processMonitor.GetProcessSafelyAsync((int)browserWindow.ProcessId);
                        if (browserProcess != null)
                        {
                            var instance = ApplicationInstance.CreateFromProcess(application, browserProcess, launchedBy);
                            instance.State = ApplicationState.Running;
                            instance.MainWindow = browserWindow;
                            instance.Metadata["LaunchType"] = "WebApplication";
                            instance.Metadata["BrowserUrl"] = normalizedUrl;
                            
                            await _processMonitor.DisposeProcessSafelyAsync(process);
                            await _processMonitor.DisposeProcessSafelyAsync(browserProcess);
                            
                            var duration = DateTime.Now - startTime;
                            return LaunchResult.Success(instance, duration);
                        }
                    }
                    
                    // Создаем виртуальный экземпляр если не нашли
                    await _processMonitor.DisposeProcessSafelyAsync(process);
                    var virtualInstance = CreateVirtualWebInstance(application, launchedBy, normalizedUrl);
                    var virtualDuration = DateTime.Now - startTime;
                    
                    return LaunchResult.Success(virtualInstance, virtualDuration);
                }
                
                // Создаем ApplicationInstance с новым процессом браузера
                var normalInstance = ApplicationInstance.CreateFromProcess(application, process, launchedBy);
                normalInstance.State = ApplicationState.Starting;
                
                // Пытаемся найти главное окно
                var mainWindow = await FindBrowserWindowAsync(process.Id, application);
                if (mainWindow != null)
                {
                    normalInstance.MainWindow = mainWindow;
                    normalInstance.State = ApplicationState.Running;
                    normalInstance.Metadata["WindowTitle"] = mainWindow.Title;
                }
                
                normalInstance.Metadata["LaunchType"] = "WebApplication";
                normalInstance.Metadata["BrowserUrl"] = normalizedUrl;
                
                await _processMonitor.DisposeProcessSafelyAsync(process);
                
                var finalDuration = DateTime.Now - startTime;
                _logger.LogInformation("Successfully launched web application {AppName} in {Duration}ms", 
                    application.Name, finalDuration.TotalMilliseconds);
                
                return LaunchResult.Success(normalInstance, finalDuration);
            }
            catch (Win32Exception ex)
            {
                var error = $"Win32Exception launching web application {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Win32Exception launching web application {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
            catch (Exception ex)
            {
                var error = $"Error launching web application {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Error launching web application {AppName}", application.Name);
                return LaunchResult.Failure(ex, error);
            }
        }
        
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            if (application?.Type != ApplicationType.Web)
                return null;
            
            try
            {
                // Для web приложений ищем окна браузеров с подходящим заголовком
                var browserWindow = await FindBrowserWindowWithUrlAsync(application, TimeSpan.FromSeconds(5));
                
                if (browserWindow != null)
                {
                    var process = await _processMonitor.GetProcessSafelyAsync((int)browserWindow.ProcessId);
                    if (process != null)
                    {
                        var instance = ApplicationInstance.CreateFromProcess(application, process, "system");
                        instance.State = ApplicationState.Running;
                        instance.MainWindow = browserWindow;
                        instance.Metadata["LaunchType"] = "WebApplication";
                        instance.Metadata["BrowserUrl"] = NormalizeUrl(application.ExecutablePath);
                        
                        await _processMonitor.DisposeProcessSafelyAsync(process);
                        
                        _logger.LogDebug("Found existing web application instance for {AppName} in process {ProcessId}", 
                            application.Name, browserWindow.ProcessId);
                        
                        return instance;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing web application instance for {AppName}", application.Name);
                return null;
            }
        }
        
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                return await FindBrowserWindowAsync(processId, application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for web application {AppName} (PID: {ProcessId})", 
                    application.Name, processId);
                return null;
            }
        }
        
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // Web приложения могут требовать время для загрузки страницы
            return 12000; // 12 секунд
        }
        
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance?.ProcessId > 0)
                {
                    _logger.LogDebug("Cleaning up web application {AppName} (PID: {ProcessId})", 
                        instance.Application.Name, instance.ProcessId);
                    
                    await _processMonitor.CleanupProcessAsync(instance.ProcessId);
                }
                else if (instance?.IsVirtual == true)
                {
                    _logger.LogDebug("Cleaning up virtual web application instance {AppName}", 
                        instance.Application.Name);
                    // Виртуальные экземпляры не требуют очистки процессов
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of web application {AppName}", 
                    instance?.Application.Name);
            }
        }

        public async Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                _logger.LogDebug("Switching to web application instance {InstanceId}", instanceId);
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to web application instance {InstanceId}", instanceId);
                return false;
            }
        }

        public async Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                _logger.LogDebug("Terminating web application instance {InstanceId} (Force: {Force})", instanceId, force);
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating web application instance {InstanceId}", instanceId);
                return false;
            }
        }

        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            try
            {
                return await Task.FromResult(new List<ApplicationInstance>().AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active web application instances");
                return new List<ApplicationInstance>().AsReadOnly();
            }
        }
        
        #endregion
        
        #region Вспомогательные методы
        
        private string NormalizeUrl(string url)
        {
            var trimmedUrl = url.Trim();
            
            // Если уже содержит схему, возвращаем как есть
            if (trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmedUrl.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                trimmedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedUrl;
            }
            
            // Добавляем https:// по умолчанию
            return $"https://{trimmedUrl}";
        }
        
        private ApplicationInstance CreateVirtualWebInstance(Application application, string launchedBy, string url)
        {
            // Создаем "виртуальный" экземпляр для web приложений которые открылись в существующем браузере
            var virtualInstance = new ApplicationInstance
            {
                InstanceId = ApplicationInstance.GenerateInstanceId(application.Id, 0),
                Application = application,
                ProcessId = 0, // Специальное значение для виртуальных экземпляров
                LaunchedBy = launchedBy,
                StartTime = DateTime.Now,
                State = ApplicationState.Running,
                IsVirtual = true,
                Metadata = new Dictionary<string, object>
                {
                    ["LaunchType"] = "WebApplication",
                    ["BrowserUrl"] = url,
                    ["IsVirtual"] = true
                }
            };
            
            return virtualInstance;
        }
        
        private async Task<WindowInfo?> FindBrowserWindowWithUrlAsync(Application application, TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var url = NormalizeUrl(application.ExecutablePath);
            
            _logger.LogDebug("Searching for browser window with URL pattern for {AppName}", application.Name);
            
            while (DateTime.Now - startTime < timeout)
            {
                try
                {
                    // Получаем доменное имя из URL для поиска
                    var searchTerms = ExtractSearchTermsFromUrl(url, application.Name);
                    
                    foreach (var searchTerm in searchTerms)
                    {
                        var window = await _windowManager.FindWindowByTitleAsync(searchTerm, exactMatch: false);
                        if (window != null && window.IsVisible && IsBrowserWindow(window))
                        {
                            _logger.LogDebug("Found browser window with title containing '{SearchTerm}': '{WindowTitle}'", 
                                searchTerm, window.Title);
                            return window;
                        }
                    }
                    
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error during browser window search");
                    await Task.Delay(500);
                }
            }
            
            _logger.LogDebug("No browser window found for web app {AppName} within timeout", application.Name);
            return null;
        }
        
        private async Task<WindowInfo?> FindBrowserWindowAsync(int processId, Application application)
        {
            try
            {
                var url = NormalizeUrl(application.ExecutablePath);
                var searchTerms = ExtractSearchTermsFromUrl(url, application.Name);
                
                // Сначала ищем по заголовку среди окон процесса
                foreach (var searchTerm in searchTerms)
                {
                    var window = await _windowManager.FindMainWindowAsync(processId, expectedTitle: searchTerm);
                    if (window != null && window.IsVisible)
                    {
                        return window;
                    }
                }
                
                // Затем любое видимое окно процесса
                var allWindows = await _windowManager.GetAllWindowsForProcessAsync(processId);
                var visibleWindow = allWindows.FirstOrDefault(w => w.IsVisible && !string.IsNullOrEmpty(w.Title));
                
                return visibleWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding browser window for process {ProcessId}", processId);
                return null;
            }
        }
        
        private List<string> ExtractSearchTermsFromUrl(string url, string appName)
        {
            var searchTerms = new List<string>();
            
            // Добавляем имя приложения
            if (!string.IsNullOrEmpty(appName))
            {
                searchTerms.Add(appName);
            }
            
            try
            {
                var uri = new Uri(url);
                
                // Добавляем домен
                searchTerms.Add(uri.Host);
                
                // Добавляем домен без поддомена (например, google.com вместо www.google.com)
                var hostParts = uri.Host.Split('.');
                if (hostParts.Length >= 2)
                {
                    var mainDomain = string.Join(".", hostParts.Skip(Math.Max(0, hostParts.Length - 2)));
                    searchTerms.Add(mainDomain);
                }
                
                // Добавляем путь если есть
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                {
                    var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                    if (pathParts.Length > 0 && !string.IsNullOrEmpty(pathParts[0]))
                    {
                        searchTerms.Add(pathParts[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error extracting search terms from URL: {Url}", url);
            }
            
            return searchTerms.Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }
        
        private bool IsBrowserWindow(WindowInfo window)
        {
            // Проверяем что это окно браузера по имени процесса
            var browserProcessNames = new[]
            {
                "chrome", "firefox", "msedge", "iexplore", "safari", "opera", "brave", "vivaldi"
            };
            
            // Можно расширить проверку по названию окна или классу
            return browserProcessNames.Any(name => 
                window.Title.ToLowerInvariant().Contains(name)) || 
                window.ClassName.ToLowerInvariant().Contains("browser");
        }
        
        #endregion
    }
}
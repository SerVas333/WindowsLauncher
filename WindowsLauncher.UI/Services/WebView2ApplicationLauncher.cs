using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Components.WebView2;
using WindowsLauncher.Core.Interfaces.UI;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Лаунчер для запуска веб-приложений через WebView2
    /// Заменяет ChromeAppLauncher для лучшего контроля жизненного цикла
    /// </summary>
    public class WebView2ApplicationLauncher : IApplicationLauncher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WebView2ApplicationLauncher> _logger;
        private readonly Dictionary<string, IWebView2Window> _activeWindows;

        // События для интеграции с ApplicationLifecycleService
        public event EventHandler<ApplicationInstance>? WindowActivated;
        public event EventHandler<ApplicationInstance>? WindowDeactivated;
        public event EventHandler<ApplicationInstance>? WindowClosed;
        public event EventHandler<ApplicationInstance>? WindowStateChanged;

        public WebView2ApplicationLauncher(
            IServiceProvider serviceProvider,
            ILogger<WebView2ApplicationLauncher> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeWindows = new Dictionary<string, IWebView2Window>();
        }

        #region IApplicationLauncher Properties

        /// <summary>
        /// Тип приложений, которые поддерживает данный лаунчер
        /// Основной тип ChromeApp, но также поддерживает Web
        /// </summary>
        public ApplicationType SupportedType => ApplicationType.ChromeApp;

        /// <summary>
        /// Приоритет лаунчера (высший приоритет для современных веб-приложений)
        /// </summary>
        public int Priority => 25; // Высший приоритет - заменяет старый ChromeAppLauncher

        #endregion

        /// <summary>
        /// Проверяет, может ли этот лаунчер запустить указанное приложение
        /// </summary>
        public bool CanLaunch(Application application)
        {
            if (application == null)
                return false;

            // Поддерживаем как ChromeApp, так и Web приложения
            if (application.Type == ApplicationType.ChromeApp || application.Type == ApplicationType.Web)
            {
                var url = GetApplicationUrl(application);
                
                // Для ChromeApp с chrome.exe путем всегда можем запустить
                if (application.Type == ApplicationType.ChromeApp && 
                    application.ExecutablePath?.Contains("chrome.exe", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogTrace("WebView2ApplicationLauncher can launch ChromeApp {AppName} (Chrome path: {Path})", 
                        application.Name, application.ExecutablePath);
                    return true;
                }
                
                // Для прямых URL проверяем валидность
                if (!string.IsNullOrEmpty(url) && IsValidWebUrl(url))
                {
                    _logger.LogTrace("WebView2ApplicationLauncher can launch {AppName} (Type: {Type}, URL: {Url})", 
                        application.Name, application.Type, url);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Запускает приложение через WebView2
        /// </summary>
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));
            
            if (string.IsNullOrWhiteSpace(launchedBy))
                throw new ArgumentException("LaunchedBy cannot be null or empty", nameof(launchedBy));

            if (!CanLaunch(application))
            {
                var error = $"WebView2ApplicationLauncher cannot launch application {application.Name} (Type: {application.Type})";
                _logger.LogWarning(error);
                return LaunchResult.Failure(error);
            }

            var startTime = DateTime.Now;
            var instanceId = GenerateInstanceId(application);

            try
            {
                _logger.LogInformation("Launching WebView2 application {AppName} (Instance: {InstanceId}) by user {LaunchedBy}", 
                    application.Name, instanceId, launchedBy);

                // Создаем окно WebView2
                var window = await CreateWebView2WindowAsync(application, instanceId, launchedBy);
                
                if (window == null)
                {
                    var error = $"Failed to create WebView2 window for {application.Name}";
                    _logger.LogError(error);
                    return LaunchResult.Failure(error);
                }

                // Регистрируем окно
                _activeWindows[instanceId] = window;
                
                // Подписываемся на события окна
                SubscribeToWindowEvents(window);

                // Показываем окно
                window.Show();
                window.Activate();

                var launchDuration = DateTime.Now - startTime;
                
                // Создаем ApplicationInstance для регистрации в системе
                var instance = new ApplicationInstance
                {
                    InstanceId = instanceId,
                    Application = application,
                    ProcessId = Environment.ProcessId, // WebView2 работает в текущем процессе
                    StartTime = startTime,
                    State = ApplicationState.Running,
                    LaunchedBy = launchedBy,
                    IsVirtual = false,
                    IsActive = true,
                    LastUpdate = DateTime.Now
                };
                
                // Уведомляем о запуске через событие (для AppSwitcher интеграции)
                WindowActivated?.Invoke(this, instance);
                
                _logger.LogInformation("Successfully launched WebView2 application {AppName} (Instance: {InstanceId}) in {Duration}ms", 
                    application.Name, instanceId, launchDuration.TotalMilliseconds);

                return LaunchResult.Success(instanceId, launchDuration);
            }
            catch (Exception ex)
            {
                var error = $"Error launching WebView2 application {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Failed to launch WebView2 application {AppName} (Instance: {InstanceId})", 
                    application.Name, instanceId);

                // Очищаем регистрацию при ошибке
                _activeWindows.Remove(instanceId);

                return LaunchResult.Failure(error);
            }
        }

        /// <summary>
        /// Переключиться на указанный экземпляр приложения
        /// </summary>
        public async Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                if (_activeWindows.TryGetValue(instanceId, out var window))
                {
                    return await window.SwitchToAsync();
                }

                _logger.LogWarning("WebView2 window not found for instance {InstanceId}", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to WebView2 instance {InstanceId}", instanceId);
                return false;
            }
        }

        /// <summary>
        /// Закрыть указанный экземпляр приложения
        /// </summary>
        public async Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                if (_activeWindows.TryGetValue(instanceId, out var window))
                {
                    _logger.LogInformation("Terminating WebView2 application instance {InstanceId} (Force: {Force})", 
                        instanceId, force);

                    // Закрываем окно
                    window.Close();

                    // Удаляем из коллекции
                    _activeWindows.Remove(instanceId);

                    return true;
                }

                _logger.LogWarning("WebView2 window not found for termination: {InstanceId}", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating WebView2 instance {InstanceId}", instanceId);
                return false;
            }
        }

        /// <summary>
        /// Получить все активные экземпляры
        /// </summary>
        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            await Task.CompletedTask;

            var instances = new List<ApplicationInstance>();

            foreach (var kvp in _activeWindows.ToList())
            {
                try
                {
                    var window = kvp.Value;
                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId, // WebView2 работает в текущем процессе
                        StartTime = window.StartTime,
                        State = ApplicationState.Running, // TODO: Определить более точное состояние
                        LaunchedBy = window.LaunchedBy
                    };

                    instances.Add(instance);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting instance info for {InstanceId}", kvp.Key);
                }
            }

            return instances;
        }

        /// <summary>
        /// Найти главное окно для запущенного процесса приложения
        /// Для WebView2 это не применимо, так как мы управляем WPF окнами напрямую
        /// </summary>
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                // Для WebView2 приложений мы управляем окнами напрямую
                // Ищем среди активных окон
                foreach (var kvp in _activeWindows)
                {
                    var window = kvp.Value;
                    if (window.Application.Id == application.Id && !window.IsClosed)
                    {
                        // Создаем WindowInfo из WPF окна
                        var wpfWindow = window as System.Windows.Window;
                        if (wpfWindow != null)
                        {
                            var windowInfo = new WindowInfo
                            {
                                Handle = new System.Windows.Interop.WindowInteropHelper(wpfWindow).Handle,
                                ProcessId = (uint)Environment.ProcessId, // WebView2 работает в текущем процессе
                                Title = window.WindowTitle,
                                IsVisible = window.IsVisible,
                                ClassName = "WebView2ApplicationWindow"
                            };
                        
                            _logger.LogDebug("Found WebView2 main window for {AppName}: '{Title}'", 
                                application.Name, windowInfo.Title);
                            return windowInfo;
                        }
                    }
                }

                _logger.LogDebug("No WebView2 window found for {AppName} with process ID {ProcessId}", 
                    application.Name, processId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for WebView2 app {AppName} (PID: {ProcessId})", 
                    application.Name, processId);
                return null;
            }
        }

        /// <summary>
        /// Найти существующий экземпляр приложения
        /// </summary>
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            try
            {
                if (!CanLaunch(application))
                    return null;

                // Ищем среди активных окон
                foreach (var kvp in _activeWindows)
                {
                    var window = kvp.Value;
                    if (window.Application.Id == application.Id && !window.IsClosed)
                    {
                        // Создаем ApplicationInstance из активного окна
                        var instance = new ApplicationInstance
                        {
                            InstanceId = window.InstanceId,
                            Application = application,
                            ProcessId = Environment.ProcessId, // WebView2 работает в текущем процессе
                            StartTime = window.StartTime,
                            State = ApplicationState.Running,
                            LaunchedBy = window.LaunchedBy,
                            IsVirtual = false
                        };

                        _logger.LogDebug("Found existing WebView2 instance for {AppName} (Instance: {InstanceId})", 
                            application.Name, instance.InstanceId);
                        return instance;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing WebView2 instance for {AppName}", application.Name);
                return null;
            }
        }

        /// <summary>
        /// Получить время ожидания инициализации окна для данного типа приложений
        /// </summary>
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // WebView2 приложения требуют времени для инициализации WebView2 и загрузки страницы
            return 20000; // 20 секунд
        }

        /// <summary>
        /// Выполнить специфичную для WebView2 очистку ресурсов
        /// </summary>
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance == null)
                    return;

                _logger.LogDebug("Cleaning up WebView2 application {AppName} (Instance: {InstanceId})", 
                    instance.Application.Name, instance.InstanceId);

                // Ищем и закрываем соответствующее окно
                if (_activeWindows.TryGetValue(instance.InstanceId, out var window))
                {
                    UnsubscribeFromWindowEvents(window);
                    window.Close();
                    _activeWindows.Remove(instance.InstanceId);
                    
                    _logger.LogDebug("WebView2 window closed and cleaned up for {AppName}", 
                        instance.Application.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of WebView2 application {AppName}", 
                    instance?.Application.Name);
            }
        }

        #region Вспомогательные методы

        /// <summary>
        /// Создает окно WebView2 для приложения
        /// </summary>
        private async Task<IWebView2Window?> CreateWebView2WindowAsync(
            Application application, string instanceId, string launchedBy)
        {
            try
            {
                // Получаем logger для окна из DI
                var windowLogger = _serviceProvider.GetRequiredService<ILogger<WebView2ApplicationWindow>>();

                // Создаем окно
                var window = new WebView2ApplicationWindow(application, instanceId, launchedBy, windowLogger);

                _logger.LogDebug("Created WebView2 window for {AppName} (Instance: {InstanceId})", 
                    application.Name, instanceId);

                return window;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating WebView2 window for {AppName}", application.Name);
                return null;
            }
        }

        /// <summary>
        /// Подписывается на события окна для интеграции с жизненным циклом
        /// </summary>
        private void SubscribeToWindowEvents(IWebView2Window window)
        {
            window.WindowClosed += OnWindowClosed;
            window.WindowActivated += OnWindowActivated;
            window.WindowDeactivated += OnWindowDeactivated;
            window.WindowStateChanged += OnWindowStateChanged;
        }

        /// <summary>
        /// Отписывается от событий окна
        /// </summary>
        private void UnsubscribeFromWindowEvents(IWebView2Window window)
        {
            window.WindowClosed -= OnWindowClosed;
            window.WindowActivated -= OnWindowActivated;
            window.WindowDeactivated -= OnWindowDeactivated;
            window.WindowStateChanged -= OnWindowStateChanged;
        }

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        private void OnWindowClosed(object? sender, ApplicationInstance instance)
        {
            try
            {
                _logger.LogDebug("WebView2 window closed for {AppName} (Instance: {InstanceId})", 
                    instance.Application?.Name, instance.InstanceId);

                // Удаляем из коллекции активных окон
                if (_activeWindows.TryGetValue(instance.InstanceId, out var window))
                {
                    UnsubscribeFromWindowEvents(window);
                    _activeWindows.Remove(instance.InstanceId);
                }

                // Уведомляем ApplicationLifecycleService о закрытии через событие
                WindowClosed?.Invoke(this, instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window closed event for {InstanceId}", instance.InstanceId);
            }
        }

        private void OnWindowActivated(object? sender, ApplicationInstance instance)
        {
            _logger.LogTrace("WebView2 window activated for {AppName} (Instance: {InstanceId})", 
                instance.Application?.Name, instance.InstanceId);
            
            // Уведомляем ApplicationLifecycleService об активации
            WindowActivated?.Invoke(this, instance);
        }

        private void OnWindowDeactivated(object? sender, ApplicationInstance instance)
        {
            _logger.LogTrace("WebView2 window deactivated for {AppName} (Instance: {InstanceId})", 
                instance.Application?.Name, instance.InstanceId);
            
            // Уведомляем ApplicationLifecycleService о деактивации
            WindowDeactivated?.Invoke(this, instance);
        }

        private void OnWindowStateChanged(object? sender, ApplicationInstance instance)
        {
            _logger.LogTrace("WebView2 window state changed for {AppName} (Instance: {InstanceId})", 
                instance.Application?.Name, instance.InstanceId);
            
            // Уведомляем ApplicationLifecycleService об изменении состояния
            WindowStateChanged?.Invoke(this, instance);
        }

        /// <summary>
        /// Получает URL для запуска из приложения согласно соглашениям проекта
        /// </summary>
        private string GetApplicationUrl(Application application)
        {
            if (application.Type == ApplicationType.Web)
            {
                // Для Web приложений ExecutablePath содержит URL
                return application.ExecutablePath ?? "";
            }
            
            if (application.Type == ApplicationType.ChromeApp)
            {
                // Для ChromeApp приоритет: Arguments с --app=URL
                var args = application.Arguments ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(args, @"--app=([^\s]+)");
                if (match.Success)
                {
                    var url = match.Groups[1].Value;
                    _logger.LogDebug("Extracted URL from ChromeApp arguments: {Url}", url);
                    return url;
                }
                
                // Если ExecutablePath содержит URL (legacy поддержка)
                var path = application.ExecutablePath ?? "";
                if (IsValidWebUrl(path))
                {
                    _logger.LogDebug("Using ExecutablePath as URL for ChromeApp: {Url}", path);
                    return path;
                }
                
                // Если ExecutablePath = chrome.exe но нет --app в Arguments
                if (path.Contains("chrome.exe", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("ChromeApp with chrome.exe path but no --app argument, using about:blank");
                    return "about:blank";
                }
                
                return path;
            }

            return "";
        }

        /// <summary>
        /// Проверяет, является ли URL валидным веб-адресом
        /// </summary>
        private bool IsValidWebUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // Поддерживаем специальные URL для WebView2
            if (url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                return true;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Генерирует уникальный ID экземпляра
        /// </summary>
        private string GenerateInstanceId(Application application)
        {
            return $"webview2_{application.Id}_{Guid.NewGuid():N}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _logger.LogDebug("Disposing WebView2ApplicationLauncher with {Count} active windows", 
                    _activeWindows.Count);

                // Закрываем все активные окна
                foreach (var kvp in _activeWindows.ToList())
                {
                    try
                    {
                        var window = kvp.Value;
                        UnsubscribeFromWindowEvents(window);
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing WebView2 window {InstanceId} during disposal", kvp.Key);
                    }
                }

                _activeWindows.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing WebView2ApplicationLauncher");
            }
        }

        #endregion
    }
}
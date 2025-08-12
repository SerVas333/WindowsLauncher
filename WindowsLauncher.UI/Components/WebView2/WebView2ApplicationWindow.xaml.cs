using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Interfaces.UI;
using WindowsLauncher.Core.Enums;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Components.WebView2
{
    /// <summary>
    /// WebView2 окно для веб-приложений с полным контролем жизненного цикла
    /// </summary>
    public partial class WebView2ApplicationWindow : Window, INotifyPropertyChanged, IWebView2Window
    {
        private readonly ILogger<WebView2ApplicationWindow> _logger;
        private readonly CoreApplication _application;
        private bool _isClosedByUser = false;
        private bool _isInitialized = false;

        // Backing fields для привязки данных
        private string _windowTitle = "Web Application";
        private string _applicationName = "Web Application";
        private string _applicationIcon = "🌐";
        private bool _showApplicationHeader = true;
        private bool _showStatusBar = true;
        private string _statusText = "Готов";
        private bool _isLoading = false;
        private string _connectionStatus = "●";
        private Brush _connectionStatusColor = Brushes.Green;

        // События для интеграции с ApplicationLifecycleService
        public event EventHandler<ApplicationInstance>? WindowActivated;
        public event EventHandler<ApplicationInstance>? WindowDeactivated;
        public event EventHandler<ApplicationInstance>? WindowClosed;
        public event EventHandler<ApplicationInstance>? WindowStateChanged;

        // Свойства экземпляра приложения
        public string InstanceId { get; }
        public DateTime StartTime { get; }
        public string LaunchedBy { get; }
        
        // Реализация IWebView2Window
        public CoreApplication Application => _application;
        public bool IsClosed { get; private set; } = false;

        public WebView2ApplicationWindow(
            CoreApplication application, 
            string instanceId, 
            string launchedBy,
            ILogger<WebView2ApplicationWindow> logger)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InstanceId = instanceId;
            LaunchedBy = launchedBy;
            StartTime = DateTime.Now;

            InitializeComponent();
            InitializeWindow();
            DataContext = this;
            
            // Устанавливаем заголовок после инициализации
            Title = _application.Name;

            _logger.LogDebug("WebView2ApplicationWindow created for {AppName} (Instance: {InstanceId})", 
                application.Name, instanceId);
        }

        #region Свойства для привязки данных

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public string ApplicationName
        {
            get => _applicationName;
            set => SetProperty(ref _applicationName, value);
        }

        public string ApplicationIcon
        {
            get => _applicationIcon;
            set => SetProperty(ref _applicationIcon, value);
        }

        public bool ShowApplicationHeader
        {
            get => _showApplicationHeader;
            set => SetProperty(ref _showApplicationHeader, value);
        }

        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set => SetProperty(ref _showStatusBar, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public Brush ConnectionStatusColor
        {
            get => _connectionStatusColor;
            set => SetProperty(ref _connectionStatusColor, value);
        }

        public CoreWebView2CreationProperties? WebViewCreationProperties { get; private set; }

        #endregion

        #region Инициализация

        private void InitializeWindow()
        {
            // Настройка свойств окна из Application
            ApplicationName = _application.Name;
            WindowTitle = _application.Name;
            ApplicationIcon = !string.IsNullOrEmpty(_application.IconText) ? _application.IconText : "🌐";

            // Настройка WebView2 Creation Properties
            WebViewCreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WindowsLauncher",
                    "WebView2",
                    InstanceId)
            };

            // Подписка на события окна
            Activated += OnWindowActivated;
            Deactivated += OnWindowDeactivated;
            StateChanged += OnWindowStateChanged;
            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize WebView2 for {AppName}", _application.Name);
                StatusText = $"Ошибка инициализации: {ex.Message}";
                ConnectionStatusColor = Brushes.Red;
                ConnectionStatus = "●";
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                _logger.LogDebug("Initializing WebView2 for {AppName}", _application.Name);
                
                // Ждем инициализации WebView2 (если еще не инициализирован)
                await WebView.EnsureCoreWebView2Async();

                // Настройка WebView2
                ConfigureWebView2();

                // Навигация к URL
                var url = GetNavigationUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    StatusText = $"Загрузка {url}...";
                    IsLoading = true;
                    
                    if (url == "about:blank")
                    {
                        // Для about:blank используем NavigateToString
                        WebView.CoreWebView2.NavigateToString("<html><body><h1>Chrome App</h1><p>Приложение готово к работе.</p></body></html>");
                    }
                    else
                    {
                        WebView.CoreWebView2.Navigate(url);
                    }
                }

                _isInitialized = true;
                _logger.LogInformation("WebView2 initialized successfully for {AppName}", _application.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing WebView2 for {AppName}", _application.Name);
                throw;
            }
        }

        private void ConfigureWebView2()
        {
            var webView2 = WebView.CoreWebView2;

            // Настройки безопасности
            webView2.Settings.IsScriptEnabled = true;
            webView2.Settings.AreDefaultScriptDialogsEnabled = true;
            webView2.Settings.IsWebMessageEnabled = true;
            webView2.Settings.AreDevToolsEnabled = true; // TODO: отключить в production

            // Настройки пользовательского агента
            webView2.Settings.UserAgent = $"WindowsLauncher/1.0 WebView2/{webView2.Environment.BrowserVersionString}";

            // Подписка на изменение заголовка документа
            webView2.DocumentTitleChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(webView2.DocumentTitle))
                {
                    var newTitle = $"{_application.Name} - {webView2.DocumentTitle}";
                    WindowTitle = newTitle;
                    _logger.LogTrace("Document title changed to '{Title}' for {AppName}", newTitle, _application.Name);
                }
            };

            // Обработка запроса на закрытие окна
            webView2.WindowCloseRequested += (s, e) =>
            {
                _logger.LogDebug("WebView2 window close requested for {AppName}", _application.Name);
                _isClosedByUser = true;
                
                // Закрываем окно в UI thread
                Dispatcher.BeginInvoke(() => Close());
            };

            // Блокировка всплывающих окон (опционально)
            webView2.NewWindowRequested += (s, e) =>
            {
                // Открываем новые окна в том же WebView
                e.Handled = true;
                WebView.CoreWebView2.Navigate(e.Uri);
            };

            // Обработка запросов разрешений
            webView2.PermissionRequested += WebView_PermissionRequested;

            _logger.LogDebug("WebView2 configured for {AppName}", _application.Name);
        }

        #endregion

        #region Обработчики событий WebView2

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _logger.LogDebug("CoreWebView2 initialization completed successfully for {AppName}", _application.Name);
                ConnectionStatus = "●";
                ConnectionStatusColor = Brushes.Green;
            }
            else
            {
                _logger.LogError(e.InitializationException, "CoreWebView2 initialization failed for {AppName}", _application.Name);
                ConnectionStatus = "●";
                ConnectionStatusColor = Brushes.Red;
                StatusText = $"Ошибка инициализации: {e.InitializationException?.Message}";
            }
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _logger.LogDebug("Navigation starting to {Uri} for {AppName}", e.Uri, _application.Name);
            IsLoading = true;
            StatusText = $"Загрузка {e.Uri}...";
            ConnectionStatus = "●";
            ConnectionStatusColor = Brushes.Orange;
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            IsLoading = false;
            
            if (e.IsSuccess)
            {
                _logger.LogDebug("Navigation completed successfully for {AppName}", _application.Name);
                StatusText = "Готов";
                ConnectionStatus = "●";
                ConnectionStatusColor = Brushes.Green;
            }
            else
            {
                _logger.LogWarning("Navigation failed for {AppName}: {Error}", _application.Name, e.WebErrorStatus);
                StatusText = $"Ошибка загрузки: {e.WebErrorStatus}";
                ConnectionStatus = "●";
                ConnectionStatusColor = Brushes.Red;
            }
        }



        private void WebView_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            // TODO: Настроить политику разрешений
            _logger.LogDebug("Permission requested: {Kind} for {AppName}", e.PermissionKind, _application.Name);
            
            // По умолчанию разрешаем основные разрешения
            switch (e.PermissionKind)
            {
                case CoreWebView2PermissionKind.Geolocation:
                case CoreWebView2PermissionKind.Notifications:
                case CoreWebView2PermissionKind.ClipboardRead:
                    e.State = CoreWebView2PermissionState.Allow;
                    break;
                default:
                    e.State = CoreWebView2PermissionState.Deny;
                    break;
            }
        }

        #endregion

        #region Обработчики событий окна

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            _logger.LogTrace("Window activated for {AppName} (Instance: {InstanceId})", _application.Name, InstanceId);
            var instance = CreateApplicationInstance();
            WindowActivated?.Invoke(this, instance);
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            _logger.LogTrace("Window deactivated for {AppName} (Instance: {InstanceId})", _application.Name, InstanceId);
            var instance = CreateApplicationInstance();
            WindowDeactivated?.Invoke(this, instance);
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            _logger.LogTrace("Window state changed to {State} for {AppName} (Instance: {InstanceId})", 
                WindowState, _application.Name, InstanceId);
            var instance = CreateApplicationInstance();
            WindowStateChanged?.Invoke(this, instance);
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                _logger.LogDebug("Window closing for {AppName} (Instance: {InstanceId}), ClosedByUser: {ClosedByUser}", 
                    _application.Name, InstanceId, _isClosedByUser);

                IsClosed = true;
                var instance = CreateApplicationInstance();
                WindowClosed?.Invoke(this, instance);

                // Cleanup WebView2
                WebView?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during window closing for {AppName}", _application.Name);
            }
        }

        #endregion

        #region Обработчики кнопок заголовка

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == System.Windows.WindowState.Maximized 
                ? System.Windows.WindowState.Normal 
                : System.Windows.WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _isClosedByUser = true;
            Close();
        }

        #endregion

        #region Публичные методы для управления

        /// <summary>
        /// Переключиться на это окно (активировать)
        /// </summary>
        public async Task<bool> SwitchToAsync()
        {
            try
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    WindowState = System.Windows.WindowState.Normal;
                }

                Activate();
                Focus();

                _logger.LogDebug("Switched to window for {AppName} (Instance: {InstanceId})", _application.Name, InstanceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch to window for {AppName}", _application.Name);
                return false;
            }
        }

        /// <summary>
        /// Навигация к новому URL
        /// </summary>
        public async Task NavigateAsync(string url)
        {
            try
            {
                if (_isInitialized && WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate(url);
                    _logger.LogDebug("Navigated to {Url} for {AppName}", url, _application.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to {Url} for {AppName}", url, _application.Name);
            }
        }

        /// <summary>
        /// Получить текущий URL
        /// </summary>
        public string GetCurrentUrl()
        {
            return WebView.CoreWebView2?.Source ?? _application.ExecutablePath ?? "";
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Получает URL для навигации WebView2
        /// </summary>
        private string GetNavigationUrl()
        {
            if (_application.Type == ApplicationType.Web)
            {
                return _application.ExecutablePath ?? "";
            }
            
            if (_application.Type == ApplicationType.ChromeApp)
            {
                // Сначала ищем URL в аргументах --app=URL
                var args = _application.Arguments ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(args, @"--app=([^\s]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                // Если ExecutablePath выглядит как URL, используем его
                var path = _application.ExecutablePath ?? "";
                if (IsValidWebUrl(path))
                {
                    return path;
                }
                
                // Для Chrome Apps без явного URL возвращаем about:blank
                if (path.Contains("chrome.exe", StringComparison.OrdinalIgnoreCase))
                {
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

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private ApplicationInstance CreateApplicationInstance()
        {
            return new ApplicationInstance
            {
                InstanceId = InstanceId,
                Application = _application,
                ProcessId = Environment.ProcessId, // WebView2 работает в текущем процессе
                StartTime = StartTime,
                State = GetCurrentApplicationState(),
                LaunchedBy = LaunchedBy
            };
        }

        private ApplicationState GetCurrentApplicationState()
        {
            if (_isClosedByUser)
                return ApplicationState.Terminated;

            return WindowState switch
            {
                System.Windows.WindowState.Minimized => ApplicationState.Minimized,
                System.Windows.WindowState.Maximized => ApplicationState.Running,
                System.Windows.WindowState.Normal => ApplicationState.Running,
                _ => ApplicationState.Running
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.UI.Infrastructure.Localization;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно справочной системы с HTML контентом через WebView2
    /// </summary>
    public partial class HelpWindow : Window
    {
        private readonly ILogger<HelpWindow> _logger;
        private bool _isInitialized = false;

        public HelpWindow()
        {
            InitializeComponent();
            
            // Получение сервисов через App.ServiceProvider
            var serviceProvider = ((App)Application.Current).ServiceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<HelpWindow>>();

            Loaded += HelpWindow_Loaded;
        }

        private async void HelpWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebViewAsync();
                await LoadHelpContentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize help window");
                ShowError("Не удалось загрузить справочную систему");
            }
        }

        /// <summary>
        /// Инициализация WebView2 с базовыми настройками безопасности
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Создание временной папки для WebView2 данных
                var tempPath = Path.Combine(Path.GetTempPath(), "WindowsLauncher", "HelpWebView");
                Directory.CreateDirectory(tempPath);

                // Инициализация среды WebView2
                var environment = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await HelpWebView.EnsureCoreWebView2Async(environment);

                // Настройки безопасности для справочной системы
                var settings = HelpWebView.CoreWebView2.Settings;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;
                settings.AreDevToolsEnabled = false; // Отключаем DevTools для production
                settings.AreHostObjectsAllowed = false;
                settings.IsWebMessageEnabled = true; // Для JavaScript взаимодействия
                settings.IsScriptEnabled = true; // Нужно для поиска и навигации

                // Блокировка внешних ресурсов - только локальный контент
                HelpWebView.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    e.State = CoreWebView2PermissionState.Deny; // Запрещаем все разрешения
                };

                _isInitialized = true;
                _logger.LogDebug("WebView2 initialized successfully for help system");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize WebView2 for help system");
                throw;
            }
        }

        /// <summary>
        /// Загрузка HTML контента справки
        /// </summary>
        private async Task LoadHelpContentAsync()
        {
            if (!_isInitialized)
                return;

            try
            {
                // Получение текущего языка для локализации
                var currentLanguage = LocalizationHelper.Instance.CurrentCulture.Name;
                
                // Путь к HTML ресурсам справки
                var htmlContent = GetEmbeddedHelpContent(currentLanguage);
                
                // Загрузка HTML контента напрямую
                HelpWebView.NavigateToString(htmlContent);
                
                _logger.LogDebug($"Help content loaded for language: {currentLanguage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load help content");
                
                // Fallback контент при ошибке
                var fallbackHtml = GetFallbackHelpContent();
                HelpWebView.NavigateToString(fallbackHtml);
            }
        }

        /// <summary>
        /// Получение встроенного HTML контента справки
        /// </summary>
        private string GetEmbeddedHelpContent(string language)
        {
            try
            {
                var languageCode = language == "ru-RU" ? "ru" : "en";
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Help", languageCode, "index.html");
                
                if (File.Exists(htmlPath))
                {
                    var htmlContent = File.ReadAllText(htmlPath, System.Text.Encoding.UTF8);
                    
                    // Обновляем пути к CSS и JS для работы в WebView2
                    var basePath = Path.GetDirectoryName(htmlPath);
                    var cssPath = Path.Combine(basePath, "styles.css");
                    var jsPath = Path.Combine(basePath, "help-script.js");
                    
                    if (File.Exists(cssPath))
                    {
                        var cssContent = File.ReadAllText(cssPath, System.Text.Encoding.UTF8);
                        htmlContent = htmlContent.Replace("<link rel=\"stylesheet\" href=\"styles.css\">", 
                            $"<style>{cssContent}</style>");
                    }
                    
                    if (File.Exists(jsPath))
                    {
                        var jsContent = File.ReadAllText(jsPath, System.Text.Encoding.UTF8);
                        htmlContent = htmlContent.Replace("<script src=\"help-script.js\"></script>", 
                            $"<script>{jsContent}</script>");
                    }
                    
                    return htmlContent;
                }
                
                _logger.LogWarning($"Help file not found: {htmlPath}");
                return GetFallbackHelpContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading help content for language: {language}");
                return GetFallbackHelpContent();
            }
        }

        /// <summary>
        /// Fallback контент при ошибке загрузки
        /// </summary>
        private string GetFallbackHelpContent()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Help System Error</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 50px; text-align: center; }
        .error { color: #C41E3A; font-size: 18px; }
    </style>
</head>
<body>
    <div class='error'>
        <h2>⚠️ Ошибка загрузки справочной системы</h2>
        <p>Попробуйте перезапустить окно справки или обратитесь в техническую поддержку.</p>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Отображение ошибки пользователю
        /// </summary>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка справочной системы", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        #region Navigation Event Handlers

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized && HelpWebView.CanGoBack)
            {
                HelpWebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized && HelpWebView.CanGoForward)
            {
                HelpWebView.GoForward();
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadHelpContentAsync(); // Перезагрузка главной страницы
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region WebView2 Event Handlers

        private void HelpWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Логирование навигации
            _logger.LogDebug($"Help navigation starting to: {e.Uri}");
            
            // Обновление состояния кнопок навигации
            UpdateNavigationButtons();
        }

        private void HelpWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _logger.LogDebug("Help navigation completed successfully");
            }
            else
            {
                _logger.LogWarning($"Help navigation failed: {e.WebErrorStatus}");
            }
            
            UpdateNavigationButtons();
        }


        /// <summary>
        /// Обновление состояния кнопок навигации
        /// </summary>
        private void UpdateNavigationButtons()
        {
            if (_isInitialized)
            {
                Dispatcher.Invoke(() =>
                {
                    BackButton.IsEnabled = HelpWebView.CanGoBack;
                    ForwardButton.IsEnabled = HelpWebView.CanGoForward;
                });
            }
        }

        #endregion
    }
}
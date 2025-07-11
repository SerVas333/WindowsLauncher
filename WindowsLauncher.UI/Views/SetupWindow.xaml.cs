using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

// Явно указываем пространства имен для разрешения конфликтов
using WpfMessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно первоначальной настройки системы
    /// </summary>
    public partial class SetupWindow : Window
    {
        private readonly IAuthenticationConfigurationService _configService;
        private readonly IAuthenticationService _authService;
        private readonly IActiveDirectoryService _adService;
        private readonly ILogger<SetupWindow> _logger;

        private bool _isDomainValid = false;
        private bool _isServiceAdminValid = false;
        private bool _isProcessing = false;

        // Валидация пароля
        private readonly Regex _uppercaseRegex = new(@"[A-Z]");
        private readonly Regex _lowercaseRegex = new(@"[a-z]");
        private readonly Regex _digitRegex = new(@"[0-9]");
        private readonly Regex _specialRegex = new(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");

        public bool SetupCompleted { get; private set; } = false;

        public SetupWindow(
            IAuthenticationConfigurationService configService,
            IAuthenticationService authService,
            IActiveDirectoryService adService,
            ILogger<SetupWindow> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeComponent();
            
            // Переносим инициализацию в событие Loaded
            Loaded += SetupWindow_Loaded;
        }

        /// <summary>
        /// Обработчик события загрузки окна
        /// </summary>
        private void SetupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWindow();
        }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // Загружаем текущую конфигурацию
                LoadCurrentConfiguration();

                // Настраиваем валидацию
                UpdateValidationIndicators();
                UpdateSetupProgress();

                _logger.LogInformation("Setup window initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing setup window");
                ShowError("Ошибка инициализации окна настройки");
            }
        }

        #region Event Handlers

        /// <summary>
        /// Обработчик изменения настроек домена
        /// </summary>
        private void DomainSettings_Changed(object sender, EventArgs e)
        {
            ValidateDomainSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик изменения настроек сервисного администратора
        /// </summary>
        private void ServiceAdmin_Changed(object sender, EventArgs e)
        {
            ValidateServiceAdminSettings();
            UpdateSetupProgress();
        }

        /// <summary>
        /// Обработчик проверки подключения
        /// </summary>
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            await TestDomainConnectionAsync();
        }

        /// <summary>
        /// Обработчик пропуска настройки
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var result = WpfMessageBox.Show(
                "Вы уверены, что хотите пропустить настройку?\n\n" +
                "Система будет работать с настройками по умолчанию.\n" +
                "Вы сможете изменить настройки позже в меню администратора.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                SetupCompleted = false;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Обработчик завершения настройки
        /// </summary>
        private async void CompleteSetupButton_Click(object sender, RoutedEventArgs e)
        {
            await CompleteSetupAsync();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Загрузка текущей конфигурации
        /// </summary>
        private void LoadCurrentConfiguration()
        {
            try
            {
                var config = _configService.GetConfiguration();

                // Настройки домена
                DomainTextBox.Text = config.Domain ?? "company.local";
                LdapServerTextBox.Text = config.LdapServer ?? "dc.company.local";
                PortTextBox.Text = config.Port.ToString();
                UseTlsCheckBox.IsChecked = config.UseTLS;

                // Настройки сервисного администратора
                ServiceUsernameTextBox.Text = config.ServiceAdmin.Username ?? "serviceadmin";
                SessionTimeoutTextBox.Text = config.ServiceAdmin.SessionTimeoutMinutes.ToString();

                _logger.LogDebug("Current configuration loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not load current configuration: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Валидация настроек домена
        /// </summary>
        private void ValidateDomainSettings()
        {
            try
            {
                // Проверяем, что все элементы UI доступны
                if (DomainTextBox == null || LdapServerTextBox == null || PortTextBox == null)
                {
                    _isDomainValid = false;
                    return;
                }
                var domain = DomainTextBox?.Text?.Trim() ?? "";
                var ldapServer = LdapServerTextBox?.Text?.Trim() ?? "";
                var portText = PortTextBox?.Text?.Trim() ?? "389";

                _isDomainValid = !string.IsNullOrEmpty(domain) &&
                                 !string.IsNullOrEmpty(ldapServer) &&
                                 int.TryParse(portText, out var port) &&
                                 port > 0 && port <= 65535;

                _logger.LogDebug("Domain settings validation: {IsValid}", _isDomainValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error validating domain settings");
                _isDomainValid = false;
            }
        }

        /// <summary>
        /// Валидация настроек сервисного администратора
        /// </summary>
        private void ValidateServiceAdminSettings()
        {
            try
            {
                // Проверяем, что все элементы UI доступны
                if (ServiceUsernameTextBox == null || ServicePasswordBox == null || 
                    ServicePasswordConfirmBox == null || SessionTimeoutTextBox == null)
                {
                    _isServiceAdminValid = false;
                    return;
                }
                var username = ServiceUsernameTextBox?.Text?.Trim();
                var password = ServicePasswordBox?.Password;
                var confirmPassword = ServicePasswordConfirmBox?.Password;
                var sessionTimeoutText = SessionTimeoutTextBox?.Text?.Trim();

                // Валидация имени пользователя
                var isUsernameValid = !string.IsNullOrEmpty(username) && username.Length >= 3;

                // Валидация времени сессии
                var isSessionTimeoutValid = int.TryParse(sessionTimeoutText, out var timeout) &&
                                           timeout > 0 && timeout <= 1440;

                // Валидация пароля
                var isPasswordValid = ValidatePassword(password);
                var isPasswordMatch = password == confirmPassword;

                _isServiceAdminValid = isUsernameValid && isSessionTimeoutValid &&
                                      isPasswordValid && isPasswordMatch;

                // Обновляем индикаторы валидации пароля
                UpdatePasswordValidationIndicators(password, confirmPassword);

                _logger.LogDebug("Service admin settings validation: {IsValid}", _isServiceAdminValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error validating service admin settings: {Error}", ex.Message);
                _isServiceAdminValid = false;
            }
        }

        /// <summary>
        /// Валидация пароля
        /// </summary>
        private bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            return password.Length >= 8 &&
                   _uppercaseRegex.IsMatch(password) &&
                   _lowercaseRegex.IsMatch(password) &&
                   _digitRegex.IsMatch(password) &&
                   _specialRegex.IsMatch(password);
        }

        /// <summary>
        /// Обновление индикаторов валидации пароля
        /// </summary>
        private void UpdatePasswordValidationIndicators(string password, string confirmPassword)
        {
            try
            {
                var successBrush = TryFindResource("SuccessBrush") as WpfBrush ?? Brushes.Green;
                var errorBrush = TryFindResource("ErrorBrush") as WpfBrush ?? Brushes.Red;
                var defaultBrush = TryFindResource("OnSurfaceVariantBrush") as WpfBrush ?? Brushes.Gray;

                // Длина пароля
                PasswordLengthValidation.Foreground = (password?.Length >= 8) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Заглавные буквы
                PasswordUppercaseValidation.Foreground = (!string.IsNullOrEmpty(password) && _uppercaseRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Строчные буквы
                PasswordLowercaseValidation.Foreground = (!string.IsNullOrEmpty(password) && _lowercaseRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Цифры
                PasswordDigitValidation.Foreground = (!string.IsNullOrEmpty(password) && _digitRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Специальные символы
                PasswordSpecialValidation.Foreground = (!string.IsNullOrEmpty(password) && _specialRegex.IsMatch(password)) ? successBrush :
                    (string.IsNullOrEmpty(password) ? defaultBrush : errorBrush);

                // Совпадение паролей
                PasswordMatchValidation.Foreground = (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(confirmPassword) && password == confirmPassword) ? successBrush :
                    (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(confirmPassword) ? defaultBrush : errorBrush);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error updating password validation indicators: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Обновление индикаторов валидации
        /// </summary>
        private void UpdateValidationIndicators()
        {
            ValidateDomainSettings();
            ValidateServiceAdminSettings();
        }

        /// <summary>
        /// Обновление прогресса настройки
        /// </summary>
        private void UpdateSetupProgress()
        {
            try
            {
                var completedSteps = 0;
                var totalSteps = 3;

                if (_isDomainValid) completedSteps++;
                if (_isServiceAdminValid) completedSteps++;
                // Третий шаг (дополнительные настройки) всегда считается выполненным

                if (SetupProgressText != null)
                {
                    SetupProgressText.Text = $"Настройка: {completedSteps}/{totalSteps} шагов завершено";
                }

                // Кнопка "Завершить настройку" доступна только если выполнены критически важные шаги
                if (CompleteSetupButton != null)
                {
                    CompleteSetupButton.IsEnabled = _isDomainValid && _isServiceAdminValid && !_isProcessing;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error updating setup progress: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Тестирование подключения к домену
        /// </summary>
        private async Task TestDomainConnectionAsync()
        {
            if (_isProcessing)
                return;

            try
            {
                SetLoadingState(true, "Проверка подключения к домену...");
                ConnectionStatusPanel.Visibility = Visibility.Collapsed;

                var server = LdapServerTextBox.Text?.Trim();
                var portText = PortTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(server) || !int.TryParse(portText, out var port))
                {
                    ShowConnectionStatus(false, "Некорректные параметры подключения");
                    return;
                }

                var isConnected = await _adService.TestConnectionAsync(server, port);

                ShowConnectionStatus(isConnected,
                    isConnected ? "Подключение успешно" : "Не удалось подключиться к серверу");

                _logger.LogInformation("Domain connection test result: {IsConnected}", isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing domain connection");
                ShowConnectionStatus(false, $"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        /// <summary>
        /// Отображение статуса подключения
        /// </summary>
        private void ShowConnectionStatus(bool isSuccess, string message)
        {
            try
            {
                ConnectionStatusPanel.Visibility = Visibility.Visible;
                ConnectionStatusText.Text = message;

                var brush = TryFindResource(isSuccess ? "SuccessBrush" : "ErrorBrush") as WpfBrush ??
                           (isSuccess ? Brushes.Green : Brushes.Red);
                ConnectionStatusIndicator.Fill = brush;
                ConnectionStatusText.Foreground = brush;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error showing connection status: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Завершение настройки
        /// </summary>
        private async Task CompleteSetupAsync()
        {
            if (_isProcessing)
                return;

            try
            {
                SetLoadingState(true, "Сохранение настроек...");

                // Создаем конфигурацию
                var config = CreateConfigurationFromForm();

                // Сохраняем конфигурацию
                await _configService.SaveConfigurationAsync(config);

                // Создаем сервисного администратора
                await CreateServiceAdminAsync();

                // Применяем дополнительные настройки
                await ApplyAdditionalSettingsAsync();

                SetupCompleted = true;
                DialogResult = true;

                _logger.LogInformation("Setup completed successfully");
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing setup");
                ShowError($"Ошибка завершения настройки: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        /// <summary>
        /// Создание конфигурации из формы
        /// </summary>
        private AuthenticationConfiguration CreateConfigurationFromForm()
        {
            return new AuthenticationConfiguration
            {
                Domain = DomainTextBox.Text?.Trim(),
                LdapServer = LdapServerTextBox.Text?.Trim(),
                Port = int.Parse(PortTextBox.Text?.Trim() ?? "389"),
                UseTLS = UseTlsCheckBox.IsChecked == true,
                ServiceAdmin = new ServiceAdminConfiguration
                {
                    Username = ServiceUsernameTextBox.Text?.Trim(),
                    SessionTimeoutMinutes = int.Parse(SessionTimeoutTextBox.Text?.Trim() ?? "60")
                },
                IsConfigured = true,
                LastModified = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создание сервисного администратора
        /// </summary>
        private async Task CreateServiceAdminAsync()
        {
            try
            {
                var username = ServiceUsernameTextBox.Text?.Trim();
                var password = ServicePasswordBox.Password;

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await _authService.CreateServiceAdminAsync(username, password);
                    _logger.LogInformation("Service admin created: {Username}", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error creating service admin: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Применение дополнительных настроек
        /// </summary>
        private async Task ApplyAdditionalSettingsAsync()
        {
            try
            {
                // Настройка аудита
                if (EnableAuditingCheckBox.IsChecked == true)
                {
                    // Включаем аудит через соответствующий сервис
                    _logger.LogInformation("Auditing enabled");
                }

                // Автозагрузка
                if (AutoStartCheckBox.IsChecked == true)
                {
                    // Добавляем в автозагрузку
                    await SetAutoStartAsync(true);
                    _logger.LogInformation("Auto-start enabled");
                }

                // Язык интерфейса
                var selectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(selectedLanguage))
                {
                    await SetLanguageAsync(selectedLanguage);
                    _logger.LogInformation("Language set to: {Language}", selectedLanguage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error applying additional settings: {Error}", ex.Message);
                // Не прерываем процесс настройки из-за дополнительных настроек
            }
        }

        /// <summary>
        /// Настройка автозагрузки
        /// </summary>
        private async Task SetAutoStartAsync(bool enable)
        {
            try
            {
                // Реализация настройки автозагрузки через реестр Windows
                await Task.Run(() =>
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                    if (enable)
                    {
                        var executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key?.SetValue("KDV Launcher", executablePath);
                    }
                    else
                    {
                        key?.DeleteValue("KDV Launcher", false);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error setting auto-start: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Настройка языка интерфейса
        /// </summary>
        private async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                await Task.Run(() =>
                {
                    var culture = new CultureInfo(languageCode);
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;

                    // Сохраняем настройку языка в конфигурации
                    // TODO: Использовать конфигурационный сервис вместо Settings
                    _logger.LogInformation("Language preference saved: {Language}", languageCode);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error setting language: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Установка состояния загрузки
        /// </summary>
        private void SetLoadingState(bool isLoading, string message = null)
        {
            try
            {
                _isProcessing = isLoading;
                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

                if (!string.IsNullOrEmpty(message))
                {
                    LoadingText.Text = message;
                }

                // Обновляем доступность кнопок
                TestConnectionButton.IsEnabled = !isLoading;
                CompleteSetupButton.IsEnabled = !isLoading && _isDomainValid && _isServiceAdminValid;
                SkipButton.IsEnabled = !isLoading;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error setting loading state: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Отображение ошибки
        /// </summary>
        private void ShowError(string message)
        {
            try
            {
                ErrorTextBlock.Text = message;
                ErrorPanel.Visibility = Visibility.Visible;

                // Автоматически скрываем ошибку через 5 секунд
                Task.Delay(5000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ErrorPanel.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error showing error message: {Error}", ex.Message);
            }
        }

        #endregion

        #region Window Events

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Предупреждаем о несохраненных изменениях
                if (!SetupCompleted && (_isDomainValid || _isServiceAdminValid))
                {
                    var result = WpfMessageBox.Show(
                        "У вас есть несохраненные изменения.\n\n" +
                        "Вы уверены, что хотите выйти без сохранения настроек?",
                        "Несохраненные изменения",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during window closing");
            }
        }

        #endregion
    }
}
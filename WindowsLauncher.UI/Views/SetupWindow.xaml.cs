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

        public SetupWindow()
        {
            InitializeComponent();

            // Получаем сервисы из DI контейнера
            var serviceProvider = ((App)Application.Current).ServiceProvider;
            _configService = serviceProvider.GetRequiredService<IAuthenticationConfigurationService>();
            _authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            _adService = serviceProvider.GetRequiredService<IActiveDirectoryService>();
            _logger = serviceProvider.GetRequiredService<ILogger<SetupWindow>>();

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
            var result = MessageBox.Show(
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
                var domain = DomainTextBox.Text?.Trim();
                var ldapServer = LdapServerTextBox.Text?.Trim();
                var portText = PortTextBox.Text?.Trim();

                _isDomainValid = !string.IsNullOrEmpty(domain) &&
                                 !string.IsNullOrEmpty(ldapServer) &&
                                 int.TryParse(portText, out var port) &&
                                 port > 0 && port <= 65535;

                _logger.LogDebug("Domain settings validation: {IsValid}", _isDomainValid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error validating domain settings: {Error}", ex.Message);
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
                var username = ServiceUsernameTextBox.Text?.Trim();
                var password = ServicePasswordBox.Password;
                var confirmPassword = ServicePasswordConfirmBox.Password;
                var sessionTimeoutText = SessionTimeoutTextBox.Text?.Trim();

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
                var successBrush = FindResource("SuccessBrush") as Brush;
                var errorBrush = FindResource("ErrorBrush") as Brush;
                var defaultBrush = FindResource("OnSurfaceVariantBrush") as Brush;

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

                SetupProgressText.Text = $"Настройка: {completedSteps}/{totalSteps} шагов завершено";

                // Кнопка "Завершить настройку" доступна только если выполнены критически важные шаги
                CompleteSetupButton.IsEnabled = _isDomainValid && _isServiceAdminValid && !_isProcessing;
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
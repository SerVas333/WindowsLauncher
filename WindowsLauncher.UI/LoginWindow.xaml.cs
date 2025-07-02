using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы для KeyEventArgs
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно входа в систему с поддержкой доменной и сервисной аутентификации
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<LoginWindow> _logger;
        private bool _isAuthenticating = false;
        private AuthenticationResult _lastResult;

        // Публичные свойства для доступа к введенным данным
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string Domain { get; private set; }
        public bool IsServiceMode { get; private set; }
        public AuthenticationResult AuthenticationResult { get; private set; }

        // Совместимость с MainViewModel
        public User? AuthenticatedUser => AuthenticationResult?.User;

        public LoginWindow()
        {
            InitializeComponent();

            // Получаем сервисы из DI контейнера
            var serviceProvider = ((App)WpfApplication.Current).ServiceProvider;
            _authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            _logger = serviceProvider.GetRequiredService<ILogger<LoginWindow>>();

            InitializeWindow();
        }

        /// <summary>
        /// Конструктор с передачей сообщения об ошибке
        /// </summary>
        public LoginWindow(string errorMessage) : this()
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ShowError(errorMessage);
            }
        }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        private async void InitializeWindow()
        {
            try
            {
                // Устанавливаем фокус на первое поле
                Loaded += (s, e) => UsernameTextBox.Focus();

                // Загружаем сохраненные настройки
                LoadSavedSettings();

                // Проверяем доступность домена
                await CheckDomainAvailabilityAsync();

                // Проверяем, настроен ли сервисный администратор
                CheckServiceAdminConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing login window");
                ShowError("Ошибка инициализации окна входа");
            }
        }

        #region Event Handlers

        /// <summary>
        /// Обработчик нажатия кнопки входа
        /// </summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLoginAsync();
        }

        /// <summary>
        /// Обработчик нажатия кнопки отмены
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Обработчик нажатия кнопки настроек
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Открыть окно настроек подключения
                MessageBox.Show(
                    "Настройки подключения будут доступны в следующей версии.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings");
                ShowError("Ошибка открытия настроек");
            }
        }

        /// <summary>
        /// Обработчик изменения режима входа
        /// </summary>
        private void LoginMode_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var isDomainMode = DomainModeRadio.IsChecked == true;

                DomainLoginPanel.Visibility = isDomainMode ? Visibility.Visible : Visibility.Collapsed;
                ServiceLoginPanel.Visibility = isDomainMode ? Visibility.Collapsed : Visibility.Visible;

                IsServiceMode = !isDomainMode;

                // Устанавливаем фокус на первое видимое поле
                if (isDomainMode)
                {
                    UsernameTextBox.Focus();
                }
                else
                {
                    ServiceUsernameTextBox.Focus();
                }

                HideError();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing login mode");
            }
        }

        /// <summary>
        /// Обработчик нажатия клавиш в полях ввода
        /// ✅ ИСПРАВЛЕНО: Используем WpfKeyEventArgs
        /// </summary>
        private async void Input_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !_isAuthenticating)
            {
                await PerformLoginAsync();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Выполнение аутентификации
        /// </summary>
        private async Task PerformLoginAsync()
        {
            if (_isAuthenticating)
                return;

            try
            {
                HideError();
                SetLoadingState(true);

                AuthenticationCredentials credentials;

                if (IsServiceMode)
                {
                    // Сервисная аутентификация
                    credentials = new AuthenticationCredentials
                    {
                        Username = ServiceUsernameTextBox.Text.Trim(),
                        Password = ServicePasswordBox.Password,
                        IsServiceAccount = true
                    };
                }
                else
                {
                    // Доменная аутентификация
                    credentials = new AuthenticationCredentials
                    {
                        Username = UsernameTextBox.Text.Trim(),
                        Password = PasswordBox.Password,
                        Domain = DomainTextBox.Text.Trim()
                    };
                }

                // Валидация введенных данных
                if (!ValidateCredentials(credentials))
                    return;

                // Выполняем аутентификацию
                var result = await _authService.AuthenticateAsync(credentials);

                _lastResult = result;
                AuthenticationResult = result;

                if (result.IsSuccess)
                {
                    // Сохраняем введенные данные для внешнего доступа
                    Username = credentials.Username;
                    Password = credentials.Password;
                    Domain = credentials.Domain;

                    // Сохраняем настройки, если нужно
                    if (RememberCredentialsCheckBox.IsChecked == true)
                    {
                        SaveSettings(credentials);
                    }

                    _logger.LogInformation("Login successful for user {Username}", credentials.Username);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Показываем ошибку аутентификации
                    var errorMessage = GetUserFriendlyErrorMessage(result);
                    ShowError(errorMessage);

                    _logger.LogWarning("Login failed for user {Username}: {Error}",
                        credentials.Username, result.ErrorMessage);

                    // Очищаем поле пароля при ошибке
                    if (IsServiceMode)
                        ServicePasswordBox.Clear();
                    else
                        PasswordBox.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login");
                ShowError("Произошла непредвиденная ошибка. Попробуйте еще раз.");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        /// <summary>
        /// Валидация учетных данных
        /// </summary>
        private bool ValidateCredentials(AuthenticationCredentials credentials)
        {
            if (string.IsNullOrWhiteSpace(credentials.Username))
            {
                ShowError("Введите имя пользователя");
                return false;
            }

            if (string.IsNullOrWhiteSpace(credentials.Password))
            {
                ShowError("Введите пароль");
                return false;
            }

            if (!IsServiceMode && string.IsNullOrWhiteSpace(credentials.Domain))
            {
                ShowError("Введите домен");
                return false;
            }

            // Дополнительная валидация для доменных учетных данных
            if (!IsServiceMode)
            {
                if (credentials.Username.Contains("\\") || credentials.Username.Contains("@"))
                {
                    ShowError("Введите только имя пользователя без домена");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Загрузка сохраненных настроек
        /// </summary>
        private void LoadSavedSettings()
        {
            try
            {
                // Загружаем настройки из реестра или конфигурационного файла
                var settings = Properties.Settings.Default;

                if (!string.IsNullOrEmpty(settings.LastDomain))
                {
                    DomainTextBox.Text = settings.LastDomain;
                }

                if (!string.IsNullOrEmpty(settings.LastUsername))
                {
                    UsernameTextBox.Text = settings.LastUsername;
                    RememberCredentialsCheckBox.IsChecked = true;
                }

                // Восстанавливаем режим входа
                if (settings.LastLoginMode == "Service")
                {
                    ServiceModeRadio.IsChecked = true;
                    LoginMode_Changed(ServiceModeRadio, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not load saved settings: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Сохранение настроек
        /// </summary>
        private void SaveSettings(AuthenticationCredentials credentials)
        {
            try
            {
                var settings = Properties.Settings.Default;

                if (!IsServiceMode)
                {
                    settings.LastDomain = credentials.Domain;
                    settings.LastUsername = credentials.Username;
                    settings.LastLoginMode = "Domain";
                }
                else
                {
                    settings.LastLoginMode = "Service";
                }

                settings.Save();

                _logger.LogDebug("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not save settings: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Проверка доступности домена
        /// </summary>
        private async Task CheckDomainAvailabilityAsync()
        {
            try
            {
                var domain = DomainTextBox.Text.Trim();
                if (string.IsNullOrEmpty(domain))
                    return;

                var isAvailable = await _authService.IsDomainAvailableAsync(domain);

                UpdateConnectionStatus(isAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Domain availability check failed: {Error}", ex.Message);
                UpdateConnectionStatus(false);
            }
        }

        /// <summary>
        /// Проверка конфигурации сервисного администратора
        /// </summary>
        private void CheckServiceAdminConfiguration()
        {
            try
            {
                var isConfigured = _authService.IsServiceAdminConfigured();

                if (!isConfigured)
                {
                    // Если сервисный администратор не настроен, скрываем эту опцию
                    ServiceModeRadio.Visibility = Visibility.Collapsed;

                    // Показываем предупреждение
                    ShowError("Сервисный администратор не настроен. Обратитесь к системному администратору.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Service admin configuration check failed: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Обновление статуса подключения
        /// </summary>
        private void UpdateConnectionStatus(bool isAvailable)
        {
            try
            {
                if (isAvailable)
                {
                    ConnectionStatusIndicator.Fill = FindResource("SuccessBrush") as Brush;
                    ConnectionStatusText.Text = "Домен доступен";
                }
                else
                {
                    ConnectionStatusIndicator.Fill = FindResource("ErrorBrush") as Brush;
                    ConnectionStatusText.Text = "Домен недоступен";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error updating connection status: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Показ ошибки
        /// </summary>
        private void ShowError(string message)
        {
            try
            {
                ErrorTextBlock.Text = message;
                ErrorPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing error message");
            }
        }

        /// <summary>
        /// Скрытие ошибки
        /// </summary>
        private void HideError()
        {
            try
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error hiding error panel: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Установка состояния загрузки
        /// </summary>
        private void SetLoadingState(bool isLoading)
        {
            try
            {
                _isAuthenticating = isLoading;

                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                LoginButton.IsEnabled = !isLoading;
                CancelButton.IsEnabled = !isLoading;

                // Отключаем поля ввода во время аутентификации
                DomainTextBox.IsEnabled = !isLoading;
                UsernameTextBox.IsEnabled = !isLoading;
                PasswordBox.IsEnabled = !isLoading;
                ServiceUsernameTextBox.IsEnabled = !isLoading;
                ServicePasswordBox.IsEnabled = !isLoading;
                DomainModeRadio.IsEnabled = !isLoading;
                ServiceModeRadio.IsEnabled = !isLoading;

                if (isLoading)
                {
                    LoadingText.Text = IsServiceMode ? "Аутентификация сервисного администратора..." : "Аутентификация в домене...";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting loading state");
            }
        }

        /// <summary>
        /// Получение понятного пользователю сообщения об ошибке
        /// </summary>
        private string GetUserFriendlyErrorMessage(AuthenticationResult result)
        {
            return result.Status switch
            {
                AuthenticationStatus.InvalidCredentials => "Неверные учетные данные. Проверьте логин и пароль.",
                AuthenticationStatus.UserNotFound => "Пользователь не найден в домене.",
                AuthenticationStatus.DomainUnavailable => "Домен недоступен. Проверьте сетевое подключение.",
                AuthenticationStatus.NetworkError => "Ошибка сети. Проверьте подключение к домену.",
                AuthenticationStatus.ServiceModeRequired => "Домен недоступен. Используйте режим сервисного администратора.",
                _ => result.ErrorMessage ?? "Произошла неизвестная ошибка."
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Показ окна входа с возможностью предварительной настройки
        /// </summary>
        public static LoginWindow ShowLoginDialog(string domain = null, string username = null, bool serviceMode = false)
        {
            var loginWindow = new LoginWindow();

            if (!string.IsNullOrEmpty(domain))
                loginWindow.DomainTextBox.Text = domain;

            if (!string.IsNullOrEmpty(username))
                loginWindow.UsernameTextBox.Text = username;

            if (serviceMode)
            {
                loginWindow.ServiceModeRadio.IsChecked = true;
                loginWindow.LoginMode_Changed(loginWindow.ServiceModeRadio, new RoutedEventArgs());
            }

            return loginWindow;
        }

        /// <summary>
        /// Показ окна с сообщением об ошибке
        /// </summary>
        public static LoginWindow ShowLoginDialogWithError(string errorMessage)
        {
            return new LoginWindow(errorMessage);
        }

        #endregion
    }
}
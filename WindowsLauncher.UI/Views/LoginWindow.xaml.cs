// WindowsLauncher.UI/Views/LoginWindow.xaml.cs - ПОЛНОСТЬЮ ИСПРАВЛЕННАЯ ВЕРСИЯ
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Properties.Resources;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Infrastructure.Extensions;
using WindowsLauncher.UI.Helpers;

// Алиасы для разрешения конфликтов имен
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно входа в систему с поддержкой доменной и сервисной аутентификации
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<LoginWindow> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private bool _isAuthenticating = false;

        // Публичные свойства для доступа к результату
        public AuthenticationResult AuthenticationResult { get; private set; } = null!;
        public User AuthenticatedUser => AuthenticationResult?.User;

        public LoginWindow(IAuthenticationService authService, ILogger<LoginWindow> logger, IServiceScopeFactory serviceScopeFactory)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoginWindow: Starting InitializeComponent...");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("LoginWindow: InitializeComponent completed successfully");
                
                // Инициализация зависимостей через constructor injection
                _authService = authService;
                _logger = logger;
                _serviceScopeFactory = serviceScopeFactory;
                
                System.Diagnostics.Debug.WriteLine("LoginWindow: Services initialized via DI successfully");
                
                System.Diagnostics.Debug.WriteLine("LoginWindow: Starting InitializeWindow...");
                InitializeWindow();
                System.Diagnostics.Debug.WriteLine("LoginWindow: InitializeWindow completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoginWindow constructor failed at: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                
                // Fallback для случаев когда XAML не загружается
                CreateFallbackWindow();
                System.Diagnostics.Debug.WriteLine($"XAML loading failed: {ex.Message}");
            }
        }

        public LoginWindow(IAuthenticationService authService, ILogger<LoginWindow> logger, IServiceScopeFactory serviceScopeFactory, string errorMessage) 
            : this(authService, logger, serviceScopeFactory)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ShowError(errorMessage);
            }
        }


        private void InitializeWindow()
        {
            try
            {
                Title = LocalizationHelper.Instance.GetString("LoginWindow_Title");

                // Устанавливаем фокус при загрузке
                Loaded += (s, e) =>
                {
                    // ✅ Гостевой режим выбран по умолчанию в XAML (GuestModeRadio IsChecked="True")
                    // Инициализируем видимость панелей согласно выбранному режиму
                    LoginMode_Changed(this, new RoutedEventArgs());
                    
                    if (DomainModeRadio?.IsChecked == true)
                    {
                        UsernameTextBox?.Focus();
                    }
                    else if (LocalModeRadio?.IsChecked == true)
                    {
                        LocalUsernameTextBox?.Focus();
                    }
                    // Для гостевого режима фокус не нужен
                };

                // Проверяем доступность домена при инициализации
                CheckDomainAvailabilityAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing login window");
            }
        }

        #region Event Handlers

        private void LoginMode_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DomainLoginPanel == null || LocalLoginPanel == null || GuestLoginPanel == null || ConnectionStatusPanel == null) return;

                if (DomainModeRadio?.IsChecked == true)
                {
                    DomainLoginPanel.Visibility = Visibility.Visible;
                    LocalLoginPanel.Visibility = Visibility.Collapsed;
                    GuestLoginPanel.Visibility = Visibility.Collapsed;
                    ConnectionStatusPanel.Visibility = Visibility.Visible; // ✅ Показываем статус подключения
                    UsernameTextBox?.Focus();
                }
                else if (LocalModeRadio?.IsChecked == true)
                {
                    DomainLoginPanel.Visibility = Visibility.Collapsed;
                    LocalLoginPanel.Visibility = Visibility.Visible;
                    GuestLoginPanel.Visibility = Visibility.Collapsed;
                    ConnectionStatusPanel.Visibility = Visibility.Collapsed; // ✅ Скрываем статус подключения
                    LocalUsernameTextBox?.Focus();
                }
                else if (GuestModeRadio?.IsChecked == true)
                {
                    DomainLoginPanel.Visibility = Visibility.Collapsed;
                    LocalLoginPanel.Visibility = Visibility.Collapsed;
                    GuestLoginPanel.Visibility = Visibility.Visible;
                    ConnectionStatusPanel.Visibility = Visibility.Collapsed; // ✅ Скрываем статус подключения
                    // Для гостевого режима фокус устанавливать не нужно
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error changing login mode");
            }
        }

        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isAuthenticating)
            {
                _ = LoginButton_ClickAsync();
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoginButton_ClickAsync();
        }

        private async Task LoginButton_ClickAsync()
        {
            if (_isAuthenticating) return;

            try
            {
                _isAuthenticating = true;
                ShowLoadingState(true);
                HideError();

                // Валидация ввода
                if (!ValidateInput())
                {
                    return;
                }

                AuthenticationResult result;

                if (DomainModeRadio?.IsChecked == true)
                {
                    // Доменная аутентификация
                    var credentials = new AuthenticationCredentials
                    {
                        Username = UsernameTextBox?.Text?.Trim() ?? "",
                        Password = PasswordBox?.Password ?? "",
                        Domain = DomainTextBox?.Text?.Trim() ?? "",
                        IsServiceAccount = false
                    };

                    result = await AuthenticateWithCredentialsAsync(credentials);
                }
                else if (LocalModeRadio?.IsChecked == true)
                {
                    // Локальная аутентификация
                    var username = LocalUsernameTextBox?.Text?.Trim() ?? "";
                    var password = LocalPasswordBox?.Password ?? "";
                    
                    // Определяем тип аутентификации: serviceadmin через LocalService, остальные через LocalUsers
                    var isServiceAdmin = username.Equals("serviceadmin", StringComparison.OrdinalIgnoreCase);
                    
                    var credentials = new AuthenticationCredentials
                    {
                        Username = username,
                        Password = password,
                        IsServiceAccount = isServiceAdmin,
                        AuthenticationType = isServiceAdmin ? AuthenticationType.LocalService : AuthenticationType.LocalUsers
                    };

                    result = await AuthenticateWithCredentialsAsync(credentials);
                }
                else
                {
                    // Гостевая аутентификация
                    var credentials = new AuthenticationCredentials
                    {
                        Username = "guest",
                        Password = "", // Пароль не нужен для гостевого режима
                        IsServiceAccount = false,
                        AuthenticationType = AuthenticationType.Guest
                    };

                    result = await AuthenticateWithCredentialsAsync(credentials);
                }

                if (result.IsSuccess)
                {
                    // УПРОЩЕНИЕ: При рестарте процесса все приложения автоматически закроются
                    // Нет необходимости в сложной логике закрытия отдельных экземпляров
                    _logger?.LogInformation("User authentication successful: {Username}", result.User?.Username);
                    
                    AuthenticationResult = result;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Login error");
                ShowError(string.Format(LocalizationHelper.Instance.GetString("Error_General") + ": {0}", ex.Message));
            }
            finally
            {
                _isAuthenticating = false;
                ShowLoadingState(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Используем ту же логику аутентификации что и при основном входе
                if (!ValidateInput())
                {
                    return;
                }

                ShowLoadingState(true);
                HideError();
                
                // Аутентификация с теми же данными что заполнены в форме
                AuthenticationResult result = await GetAuthenticationResultFromCurrentInputAsync();
                
                if (!result.IsSuccess)
                {
                    ShowError(result.ErrorMessage);
                    return;
                }

                // Проверяем права администратора
                if (result.User.Role < UserRole.Administrator)
                {
                    ShowError("Недостаточно прав доступа. Для настроек БД требуются права администратора.");
                    return;
                }

                _logger?.LogInformation("Database settings access granted for admin user: {Username}", result.User.Username);

                // Показываем окно настроек БД
                var settingsWindow = new DatabaseSettingsWindow { Owner = this };
                var settingsResult = settingsWindow.ShowDialog();
                
                if (settingsResult == true && settingsWindow.ResultConfiguration != null)
                {
                    _logger?.LogInformation("Database configuration updated by admin {Admin}, type: {DatabaseType}", 
                        result.User.Username, settingsWindow.ResultConfiguration.DatabaseType);
                        
                    CheckDomainAvailabilityAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Settings button error");
                ShowError($"Ошибка при открытии настроек: {ex.Message}");
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        #endregion

        #region Authentication Methods

        private async Task<AuthenticationResult> GetAuthenticationResultFromCurrentInputAsync()
        {
            // Та же логика что в LoginButton_ClickAsync()
            if (DomainModeRadio?.IsChecked == true)
            {
                var credentials = new AuthenticationCredentials
                {
                    Username = UsernameTextBox?.Text?.Trim() ?? "",
                    Password = PasswordBox?.Password ?? "",
                    Domain = DomainTextBox?.Text?.Trim() ?? "",
                    IsServiceAccount = false
                };
                return await AuthenticateWithCredentialsAsync(credentials);
            }
            else if (LocalModeRadio?.IsChecked == true)
            {
                var username = LocalUsernameTextBox?.Text?.Trim() ?? "";
                var password = LocalPasswordBox?.Password ?? "";
                var isServiceAdmin = username.Equals("serviceadmin", StringComparison.OrdinalIgnoreCase);
                
                var credentials = new AuthenticationCredentials
                {
                    Username = username,
                    Password = password,
                    IsServiceAccount = isServiceAdmin,
                    AuthenticationType = isServiceAdmin ? AuthenticationType.LocalService : AuthenticationType.LocalUsers
                };
                return await AuthenticateWithCredentialsAsync(credentials);
            }
            else
            {
                return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials, "Гостевой режим не поддерживает доступ к настройкам");
            }
        }

        private async Task<AuthenticationResult> AuthenticateWithCredentialsAsync(AuthenticationCredentials credentials)
        {
            try
            {
                // Реальная аутентификация через сервис (DI гарантирует наличие сервиса)
                LoadingText.Text = credentials.AuthenticationType == AuthenticationType.LocalUsers
                    ? "Аутентификация локального пользователя..."
                    : LocalizationHelper.Instance.GetString("LoginWindow_AuthenticatingDomain");

                return await _authService.AuthenticateAsync(credentials);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Authentication error");
                return AuthenticationResult.Failure(AuthenticationStatus.NetworkError, ex.Message);
            }
        }

        private AuthenticationResult CreateFallbackAuthResult(AuthenticationCredentials credentials)
        {
            // Простая fallback аутентификация для тестирования
            var user = new User
            {
                Id = new Random().Next(1000, 9999),
                Username = credentials.Username,
                DisplayName = $"Test User ({credentials.Username})",
                Email = $"{credentials.Username}@{credentials.Domain}",
                Role = credentials.IsServiceAccount ? UserRole.Administrator : UserRole.Standard,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            return AuthenticationResult.Success(user,
                credentials.IsServiceAccount ? AuthenticationType.LocalService : AuthenticationType.DomainLDAP,
                credentials.Domain);
        }

        #endregion

        #region Validation

        private bool ValidateInput()
        {
            if (DomainModeRadio?.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox?.Text))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterUsername"));
                    UsernameTextBox?.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(PasswordBox?.Password))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterPassword"));
                    PasswordBox?.Focus();
                    return false;
                }
            }
            else if (LocalModeRadio?.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(LocalUsernameTextBox?.Text))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterUsername"));
                    LocalUsernameTextBox?.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(LocalPasswordBox?.Password))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterPassword"));
                    LocalPasswordBox?.Focus();
                    return false;
                }
            }
            else if (GuestModeRadio?.IsChecked == true)
            {
                // Для гостевого режима валидация не нужна
                return true;
            }

            return true;
        }

        #endregion

        #region UI State Management

        private void ShowLoadingState(bool isLoading)
        {
            try
            {
                if (LoadingOverlay != null)
                {
                    LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                }

                if (LoginButton != null)
                {
                    LoginButton.IsEnabled = !isLoading;
                }

                if (isLoading && LoadingText != null)
                {
                    LoadingText.Text = LocalizationHelper.Instance.GetString("LoginWindow_Authenticating");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating loading state");
            }
        }

        private void ShowError(string message)
        {
            try
            {
                if (ErrorPanel != null && ErrorTextBlock != null)
                {
                    ErrorTextBlock.Text = message;
                    ErrorPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    // Fallback к MessageBox
                    MessageBox.Show(message, LocalizationHelper.Instance.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing error message");
            }
        }

        private void HideError()
        {
            try
            {
                if (ErrorPanel != null)
                {
                    ErrorPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error hiding error message");
            }
        }

        private async void CheckDomainAvailabilityAsync()
        {
            try
            {
                if (ConnectionStatusIndicator == null || ConnectionStatusText == null)
                    return;

                var isAvailable = await _authService.IsDomainAvailableAsync(null);

                ConnectionStatusIndicator.Fill = isAvailable
                    ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                    : (System.Windows.Media.Brush)FindResource("ErrorBrush");

                ConnectionStatusText.Text = isAvailable
                    ? LocalizationHelper.Instance.GetString("LoginWindow_DomainAvailable")
                    : LocalizationHelper.Instance.GetString("LoginWindow_DomainUnavailable");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking domain availability");
            }
        }

        #endregion

        #region Fallback Window Creation

        private void CreateFallbackWindow()
        {
            // Простое окно для случаев когда XAML не работает
            Title = "System Login";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock { Text = "Username:", Margin = new Thickness(0, 0, 0, 5) });
            var usernameBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(usernameBox);

            panel.Children.Add(new TextBlock { Text = "Password:", Margin = new Thickness(0, 0, 0, 5) });
            var passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(passwordBox);

            var loginBtn = new Button { Content = "Login", Height = 30 };
            loginBtn.Click += async (s, e) => await FallbackLogin(usernameBox.Text, passwordBox.Password);
            panel.Children.Add(loginBtn);

            Content = panel;
        }

        private async Task FallbackLogin(string username, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter username and password");
                    return;
                }

                var credentials = new AuthenticationCredentials
                {
                    Username = username,
                    Password = password,
                    Domain = "local"
                };

                AuthenticationResult = CreateFallbackAuthResult(credentials);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}");
            }
        }

        #endregion

        #region Static Methods

        public static LoginWindow ShowLoginDialog(IAuthenticationService authService, ILogger<LoginWindow> logger, IServiceScopeFactory serviceScopeFactory, string domain = null, string username = null, bool serviceMode = false)
        {
            var window = new LoginWindow(authService, logger, serviceScopeFactory);

            try
            {
                if (!string.IsNullOrEmpty(domain) && window.DomainTextBox != null)
                    window.DomainTextBox.Text = domain;

                if (!string.IsNullOrEmpty(username))
                {
                    if (serviceMode && window.LocalUsernameTextBox != null)
                        window.LocalUsernameTextBox.Text = username;
                    else if (window.UsernameTextBox != null)
                        window.UsernameTextBox.Text = username;
                }

                if (serviceMode && window.LocalModeRadio != null)
                    window.LocalModeRadio.IsChecked = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting initial values: {ex.Message}");
            }

            return window;
        }

        #endregion

        private void DomainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var virtualKeyboardService = _serviceScopeFactory.CreateScopedService<IVirtualKeyboardService>();
                
                // Сначала выполняем диагностику
                var diagnosis = await virtualKeyboardService.DiagnoseVirtualKeyboardAsync();
                _logger?.LogInformation("Диагностика перед показом клавиатуры:\n{Diagnosis}", diagnosis);
                
                // Затем пытаемся показать клавиатуру
                var success = await virtualKeyboardService.ShowVirtualKeyboardAsync();
                
                if (!success)
                {
                    // Дополнительная попытка принудительного позиционирования
                    _logger?.LogInformation("Первая попытка не удалась, пытаемся принудительное позиционирование");
                    success = await virtualKeyboardService.RepositionKeyboardAsync();
                }
                
                if (!success)
                {
                    // Если всё ещё не удалось, показываем диагностику пользователю
                    var finalDiagnosis = await virtualKeyboardService.DiagnoseVirtualKeyboardAsync();
                    MessageBox.Show($"Не удалось показать виртуальную клавиатуру.\n\nДиагностика:\n{finalDiagnosis}", 
                                  "Диагностика виртуальной клавиатуры", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
                else
                {
                    _logger?.LogInformation("Виртуальная клавиатура успешно показана");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling virtual keyboard");
                MessageBox.Show($"Ошибка при вызове виртуальной клавиатуры: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Автоматическая очистка теперь выполняется GlobalTouchKeyboardManager
            base.OnClosed(e);
        }
    }
}
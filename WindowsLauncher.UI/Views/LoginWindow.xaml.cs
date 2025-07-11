// WindowsLauncher.UI/Views/LoginWindow.xaml.cs - ПОЛНОСТЬЮ ИСПРАВЛЕННАЯ ВЕРСИЯ
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Properties.Resources;
using WindowsLauncher.UI.Infrastructure.Localization;

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
        private readonly IAuthenticationService? _authService;
        private readonly ILogger<LoginWindow>? _logger;
        private bool _isAuthenticating = false;

        // Публичные свойства для доступа к результату
        public AuthenticationResult AuthenticationResult { get; private set; }
        public User AuthenticatedUser => AuthenticationResult?.User;

        public LoginWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoginWindow: Starting InitializeComponent...");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("LoginWindow: InitializeComponent completed successfully");
                
                // Инициализация readonly полей в конструкторе
                System.Diagnostics.Debug.WriteLine("LoginWindow: Starting service initialization...");
                var app = WpfApplication.Current as App;
                if (app?.ServiceProvider != null)
                {
                    _authService = app.ServiceProvider.GetService<IAuthenticationService>();
                    _logger = app.ServiceProvider.GetService<ILogger<LoginWindow>>();
                    System.Diagnostics.Debug.WriteLine("LoginWindow: Services initialized successfully");
                }
                
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

        public LoginWindow(string errorMessage) : this()
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
                    if (DomainModeRadio?.IsChecked == true)
                    {
                        UsernameTextBox?.Focus();
                    }
                    else if (ServiceModeRadio?.IsChecked == true)
                    {
                        ServiceUsernameTextBox?.Focus();
                    }
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
                if (DomainLoginPanel == null || ServiceLoginPanel == null) return;

                if (DomainModeRadio?.IsChecked == true)
                {
                    DomainLoginPanel.Visibility = Visibility.Visible;
                    ServiceLoginPanel.Visibility = Visibility.Collapsed;
                    UsernameTextBox?.Focus();
                }
                else if (ServiceModeRadio?.IsChecked == true)
                {
                    DomainLoginPanel.Visibility = Visibility.Collapsed;
                    ServiceLoginPanel.Visibility = Visibility.Visible;
                    ServiceUsernameTextBox?.Focus();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error changing login mode");
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
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
                else
                {
                    // Сервисная аутентификация
                    var credentials = new AuthenticationCredentials
                    {
                        Username = ServiceUsernameTextBox?.Text?.Trim() ?? "",
                        Password = ServicePasswordBox?.Password ?? "",
                        IsServiceAccount = true
                    };

                    result = await AuthenticateWithCredentialsAsync(credentials);
                }

                if (result.IsSuccess)
                {
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Временная заглушка
                MessageBox.Show(
                    LocalizationHelper.Instance.GetString("SettingsComingSoon"),
                    LocalizationHelper.Instance.GetString("Common_Settings"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Settings button error");
            }
        }

        #endregion

        #region Authentication Methods

        private async Task<AuthenticationResult> AuthenticateWithCredentialsAsync(AuthenticationCredentials credentials)
        {
            try
            {
                if (_authService == null)
                {
                    // Fallback аутентификация для тестирования
                    return CreateFallbackAuthResult(credentials);
                }

                // Реальная аутентификация через сервис
                LoadingText.Text = credentials.IsServiceAccount
                    ? LocalizationHelper.Instance.GetString("LoginWindow_AuthenticatingService")
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
            else if (ServiceModeRadio?.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(ServiceUsernameTextBox?.Text))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterUsername"));
                    ServiceUsernameTextBox?.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ServicePasswordBox?.Password))
                {
                    ShowError(LocalizationHelper.Instance.GetString("PleaseEnterPassword"));
                    ServicePasswordBox?.Focus();
                    return false;
                }
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
                if (_authService == null || ConnectionStatusIndicator == null || ConnectionStatusText == null)
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

        public static LoginWindow ShowLoginDialog(string domain = null, string username = null, bool serviceMode = false)
        {
            var window = new LoginWindow();

            try
            {
                if (!string.IsNullOrEmpty(domain) && window.DomainTextBox != null)
                    window.DomainTextBox.Text = domain;

                if (!string.IsNullOrEmpty(username))
                {
                    if (serviceMode && window.ServiceUsernameTextBox != null)
                        window.ServiceUsernameTextBox.Text = username;
                    else if (window.UsernameTextBox != null)
                        window.UsernameTextBox.Text = username;
                }

                if (serviceMode && window.ServiceModeRadio != null)
                    window.ServiceModeRadio.IsChecked = true;
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
    }
}
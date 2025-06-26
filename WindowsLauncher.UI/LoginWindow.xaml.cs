using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.UI
{
    public partial class LoginWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        public User? AuthenticatedUser { get; private set; }

        public LoginWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            LoadCurrentUser();
        }

        private async void LoadCurrentUser()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var result = await authService.AuthenticateAsync();
                if (result.IsSuccess && result.User != null)
                {
                    CurrentUserText.Text = $"{result.User.DisplayName} ({result.User.Username})";
                }
                else
                {
                    CurrentUserText.Text = "Windows authentication failed";
                    CurrentUserButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                CurrentUserText.Text = $"Error: {ex.Message}";
                CurrentUserButton.IsEnabled = false;
            }
        }

        private async void CurrentUserButton_Click(object sender, RoutedEventArgs e)
        {
            await AuthenticateCurrentUser();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AuthenticateWithCredentials();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async Task AuthenticateCurrentUser()
        {
            try
            {
                ShowLoading(true);

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var result = await authService.AuthenticateAsync();

                if (result.IsSuccess && result.User != null)
                {
                    AuthenticatedUser = result.User;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError($"Authentication failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task AuthenticateWithCredentials()
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter username");
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter password");
                PasswordBox.Focus();
                return;
            }

            try
            {
                ShowLoading(true);

                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var result = await authService.AuthenticateAsync(username, password);

                if (result.IsSuccess && result.User != null)
                {
                    AuthenticatedUser = result.User;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError($"Authentication failed: {result.ErrorMessage}");
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }

        private void ShowLoading(bool isLoading)
        {
            LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            LoginButton.IsEnabled = !isLoading;
            CurrentUserButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
        }
    }
}
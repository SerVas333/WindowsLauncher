using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Диалог смены пароля для локальных пользователей
    /// </summary>
    public partial class ChangePasswordDialog : Window
    {
        private readonly ILocalUserService _localUserService;
        private readonly ILogger<ChangePasswordDialog> _logger;
        private readonly int _userId;
        private readonly string _username;
        private PasswordInfo? _passwordInfo;

        public bool PasswordChanged { get; private set; }

        public ChangePasswordDialog(int userId, string username)
        {
            InitializeComponent();
            
            _userId = userId;
            _username = username;
            
            // Получаем сервисы из DI контейнера
            var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
            _localUserService = serviceProvider.GetRequiredService<ILocalUserService>();
            _logger = serviceProvider.GetRequiredService<ILogger<ChangePasswordDialog>>();

            InitializeDialog();
        }

        private async void InitializeDialog()
        {
            try
            {
                // Устанавливаем имя пользователя
                UsernameTextBlock.Text = _username;

                // Загружаем информацию о пароле
                _passwordInfo = await _localUserService.GetPasswordInfoAsync(_userId);
                UpdatePasswordInfo();

                // Загружаем требования к паролю
                await LoadPasswordRequirements();

                _logger.LogInformation("Change password dialog initialized for user: {Username} (ID: {UserId})", _username, _userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing change password dialog for user: {Username}", _username);
                ShowGeneralError("Ошибка при загрузке информации о пользователе");
            }
        }

        private void UpdatePasswordInfo()
        {
            if (_passwordInfo == null)
            {
                PasswordInfoTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            var infoText = "";
            if (_passwordInfo.LastPasswordChange.HasValue)
            {
                infoText = $"Последняя смена пароля: {_passwordInfo.LastPasswordChange.Value:dd.MM.yyyy HH:mm}";
                
                if (_passwordInfo.DaysUntilExpiry.HasValue)
                {
                    if (_passwordInfo.IsExpired)
                    {
                        infoText += " (Срок действия истек!)";
                    }
                    else
                    {
                        infoText += $" (Истекает через {_passwordInfo.DaysUntilExpiry.Value} дн.)";
                    }
                }
            }
            else
            {
                infoText = "Пароль еще не устанавливался";
            }

            PasswordInfoTextBlock.Text = infoText;
            PasswordInfoTextBlock.Visibility = Visibility.Visible;
        }

        private async Task LoadPasswordRequirements()
        {
            try
            {
                // Создаем тестовый пароль для получения требований
                var validation = await _localUserService.ValidatePasswordAsync("");
                
                var requirements = "• Минимум 8 символов\n" +
                                 "• Должен содержать строчные и заглавные буквы\n" +
                                 "• Должен содержать цифры\n" +
                                 "• Должен содержать специальные символы\n" +
                                 "• Не должен содержать запрещенные слова";

                PasswordRequirementsText.Text = requirements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading password requirements");
                PasswordRequirementsText.Text = "Не удалось загрузить требования к паролю";
            }
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ClearFieldError(CurrentPasswordErrorText);
            ValidateForm();
        }

        private async void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ClearFieldError(NewPasswordErrorText);
            await ValidateNewPassword();
            ValidateForm();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ClearFieldError(ConfirmPasswordErrorText);
            ValidatePasswordConfirmation();
            ValidateForm();
        }

        private async Task ValidateNewPassword()
        {
            try
            {
                var password = NewPasswordBox.Password;
                if (string.IsNullOrEmpty(password))
                    return;

                var validation = await _localUserService.ValidatePasswordAsync(password);
                if (!validation.IsValid)
                {
                    ShowFieldError(NewPasswordErrorText, string.Join("; ", validation.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating new password");
                ShowFieldError(NewPasswordErrorText, "Ошибка при проверке пароля");
            }
        }

        private void ValidatePasswordConfirmation()
        {
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (!string.IsNullOrEmpty(confirmPassword) && newPassword != confirmPassword)
            {
                ShowFieldError(ConfirmPasswordErrorText, "Пароли не совпадают");
            }
        }

        private void ValidateForm()
        {
            var isValid = !string.IsNullOrEmpty(CurrentPasswordBox.Password) &&
                         !string.IsNullOrEmpty(NewPasswordBox.Password) &&
                         !string.IsNullOrEmpty(ConfirmPasswordBox.Password) &&
                         NewPasswordBox.Password == ConfirmPasswordBox.Password &&
                         !HasVisibleErrors();

            ChangePasswordButton.IsEnabled = isValid;
        }

        private bool HasVisibleErrors()
        {
            return CurrentPasswordErrorText.Visibility == Visibility.Visible ||
                   NewPasswordErrorText.Visibility == Visibility.Visible ||
                   ConfirmPasswordErrorText.Visibility == Visibility.Visible;
        }

        private void ShowFieldError(TextBlock errorTextBlock, string message)
        {
            errorTextBlock.Text = message;
            errorTextBlock.Visibility = Visibility.Visible;
        }

        private void ClearFieldError(TextBlock errorTextBlock)
        {
            errorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void ShowGeneralError(string message)
        {
            GeneralErrorText.Text = message;
            GeneralErrorCard.Visibility = Visibility.Visible;
        }

        private void ClearGeneralError()
        {
            GeneralErrorCard.Visibility = Visibility.Collapsed;
        }

        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ClearGeneralError();
                ChangePasswordButton.IsEnabled = false;
                ChangePasswordButton.Content = "Смена пароля...";

                var currentPassword = CurrentPasswordBox.Password;
                var newPassword = NewPasswordBox.Password;

                _logger.LogInformation("Attempting to change password for user: {Username} (ID: {UserId})", _username, _userId);

                var result = await _localUserService.ChangeLocalUserPasswordAsync(_userId, currentPassword, newPassword);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Password changed successfully for user: {Username}", _username);
                    
                    PasswordChanged = true;
                    MessageBox.Show(
                        result.Message ?? "Пароль успешно изменен",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    
                    DialogResult = true;
                    Close();
                }
                else
                {
                    _logger.LogWarning("Password change failed for user: {Username}. Error: {Error}", _username, result.ErrorMessage);
                    ShowGeneralError(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {Username}", _username);
                ShowGeneralError("Произошла ошибка при смене пароля. Попробуйте позже.");
            }
            finally
            {
                ChangePasswordButton.IsEnabled = true;
                ChangePasswordButton.Content = "Сменить пароль";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                var virtualKeyboardService = serviceProvider.GetRequiredService<IVirtualKeyboardService>();
                await virtualKeyboardService.ToggleVirtualKeyboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling virtual keyboard");
                MessageBox.Show($"Ошибка при переключении виртуальной клавиатуры: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Фокусируемся на поле текущего пароля
            CurrentPasswordBox.Focus();
        }
    }
}
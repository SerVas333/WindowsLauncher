// WindowsLauncher.UI/Views/LocalUserDialog.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Диалоговое окно для создания/редактирования локальных пользователей
    /// </summary>
    public partial class LocalUserDialog : Window
    {
        private LocalUserDialogViewModel? _viewModel;

        public LocalUserDialog()
        {
            InitializeComponent();
            
            // Подписываемся на изменения пароля
            PasswordBox.PasswordChanged += OnPasswordChanged;
            ConfirmPasswordBox.PasswordChanged += OnConfirmPasswordChanged;
        }

        public LocalUserDialog(LocalUserDialogViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Подписываемся на событие закрытия диалога
            _viewModel.DialogClosed += OnDialogClosed;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is PasswordBox passwordBox)
            {
                _viewModel.Password = passwordBox.Password;
            }
        }

        private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is PasswordBox passwordBox)
            {
                _viewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        private void OnDialogClosed(object? sender, bool result)
        {
            DialogResult = result;
            Close();
        }

        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                var virtualKeyboardService = serviceProvider.GetRequiredService<IVirtualKeyboardService>();
                await virtualKeyboardService.ToggleVirtualKeyboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переключении виртуальной клавиатуры: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Отписываемся от событий
            if (_viewModel != null)
            {
                _viewModel.DialogClosed -= OnDialogClosed;
            }
            
            PasswordBox.PasswordChanged -= OnPasswordChanged;
            ConfirmPasswordBox.PasswordChanged -= OnConfirmPasswordChanged;
            
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Аргументы события закрытия диалога
    /// </summary>
    public class DialogClosedEventArgs : EventArgs
    {
        public bool DialogResult { get; }
        
        public DialogClosedEventArgs(bool dialogResult)
        {
            DialogResult = dialogResult;
        }
    }
}
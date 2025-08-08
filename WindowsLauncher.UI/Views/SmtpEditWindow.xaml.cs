using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Interaction logic for SmtpEditWindow.xaml
    /// </summary>
    public partial class SmtpEditWindow : Window
    {
        private SmtpEditViewModel? ViewModel => DataContext as SmtpEditViewModel;
        
        public SmtpEditWindow()
        {
            InitializeComponent();
        }
        
        public SmtpEditWindow(SmtpEditViewModel viewModel) : this()
        {
            DataContext = viewModel;
            
            // Subscribe to commands
            if (viewModel != null)
            {
                viewModel.SaveCompleted += OnSaveCompleted;
                viewModel.CancelRequested += OnCancelRequested;
            }
        }
        
        /// <summary>
        /// Обработка завершения сохранения
        /// </summary>
        private void OnSaveCompleted(object? sender, EventArgs e)
        {
            // Безопасно устанавливаем DialogResult только если окно открыто как диалог
            try
            {
                DialogResult = true;
                System.Diagnostics.Debug.WriteLine($"SmtpEditWindow: DialogResult set to true successfully");
            }
            catch (InvalidOperationException ex)
            {
                // Окно не открыто как диалог, просто закрываем без установки DialogResult
                System.Diagnostics.Debug.WriteLine($"SmtpEditWindow: Could not set DialogResult - {ex.Message}");
            }
            Close();
        }
        
        /// <summary>
        /// Обработка отмены
        /// </summary>
        private void OnCancelRequested(object? sender, EventArgs e)
        {
            // Безопасно устанавливаем DialogResult только если окно открыто как диалог
            try
            {
                DialogResult = false;
                System.Diagnostics.Debug.WriteLine($"SmtpEditWindow: DialogResult set to false successfully");
            }
            catch (InvalidOperationException ex)
            {
                // Окно не открыто как диалог, просто закрываем без установки DialogResult
                System.Diagnostics.Debug.WriteLine($"SmtpEditWindow: Could not set DialogResult - {ex.Message}");
            }
            Close();
        }
        
        /// <summary>
        /// Обработка изменения пароля в PasswordBox
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox && ViewModel != null)
            {
                // Обновляем пароль в ViewModel только если пароль скрыт
                if (!ViewModel.IsPasswordVisible)
                {
                    ViewModel.Password = passwordBox.Password;
                }
            }
        }
        
        /// <summary>
        /// Обработка загрузки окна
        /// </summary>
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            
            // Устанавливаем пароль в PasswordBox при загрузке
            if (ViewModel != null && !string.IsNullOrEmpty(ViewModel.Password))
            {
                PasswordBox.Password = ViewModel.Password;
                PasswordTextBox.Text = ViewModel.Password;
            }
            
            // Подписываемся на изменения видимости пароля для синхронизации
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }
        
        /// <summary>
        /// Обработка изменений свойств ViewModel
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ViewModel == null) return;
            
            switch (e.PropertyName)
            {
                case nameof(SmtpEditViewModel.IsPasswordVisible):
                    // Синхронизируем пароль при переключении видимости
                    if (ViewModel.IsPasswordVisible)
                    {
                        // Переключили на видимый - копируем из PasswordBox в TextBox
                        PasswordTextBox.Text = PasswordBox.Password;
                    }
                    else
                    {
                        // Переключили на скрытый - копируем из TextBox в PasswordBox
                        PasswordBox.Password = PasswordTextBox.Text;
                    }
                    break;
                case nameof(SmtpEditViewModel.Password):
                    // Синхронизируем оба поля при изменении пароля в ViewModel
                    if (!ViewModel.IsPasswordVisible && PasswordBox.Password != ViewModel.Password)
                    {
                        PasswordBox.Password = ViewModel.Password;
                    }
                    if (ViewModel.IsPasswordVisible && PasswordTextBox.Text != ViewModel.Password)
                    {
                        PasswordTextBox.Text = ViewModel.Password;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Обработка закрытия окна
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Проверяем несохраненные изменения
            if (ViewModel != null && ViewModel.HasUnsavedChanges && DialogResult != true)
            {
                var result = MessageBox.Show(
                    "У вас есть несохраненные изменения. Вы уверены, что хотите закрыть окно без сохранения?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            
            // Отписываемся от событий
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.SaveCompleted -= OnSaveCompleted;
                ViewModel.CancelRequested -= OnCancelRequested;
            }
            
            base.OnClosing(e);
        }
    }
}
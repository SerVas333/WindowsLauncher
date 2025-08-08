using System.Windows;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Interaction logic for SmtpSettingsWindow.xaml
    /// </summary>
    public partial class SmtpSettingsWindow : Window
    {
        private SmtpSettingsViewModel? ViewModel => DataContext as SmtpSettingsViewModel;
        
        public SmtpSettingsWindow()
        {
            InitializeComponent();
        }
        
        public SmtpSettingsWindow(SmtpSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
            
            // Subscribe to close event
            if (viewModel != null)
            {
                viewModel.CloseRequested += OnCloseRequested;
            }
        }
        
        /// <summary>
        /// Обработка запроса на закрытие окна
        /// </summary>
        private void OnCloseRequested(object? sender, System.EventArgs e)
        {
            // Безопасно устанавливаем DialogResult только если окно открыто как диалог
            try
            {
                DialogResult = true;
                System.Diagnostics.Debug.WriteLine($"SmtpSettingsWindow: DialogResult set to true successfully");
            }
            catch (System.InvalidOperationException ex)
            {
                // Окно не открыто как диалог, просто закрываем без установки DialogResult
                System.Diagnostics.Debug.WriteLine($"SmtpSettingsWindow: Could not set DialogResult - {ex.Message}");
            }
            Close();
        }
        
        /// <summary>
        /// Обработка закрытия окна
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Проверяем несохраненные изменения в открытых окнах редактирования
            if (ViewModel != null && ViewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "У вас есть несохраненные изменения в открытых окнах редактирования SMTP серверов.\n" +
                    "Вы уверены, что хотите закрыть настройки SMTP без сохранения изменений?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            
            // Отписываемся от событий и очищаем ресурсы при закрытии
            if (ViewModel != null)
            {
                ViewModel.CloseRequested -= OnCloseRequested;
                ViewModel.Cleanup();
            }
            
            base.OnClosing(e);
        }
    }
}
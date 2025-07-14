using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Extensions;
using WindowsLauncher.Services;
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.UI.Infrastructure.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsLauncher.UI
{
    public partial class MainWindow : Window
    {
        private bool _isViewModelInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            try
            {
                // Предотвращаем дублирование инициализации
                if (_isViewModelInitialized)
                    return;

                // Получаем ViewModel через DI
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                var viewModel = serviceProvider.GetRequiredService<MainViewModel>();

                DataContext = viewModel;
                _isViewModelInitialized = true;
            }
            catch (Exception ex)
            {
                // Базовая обработка ошибок если DI не работает
                MessageBox.Show($"Failed to initialize application: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
            {
                var culture = item.Tag.ToString();
                if (!string.IsNullOrEmpty(culture))
                {
                    LocalizationHelper.Instance.SetLanguage(culture);
                }
            }
        }
    }
}
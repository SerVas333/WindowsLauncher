using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.Data;
using WindowsLauncher.Services;
using WindowsLauncher.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsLauncher.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 🔄 УБИРАЕМ всю бизнес-логику из конструктора
            // 🆕 ViewModel теперь инжектируется через DI в App.xaml.cs или здесь
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            try
            {
                // Получаем ViewModel через DI
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                DataContext = serviceProvider.GetRequiredService<MainViewModel>();
            }
            catch (Exception ex)
            {
                // Базовая обработка ошибок если DI не работает
                MessageBox.Show($"Failed to initialize application: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // 🔄 ОСТАВЛЯЕМ только UI логику
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
            {
                var culture = item.Tag.ToString();
                if (!string.IsNullOrEmpty(culture))
                {
                    LocalizationManager.SetLanguage(culture);
                }
            }
        }

        // 🔄 УДАЛЯЕМ методы TestDatabase() и TestAD() - они должны быть в сервисном слое
        // 🔄 УДАЛЯЕМ InitializeAsync() - это ответственность ViewModel
    }
}
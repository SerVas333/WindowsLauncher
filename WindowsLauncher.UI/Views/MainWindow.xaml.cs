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
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.UI
{
    public partial class MainWindow : Window
    {
        private bool _isViewModelInitialized = false;
        private double _originalHeight;
        private const double KEYBOARD_HEIGHT_ESTIMATE = 300; // Примерная высота виртуальной клавиатуры

        public MainWindow()
        {
            InitializeComponent();
            _originalHeight = Height;
            InitializeViewModel();
            SubscribeToVirtualKeyboardEvents();
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

        private void SubscribeToVirtualKeyboardEvents()
        {
            try
            {
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                var virtualKeyboardService = serviceProvider.GetService<IVirtualKeyboardService>();
                
                if (virtualKeyboardService != null)
                {
                    virtualKeyboardService.StateChanged += OnVirtualKeyboardStateChanged;
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу приложения
                System.Diagnostics.Debug.WriteLine($"Failed to subscribe to virtual keyboard events: {ex.Message}");
            }
        }

        private void OnVirtualKeyboardStateChanged(object? sender, VirtualKeyboardStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (e.IsVisible)
                    {
                        // Виртуальная клавиатура показана - поднимаем окно выше
                        var screenHeight = SystemParameters.PrimaryScreenHeight;
                        var newTop = Math.Max(0, screenHeight - Height - KEYBOARD_HEIGHT_ESTIMATE - 50);
                        
                        if (Top > newTop)
                        {
                            Top = newTop;
                        }
                    }
                    else
                    {
                        // Виртуальная клавиатура скрыта - возвращаем окно в центр если нужно
                        if (WindowStartupLocation == WindowStartupLocation.CenterScreen)
                        {
                            var screenHeight = SystemParameters.PrimaryScreenHeight;
                            var screenWidth = SystemParameters.PrimaryScreenWidth;
                            Top = (screenHeight - Height) / 2;
                            Left = (screenWidth - Width) / 2;
                        }
                    }

                    // Обновляем состояние в ViewModel
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.IsVirtualKeyboardVisible = e.IsVisible;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling virtual keyboard state change: {ex.Message}");
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Отписываемся от событий при закрытии окна
                var serviceProvider = ((App)Application.Current).ServiceProvider;
                var virtualKeyboardService = serviceProvider.GetService<IVirtualKeyboardService>();
                
                if (virtualKeyboardService != null)
                {
                    virtualKeyboardService.StateChanged -= OnVirtualKeyboardStateChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unsubscribing from virtual keyboard events: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}
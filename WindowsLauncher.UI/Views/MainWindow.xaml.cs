using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Extensions;
using WindowsLauncher.Services;
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.UI.Infrastructure.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Helpers;
using WindowsLauncher.UI.Services;

namespace WindowsLauncher.UI
{
    public partial class MainWindow : Window
    {
        private bool _isViewModelInitialized = false;
        private double _originalHeight;
        private const double KEYBOARD_HEIGHT_ESTIMATE = 300; // Примерная высота виртуальной клавиатуры
        private ISessionManagementService? _sessionManager;
        private ILogger<MainWindow>? _logger;
        private GlobalHotKeyService? _globalHotKeyService;
        private AppSwitcherService? _appSwitcherService;
        private ShellMode _currentShellMode = ShellMode.Normal;

        public MainWindow()
        {
            InitializeComponent();
            _originalHeight = Height;
            InitializeViewModel();
            SubscribeToVirtualKeyboardEvents();
            InitializeSessionManagement();
            _ = InitializeAppSwitcherAsync(); // Инициализируем асинхронно
        }

        private void InitializeViewModel()
        {
            try
            {
                // Предотвращаем дублирование инициализации
                if (_isViewModelInitialized)
                    return;

                // Получаем ViewModel через DI
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                var viewModel = serviceProvider.GetRequiredService<MainViewModel>();

                DataContext = viewModel;
                _isViewModelInitialized = true;
            }
            catch (Exception ex)
            {
                // Базовая обработка ошибок если DI не работает
                MessageBox.Show($"Failed to initialize application: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
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
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
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

        private void InitializeSessionManagement()
        {
            try
            {
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                _sessionManager = serviceProvider.GetService<ISessionManagementService>();
                _logger = serviceProvider.GetService<ILogger<MainWindow>>();
                
                if (_sessionManager != null)
                {
                    // Загружаем конфигурацию сессии
                    _ = Task.Run(async () => await _sessionManager.LoadConfigurationAsync());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing session management: {ex.Message}");
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // ОТКЛЮЧЕНО: Старая логика с автоперезапуском конфликтует с новой логикой в App.xaml.cs
                // Теперь вся логика закрытия обрабатывается в App.xaml.cs HandleMainWindowClosedAsync()
                _logger?.LogInformation("MainWindow closing event - new logic in App.xaml.cs will handle this");
                
                // Просто разрешаем закрытие - логика обработается в App.xaml.cs
                // (старый код с SessionManager отключен)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling window closing: {ex.Message}");
                // В случае ошибки разрешаем закрытие
            }
            
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Отписываемся от событий при закрытии окна
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                var virtualKeyboardService = serviceProvider.GetService<IVirtualKeyboardService>();
                
                if (virtualKeyboardService != null)
                {
                    virtualKeyboardService.StateChanged -= OnVirtualKeyboardStateChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up resources: {ex.Message}");
            }

            // Освобождаем ресурсы переключателя приложений
            try
            {
                _disposed = true; // Останавливаем таймер обновления
                _globalHotKeyService?.Dispose();
                _appSwitcherService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing app switcher resources: {ex.Message}");
            }
            
            base.OnClosed(e);
        }

        /// <summary>
        /// Инициализация переключателя приложений и глобальных хоткеев
        /// </summary>
        private async Task InitializeAppSwitcherAsync()
        {
            try
            {
                var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                _logger = serviceProvider.GetService<ILogger<MainWindow>>();

                // Определяем режим работы приложения
                var shellModeDetectionService = serviceProvider.GetService<ShellModeDetectionService>();
                if (shellModeDetectionService != null)
                {
                    _currentShellMode = await shellModeDetectionService.DetectShellModeAsync();
                }
                else
                {
                    _currentShellMode = ShellMode.Normal;
                }

                // Создаем сервисы
                _globalHotKeyService = serviceProvider.GetService<GlobalHotKeyService>();
                _appSwitcherService = serviceProvider.GetService<AppSwitcherService>();

                if (_globalHotKeyService == null || _appSwitcherService == null)
                {
                    _logger?.LogWarning("App switcher services not registered in DI container");
                    return;
                }

                // Инициализируем сервис глобальных хоткеев с определенным режимом
                await _globalHotKeyService.InitializeAsync(this, _currentShellMode);

                // Подписываемся на события хоткеев
                _globalHotKeyService.AltTabPressed += OnAltTabPressed;
                _globalHotKeyService.CtrlAltTabPressed += OnCtrlAltTabPressed;

                // Логируем информацию о режиме и доступных хоткеях
                var modeDescription = shellModeDetectionService?.GetModeDescription(_currentShellMode) ?? "Unknown";
                var hotKeysDescription = shellModeDetectionService?.GetHotKeysDescription(_currentShellMode) ?? "Unknown";
                
                _logger?.LogInformation("Application switcher initialized successfully");
                _logger?.LogInformation("Running in: {ModeDescription}", modeDescription);
                _logger?.LogInformation("Available hotkeys: {HotKeysDescription}", hotKeysDescription);

                // Обновляем UI кнопки переключателя
                UpdateAppSwitcherButtonState(modeDescription, hotKeysDescription);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing application switcher");
            }
        }

        /// <summary>
        /// Обработчик нажатия Alt+Tab
        /// </summary>
        private async void OnAltTabPressed(object? sender, EventArgs e)
        {
            try
            {
                if (_appSwitcherService != null)
                {
                    await _appSwitcherService.ShowAppSwitcherAsync(_currentShellMode);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling Alt+Tab press");
            }
        }

        /// <summary>
        /// Обработчик нажатия Ctrl+Alt+Tab
        /// </summary>
        private async void OnCtrlAltTabPressed(object? sender, EventArgs e)
        {
            try
            {
                if (_appSwitcherService != null)
                {
                    // Показываем переключатель в режиме обратного переключения
                    await _appSwitcherService.ShowAppSwitcherAsync(_currentShellMode);
                    await _appSwitcherService.SelectPreviousApplicationAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling Ctrl+Alt+Tab press");
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки AppSwitcher в статус баре
        /// </summary>
        private async void AppSwitcherButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_appSwitcherService != null)
                {
                    await _appSwitcherService.ShowAppSwitcherAsync(_currentShellMode);
                    _logger?.LogDebug("App switcher opened via status bar button");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening app switcher from status bar button");
            }
        }

        /// <summary>
        /// Обновить состояние кнопки переключателя приложений
        /// </summary>
        private void UpdateAppSwitcherButtonState(string modeDescription, string hotKeysDescription)
        {
            try
            {
                // Обновляем tooltip с информацией о режиме
                if (AppSwitcherTooltipText != null)
                {
                    AppSwitcherTooltipText.Text = $"{modeDescription}\nГорячие клавиши: {hotKeysDescription}";
                }

                // Запускаем периодическое обновление счетчика приложений
                _ = StartAppCountUpdateTimerAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating app switcher button state");
            }
        }

        /// <summary>
        /// Запустить таймер для обновления счетчика приложений
        /// </summary>
        private async Task StartAppCountUpdateTimerAsync()
        {
            await Task.Run(async () =>
            {
                while (!_disposed)
                {
                    try
                    {
                        if (_appSwitcherService != null)
                        {
                            var serviceProvider = ((App)System.Windows.Application.Current).ServiceProvider;
                            var runningAppsService = serviceProvider.GetService<IRunningApplicationsService>();
                            
                            if (runningAppsService != null)
                            {
                                var appCount = await runningAppsService.GetRunningApplicationsCountAsync();
                                
                                // Обновляем UI в главном потоке
                                Dispatcher.BeginInvoke(() =>
                                {
                                    UpdateAppCountDisplay(appCount);
                                });
                            }
                        }

                        await Task.Delay(2000); // Обновляем каждые 2 секунды
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Error updating app count display");
                        await Task.Delay(5000); // Увеличиваем интервал при ошибке
                    }
                }
            });
        }

        /// <summary>
        /// Обновить отображение счетчика приложений
        /// </summary>
        private void UpdateAppCountDisplay(int appCount)
        {
            try
            {
                if (AppCountText != null && AppCountBadge != null)
                {
                    if (appCount > 0)
                    {
                        AppCountText.Text = appCount > 9 ? "9+" : appCount.ToString();
                        AppCountBadge.Visibility = Visibility.Visible;
                        
                        // Включаем кнопку если есть приложения для переключения
                        if (AppSwitcherButton != null)
                        {
                            AppSwitcherButton.IsEnabled = true;
                        }
                    }
                    else
                    {
                        AppCountBadge.Visibility = Visibility.Collapsed;
                        
                        // Отключаем кнопку если нет приложений
                        if (AppSwitcherButton != null)
                        {
                            AppSwitcherButton.IsEnabled = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error updating app count display");
            }
        }

        /// <summary>
        /// Флаг для остановки таймера при закрытии окна
        /// </summary>
        private bool _disposed = false;
    }
}
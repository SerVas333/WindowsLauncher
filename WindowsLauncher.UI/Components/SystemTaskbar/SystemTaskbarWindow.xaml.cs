using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Services;
using WindowsLauncher.UI.Services;
using WindowsLauncher.UI.Helpers;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Components.SystemTaskbar
{
    /// <summary>
    /// Системная панель задач для Shell-режима
    /// </summary>
    public partial class SystemTaskbarWindow : Window
    {
        private readonly ILogger<SystemTaskbarWindow>? _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly AppSwitcherService? _appSwitcherService;
        private readonly ShellModeDetectionService? _shellModeDetectionService;
        
        private DispatcherTimer? _clockTimer;
        private ShellMode _currentShellMode = ShellMode.Normal;
        private bool _disposed = false;

        public SystemTaskbarWindow()
        {
            InitializeComponent();
            
            // Получаем сервисы из DI контейнера
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                _serviceProvider = app.ServiceProvider;
                _logger = app.ServiceProvider.GetService<ILogger<SystemTaskbarWindow>>();
                _appSwitcherService = app.ServiceProvider.GetService<AppSwitcherService>();
                _shellModeDetectionService = app.ServiceProvider.GetService<ShellModeDetectionService>();
            }

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            try
            {
                // Устанавливаем размеры и позицию панели задач
                SetTaskbarPosition();
                
                // Запускаем таймер для обновления времени
                StartClockTimer();
                
                // Инициализируем состояние кнопки AppSwitcher
                InitializeAppSwitcherButton();
                
                // Определяем режим Shell
                _ = DetectShellModeAsync();
                
                _logger?.LogInformation("SystemTaskbar initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing SystemTaskbar");
            }
        }

        /// <summary>
        /// Установка позиции и размеров панели задач
        /// </summary>
        private void SetTaskbarPosition()
        {
            try
            {
                // Получаем размеры экрана
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                // Устанавливаем размеры и позицию
                Width = screenWidth;
                Height = 60;
                Left = 0;
                Top = screenHeight - Height;
                
                _logger?.LogDebug("Taskbar positioned: {Width}x{Height} at ({Left},{Top})", Width, Height, Left, Top);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting taskbar position");
            }
        }

        /// <summary>
        /// Запуск таймера для обновления времени
        /// </summary>
        private void StartClockTimer()
        {
            try
            {
                _clockTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _clockTimer.Tick += ClockTimer_Tick;
                _clockTimer.Start();
                
                // Обновляем время сразу
                UpdateClock();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting clock timer");
            }
        }

        /// <summary>
        /// Обновление отображения времени
        /// </summary>
        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            try
            {
                var now = DateTime.Now;
                TimeTextBlock.Text = now.ToString("HH:mm");
                DateTextBlock.Text = now.ToString("dd.MM.yyyy");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating clock");
            }
        }

        /// <summary>
        /// Инициализация кнопки AppSwitcher
        /// </summary>
        private void InitializeAppSwitcherButton()
        {
            try
            {
                // Начальное состояние - отключена
                AppSwitcherButton.IsEnabled = false;
                AppCountTextBlock.Text = "Apps(0)";
                
                // TODO: Подписаться на события изменения количества приложений
                // Это будет реализовано при интеграции с ApplicationLifecycleService
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing AppSwitcher button");
            }
        }

        /// <summary>
        /// Определение режима Shell
        /// </summary>
        private async System.Threading.Tasks.Task DetectShellModeAsync()
        {
            try
            {
                if (_shellModeDetectionService != null)
                {
                    _currentShellMode = await _shellModeDetectionService.DetectShellModeAsync();
                    var modeDescription = _shellModeDetectionService.GetModeDescription(_currentShellMode);
                    
                    UpdateAppSwitcherButtonState(modeDescription, GetHotKeysDescription(_currentShellMode));
                    
                    _logger?.LogInformation("Shell mode detected: {Mode}", _currentShellMode);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error detecting shell mode");
            }
        }

        /// <summary>
        /// Получение описания горячих клавиш для текущего режима
        /// </summary>
        private string GetHotKeysDescription(ShellMode mode)
        {
            return mode switch
            {
                ShellMode.Shell => "Alt+Tab, Ctrl+Alt+Tab",
                ShellMode.Normal => "Win+`, Win+Shift+`",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Обновление состояния кнопки AppSwitcher
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
                _logger?.LogError(ex, "Error updating AppSwitcher button state");
            }
        }

        #region Event Handlers

        /// <summary>
        /// Обработчик клика по кнопке "Пуск" - показывает контекстное меню
        /// </summary>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogDebug("Start button clicked - showing context menu");
                
                // Обновляем видимость админ пункта перед показом меню
                UpdateAdminMenuItemVisibility();
                
                // Показываем контекстное меню
                if (StartButton.ContextMenu != null)
                {
                    StartButton.ContextMenu.PlacementTarget = StartButton;
                    StartButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                    StartButton.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing Start menu");
            }
        }

        /// <summary>
        /// Обновить видимость пункта админ меню в зависимости от роли пользователя
        /// </summary>
        private void UpdateAdminMenuItemVisibility()
        {
            try
            {
                // Получаем MainWindow для проверки роли пользователя
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    // Показываем админ пункт только для администраторов и выше
                    var hasAdminRights = mainViewModel.CurrentUser?.Role >= WindowsLauncher.Core.Enums.UserRole.Administrator;
                    if (AdminMenuItem != null)
                    {
                        AdminMenuItem.Visibility = hasAdminRights ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating admin menu item visibility");
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке переключения приложений
        /// </summary>
        private async void AppSwitcherButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_appSwitcherService != null)
                {
                    await _appSwitcherService.ShowAppSwitcherAsync(_currentShellMode);
                    _logger?.LogDebug("App switcher opened via taskbar button");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening app switcher from taskbar button");
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке виртуальной клавиатуры
        /// </summary>
        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_serviceProvider != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var keyboardService = scope.ServiceProvider.GetRequiredService<IVirtualKeyboardService>();

                    _logger?.LogDebug("Attempting to show virtual keyboard via taskbar button");

                    // Используем ту же логику что и в MainViewModel - сначала показываем, потом позиционируем
                    bool success = await keyboardService.ShowVirtualKeyboardAsync();
                    
                    if (!success)
                    {
                        // Если первая попытка не удалась, пробуем принудительное позиционирование
                        _logger?.LogInformation("First attempt failed, trying to reposition keyboard");
                        success = await keyboardService.RepositionKeyboardAsync();
                    }

                    if (success)
                    {
                        _logger?.LogDebug("Virtual keyboard shown successfully via taskbar button");
                    }
                    else
                    {
                        _logger?.LogWarning("Failed to show virtual keyboard via taskbar button");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling virtual keyboard from taskbar button");
            }
        }

        #region Start Menu Event Handlers

        /// <summary>
        /// Показать главное окно приложений
        /// </summary>
        private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow != null)
                {
                    if (app.MainWindow.WindowState == WindowState.Minimized)
                    {
                        app.MainWindow.WindowState = WindowState.Normal;
                    }
                    app.MainWindow.Show();
                    app.MainWindow.Activate();
                    app.MainWindow.Focus();
                    
                    _logger?.LogDebug("MainWindow shown and activated from Start menu");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing MainWindow from Start menu");
            }
        }

        /// <summary>
        /// Смена пользователя
        /// </summary>
        private void SwitchUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    if (mainViewModel.SwitchUserCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.SwitchUserCommand.Execute(null);
                        _logger?.LogDebug("Switch user command executed from Start menu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing switch user from Start menu");
            }
        }

        /// <summary>
        /// Открыть админ панель
        /// </summary>
        private void OpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    if (mainViewModel.OpenAdminCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.OpenAdminCommand.Execute(null);
                        _logger?.LogDebug("Open admin command executed from Start menu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening admin from Start menu");
            }
        }

        /// <summary>
        /// Открыть настройки
        /// </summary>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    if (mainViewModel.OpenSettingsCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.OpenSettingsCommand.Execute(null);
                        _logger?.LogDebug("Open settings command executed from Start menu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening settings from Start menu");
            }
        }

        /// <summary>
        /// Обновить приложения
        /// </summary>
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    if (mainViewModel.RefreshCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.RefreshCommand.Execute(null);
                        _logger?.LogDebug("Refresh command executed from Start menu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing refresh from Start menu");
            }
        }

        /// <summary>
        /// Выйти из приложения
        /// </summary>
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    if (mainViewModel.LogoutCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.LogoutCommand.Execute(null);
                        _logger?.LogDebug("Logout command executed from Start menu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing logout from Start menu");
            }
        }

        #endregion

        #endregion

        #region App Count Update

        /// <summary>
        /// Запустить таймер для обновления счетчика приложений
        /// </summary>
        private async System.Threading.Tasks.Task StartAppCountUpdateTimerAsync()
        {
            await System.Threading.Tasks.Task.Run(async () =>
            {
                while (!_disposed)
                {
                    try
                    {
                        if (_serviceProvider != null)
                        {
                            var lifecycleService = _serviceProvider.GetService<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService>();
                            
                            if (lifecycleService != null)
                            {
                                var appCount = await lifecycleService.GetCountAsync();
                                
                                // Обновляем UI в главном потоке
                                Dispatcher.BeginInvoke(() =>
                                {
                                    UpdateAppCountDisplay(appCount);
                                });
                            }
                        }

                        await System.Threading.Tasks.Task.Delay(2000); // Обновляем каждые 2 секунды
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Error updating app count display in SystemTaskbar");
                        await System.Threading.Tasks.Task.Delay(5000); // Увеличиваем интервал при ошибке
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
                if (AppCountTextBlock != null && AppSwitcherButton != null)
                {
                    if (appCount > 0)
                    {
                        AppCountTextBlock.Text = $"Apps({appCount})";
                        
                        // Включаем кнопку если есть приложения для переключения
                        AppSwitcherButton.IsEnabled = true;
                        
                        _logger?.LogDebug("SystemTaskbar AppSwitcher enabled with {AppCount} applications", appCount);
                    }
                    else
                    {
                        AppCountTextBlock.Text = "Apps(0)";
                        
                        // Отключаем кнопку если нет приложений
                        AppSwitcherButton.IsEnabled = false;
                        
                        _logger?.LogDebug("SystemTaskbar AppSwitcher disabled - no applications running");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating app count display in SystemTaskbar");
            }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Устанавливаем флаг для остановки таймера обновления приложений
                _disposed = true;
                
                // Останавливаем таймер
                if (_clockTimer != null)
                {
                    _clockTimer.Stop();
                    _clockTimer = null;
                }
                
                _logger?.LogInformation("SystemTaskbar closed and cleaned up");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during SystemTaskbar cleanup");
            }
            
            base.OnClosed(e);
        }

        #endregion
    }
}
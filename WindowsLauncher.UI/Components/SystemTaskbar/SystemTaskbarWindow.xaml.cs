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
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Enums;
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
        private IAndroidSubsystemService? _androidSubsystemService;
        
        private DispatcherTimer? _clockTimer;
        private DispatcherTimer? _wsaInitTimer;
        private ShellMode _currentShellMode = ShellMode.Normal;
        private bool _disposed = false;
        private int _wsaInitRetryCount = 0;
        private const int MAX_WSA_INIT_RETRIES = 5;
        
        // WSA Status properties для прямого управления
        private bool _showWSAStatus = false;
        private string _wsaStatusText = "Unknown";
        private string _wsaStatusColor = "#666666";
        private string _wsaStatusTooltip = "Android подсистема";

        public bool ShowWSAStatus
        {
            get => _showWSAStatus;
            private set
            {
                if (_showWSAStatus != value)
                {
                    _showWSAStatus = value;
                    _logger?.LogDebug("WSA indicator visibility: {State}", value ? "Visible" : "Hidden");
                    
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (WSAStatusIndicator != null)
                        {
                            WSAStatusIndicator.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else
                        {
                            _logger?.LogWarning("WSAStatusIndicator UI element is null - cannot update visibility");
                        }
                    });
                }
            }
        }

        public string WSAStatusText
        {
            get => _wsaStatusText;
            private set
            {
                _wsaStatusText = value;
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateWSAStatusDisplay();
                });
            }
        }

        public string WSAStatusColor
        {
            get => _wsaStatusColor;
            private set
            {
                _wsaStatusColor = value;
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateWSAStatusDisplay();
                });
            }
        }

        public string WSAStatusTooltip
        {
            get => _wsaStatusTooltip;
            private set
            {
                _wsaStatusTooltip = value;
                Dispatcher.BeginInvoke(() =>
                {
                    if (WSAStatusIndicator != null)
                    {
                        WSAStatusIndicator.ToolTip = value;
                    }
                });
            }
        }

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
                _androidSubsystemService = app.ServiceProvider.GetService<IAndroidSubsystemService>();
                
                // Диагностика сервисов
                _logger?.LogDebug("SystemTaskbarWindow constructor: ServiceProvider available");
                _logger?.LogDebug("Services resolved: AppSwitcher={AppSwitcher}, ShellMode={ShellMode}, Android={Android}", 
                    _appSwitcherService != null, 
                    _shellModeDetectionService != null, 
                    _androidSubsystemService != null);
                
                // AndroidSubsystemService диагностика
                if (_androidSubsystemService != null)
                {
                    _logger?.LogDebug("AndroidSubsystemService found in constructor: Mode={Mode}, Status={Status}", 
                        _androidSubsystemService.CurrentMode, 
                        _androidSubsystemService.WSAStatus);
                }
                else
                {
                    _logger?.LogWarning("AndroidSubsystemService is null in constructor - will attempt delayed initialization");
                }
                
                // Устанавливаем DataContext на self для прямого binding
                DataContext = this;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SystemTaskbarWindow: ServiceProvider is null!");
            }

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            try
            {
                _logger?.LogDebug("SystemTaskbarWindow.InitializeWindow started");
                
                // Устанавливаем размеры и позицию панели задач
                SetTaskbarPosition();
                
                // Запускаем таймер для обновления времени
                StartClockTimer();
                
                // Инициализируем состояние кнопки AppSwitcher
                InitializeAppSwitcherButton();
                
                // Запускаем отложенную инициализацию WSA статуса
                StartDelayedWSAInitialization();
                
                // Определяем режим Shell
                _ = DetectShellModeAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        _logger?.LogError(task.Exception, "Error detecting shell mode");
                    }
                }, TaskScheduler.Default);
                
                _logger?.LogDebug("SystemTaskbar initialization completed");
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
                _ = StartAppCountUpdateTimerAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        _logger?.LogError(task.Exception, "Error starting app count update timer");
                    }
                }, TaskScheduler.Default);
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
                    if (mainViewModel.ExitApplicationCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.ExitApplicationCommand.Execute(null);
                        _logger?.LogDebug("Exit application command executed from Start menu");
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
                    if (mainViewModel.ExitApplicationCommand?.CanExecute(null) == true)
                    {
                        mainViewModel.ExitApplicationCommand.Execute(null);
                        _logger?.LogDebug("Exit application command executed from Start menu");
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

        #region WSA Delayed Initialization

        /// <summary>
        /// Запустить отложенную инициализацию WSA статуса
        /// </summary>
        private void StartDelayedWSAInitialization()
        {
            try
            {
                _logger?.LogDebug("Starting delayed WSA initialization - attempt in 2 seconds");
                
                // Создаем таймер для отложенной инициализации
                _wsaInitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2) // Задержка 2 секунды
                };
                _wsaInitTimer.Tick += WSAInitTimer_Tick;
                _wsaInitTimer.Start();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting delayed WSA initialization");
            }
        }

        /// <summary>
        /// Обработчик таймера отложенной инициализации WSA
        /// </summary>
        private async void WSAInitTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                _wsaInitRetryCount++;
                _logger?.LogDebug("WSA initialization attempt #{Attempt} of {MaxAttempts}", _wsaInitRetryCount, MAX_WSA_INIT_RETRIES);
                
                // Останавливаем таймер
                _wsaInitTimer?.Stop();
                
                // Пробуем получить AndroidSubsystemService заново
                var androidService = _serviceProvider?.GetService<IAndroidSubsystemService>();
                
                if (androidService != null)
                {
                    _logger?.LogDebug("AndroidSubsystemService found on attempt #{Attempt}: Mode={Mode}, Status={Status}", 
                        _wsaInitRetryCount, androidService.CurrentMode, androidService.WSAStatus);
                    
                    // Заменяем null сервис
                    _androidSubsystemService = androidService;
                    
                    // Инициализируем WSA статус
                    await InitializeWSAStatusAsync();
                    
                    // Останавливаем таймер - успех
                    _wsaInitTimer = null;
                }
                else if (_wsaInitRetryCount < MAX_WSA_INIT_RETRIES)
                {
                    _logger?.LogDebug("AndroidSubsystemService still null on attempt #{Attempt}/{MaxAttempts}, retrying in 3 seconds", 
                        _wsaInitRetryCount, MAX_WSA_INIT_RETRIES);
                    
                    // Перезапускаем таймер с увеличенной задержкой
                    _wsaInitTimer.Interval = TimeSpan.FromSeconds(3);
                    _wsaInitTimer.Start();
                }
                else
                {
                    _logger?.LogWarning("AndroidSubsystemService not available after {MaxRetries} attempts, hiding WSA indicator", MAX_WSA_INIT_RETRIES);
                    _wsaInitTimer = null;
                    ShowWSAStatus = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during WSA initialization retry #{Attempt}", _wsaInitRetryCount);
                
                // Пробуем еще раз если не превысили лимит
                if (_wsaInitRetryCount < MAX_WSA_INIT_RETRIES && _wsaInitTimer != null)
                {
                    _wsaInitTimer.Interval = TimeSpan.FromSeconds(5);
                    _wsaInitTimer.Start();
                }
            }
        }

        #endregion

        #region WSA Status Management

        /// <summary>
        /// Инициализировать статус WSA
        /// </summary>
        private async Task InitializeWSAStatusAsync()
        {
            try
            {
                _logger?.LogDebug("InitializeWSAStatusAsync started");
                
                if (_androidSubsystemService == null)
                {
                    _logger?.LogWarning("AndroidSubsystemService is null, WSA status disabled");
                    ShowWSAStatus = false;
                    return;
                }

                var mode = _androidSubsystemService.CurrentMode;
                var currentStatus = _androidSubsystemService.WSAStatus;
                
                _logger?.LogDebug("AndroidSubsystemService configuration: Mode={Mode}, Status={Status}", mode, currentStatus);
                
                ShowWSAStatus = mode != AndroidMode.Disabled;

                if (!ShowWSAStatus)
                {
                    _logger?.LogDebug("WSA disabled in configuration (mode: {Mode})", mode);
                    return;
                }

                // Подписываемся на изменения статуса
                _androidSubsystemService.StatusChanged += OnWSAStatusChanged;
                _logger?.LogDebug("Subscribed to AndroidSubsystemService.StatusChanged event");

                // Обновляем текущий статус
                await UpdateWSAStatusAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing WSA status in SystemTaskbar");
                ShowWSAStatus = false;
            }
        }

        /// <summary>
        /// Обработчик изменения статуса WSA
        /// </summary>
        private async void OnWSAStatusChanged(object? sender, string status)
        {
            try
            {
                _logger?.LogDebug("WSA status changed event received: {Status}", status);
                await UpdateWSAStatusAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling WSA status change in SystemTaskbar");
            }
        }

        /// <summary>
        /// Обновить отображение статуса WSA
        /// </summary>
        private async Task UpdateWSAStatusAsync()
        {
            try
            {
                if (_androidSubsystemService == null || !ShowWSAStatus)
                    return;

                var status = _androidSubsystemService.WSAStatus;
                var mode = _androidSubsystemService.CurrentMode;

                // Устанавливаем текст и цвет в зависимости от статуса
                WSAStatusText = GetLocalizedStatusText(status);
                WSAStatusColor = GetStatusColor(status);
                WSAStatusTooltip = GetStatusTooltip(status, mode);

                _logger?.LogDebug("WSA status updated: {Status} -> '{Text}'", status, WSAStatusText);
                
                await Task.CompletedTask; // Make method properly async
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating WSA status display");
            }
        }

        /// <summary>
        /// Получить локализованный текст статуса
        /// </summary>
        private string GetLocalizedStatusText(string status)
        {
            return status switch
            {
                "Ready" => "Готов",
                "Starting" => "Запуск",
                "Stopping" => "Остановка",
                "Error" => "Ошибка",
                "Unavailable" => "Недоступен",
                "Disabled" => "Отключен",
                _ => status
            };
        }

        /// <summary>
        /// Получить цвет для статуса
        /// </summary>
        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Ready" => "#4CAF50",           // Зеленый
                "Starting" => "#FF9800",       // Оранжевый
                "Stopping" => "#FF9800",       // Оранжевый  
                "Error" => "#F44336",          // Красный
                "Unavailable" => "#9E9E9E",    // Серый
                "Disabled" => "#9E9E9E",       // Серый
                _ => "#666666"                 // Темно-серый по умолчанию
            };
        }

        /// <summary>
        /// Получить tooltip для статуса
        /// </summary>
        private string GetStatusTooltip(string status, AndroidMode mode)
        {
            var modeText = mode switch
            {
                AndroidMode.Disabled => "Отключен",
                AndroidMode.OnDemand => "По требованию", 
                AndroidMode.Preload => "Предзагрузка",
                _ => mode.ToString()
            };

            return $"Android подсистема: {GetLocalizedStatusText(status)}\nРежим: {modeText}";
        }

        /// <summary>
        /// Обновить отображение WSA статуса в UI
        /// </summary>
        private void UpdateWSAStatusDisplay()
        {
            try
            {
                // Ищем TextBlock по имени
                var textBlock = FindName("WSAStatusTextBlock") as System.Windows.Controls.TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = WSAStatusText;
                    textBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(WSAStatusColor));
                }
                else
                {
                    _logger?.LogWarning("WSAStatusTextBlock not found in UI - cannot update text display");
                }

                // Обновляем background Border в зависимости от статуса
                UpdateWSABackgroundColor();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating WSA status display");
            }
        }


        /// <summary>
        /// Обновить цвет фона WSA индикатора
        /// </summary>
        private void UpdateWSABackgroundColor()
        {
            if (WSAStatusIndicator == null)
                return;
            
            var backgroundColor = WSAStatusText switch
            {
                "Готов" => "#E8F5E8",      // Светло-зеленый
                "Запуск" => "#FFF4E6",     // Светло-оранжевый
                "Ошибка" => "#FFEBEE",     // Светло-красный
                _ => "Transparent"         // Прозрачный по умолчанию
            };

            try
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundColor));
                WSAStatusIndicator.Background = brush;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error setting WSA background color");
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
                
                // Останавливаем таймер времени
                if (_clockTimer != null)
                {
                    _clockTimer.Stop();
                    _clockTimer = null;
                }
                
                // Останавливаем таймер WSA инициализации
                if (_wsaInitTimer != null)
                {
                    _wsaInitTimer.Stop();
                    _wsaInitTimer = null;
                }
                
                // Отписываемся от событий WSA
                if (_androidSubsystemService != null)
                {
                    _androidSubsystemService.StatusChanged -= OnWSAStatusChanged;
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
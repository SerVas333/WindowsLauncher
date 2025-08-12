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
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Data;
using WindowsLauncher.Core.Services;

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
            _ = InitializeAppSwitcherAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _logger?.LogError(task.Exception, "Error initializing app switcher");
                }
            }, TaskScheduler.Default);
            
            // Подписываемся на событие загрузки для настройки адаптивных плиток
            Loaded += MainWindow_Loaded;
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
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sessionManager.LoadConfigurationAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error loading session configuration");
                        }
                    });
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
                
                // Отписываемся от события изменения коллекции приложений
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.FilteredApplications.CollectionChanged -= OnFilteredApplicationsChanged;
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
                
                // Адаптируем интерфейс под Shell режим
                AdaptUIForShellMode(_currentShellMode);
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
                            var lifecycleService = serviceProvider.GetService<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService>();
                            
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
        
        /// <summary>
        /// Публичный метод для принудительного пересчета высот плиток (для тестирования)
        /// </summary>
        public void RecalculateTileHeights()
        {
            try
            {
                _logger?.LogInformation("Manual tile heights recalculation requested");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CalculateAndApplyAdaptiveTileHeights();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in manual tile heights recalculation");
            }
        }
        
        /// <summary>
        /// Обработчик загрузки окна для настройки адаптивных плиток
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Подписываемся на изменения коллекции приложений
            SubscribeToApplicationsCollectionChanges();
            
            // Ждем небольшую задержку для завершения рендеринга
            await Task.Delay(100);
            CalculateAndApplyAdaptiveTileHeights();
        }
        
        /// <summary>
        /// Подписаться на изменения коллекции приложений
        /// </summary>
        private void SubscribeToApplicationsCollectionChanges()
        {
            try
            {
                if (DataContext is MainViewModel viewModel)
                {
                    // Подписываемся на изменения фильтрации приложений
                    viewModel.FilteredApplications.CollectionChanged += OnFilteredApplicationsChanged;
                    _logger?.LogDebug("Subscribed to FilteredApplications collection changes");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error subscribing to applications collection changes");
            }
        }
        
        /// <summary>
        /// Обработчик изменения коллекции приложений
        /// </summary>
        private async void OnFilteredApplicationsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            try
            {
                _logger?.LogTrace("FilteredApplications collection changed: {Action}, Count: {Count}", 
                    e.Action, (sender as System.Collections.ICollection)?.Count ?? 0);
                
                // Небольшая задержка для обновления UI и завершения рендеринга
                await Task.Delay(150); // Увеличиваем задержку для стабильности
                CalculateAndApplyAdaptiveTileHeights();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error handling applications collection change");
            }
        }
        
        /// <summary>
        /// Рассчитать и применить адаптивную высоту плиток
        /// </summary>
        private void CalculateAndApplyAdaptiveTileHeights()
        {
            try
            {
                _logger?.LogDebug("Calculating adaptive tile heights for MainWindow");
                
                // Находим ItemsControl с приложениями
                var itemsControl = FindApplicationsItemsControl();
                if (itemsControl == null)
                {
                    _logger?.LogWarning("Could not find applications ItemsControl for adaptive heights");
                    return;
                }
                
                _logger?.LogTrace("Found ItemsControl with {ItemCount} items", itemsControl.Items.Count);
                
                if (itemsControl.Items.Count == 0)
                {
                    _logger?.LogTrace("No items in ItemsControl, skipping height calculation");
                    return;
                }
                
                var tileBorders = new List<Border>();
                double maxHeight = 0;
                
                // Собираем все Border элементы плиток
                for (int i = 0; i < itemsControl.Items.Count; i++)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container != null)
                    {
                        var border = FindChildByName<Border>(container, "AppTileBorder");
                        if (border != null)
                        {
                            tileBorders.Add(border);
                            
                            // Рассчитываем высоту содержимого
                            var contentHeight = CalculateTileContentHeight(border);
                            maxHeight = Math.Max(maxHeight, contentHeight);
                        }
                    }
                }
                
                // Добавляем минимальную высоту и запас для стабильности
                const double minHeight = 160; // Минимальная высота плитки
                const double safetyMargin = 10; // Дополнительный запас для стабильности
                maxHeight = Math.Max(maxHeight + safetyMargin, minHeight);
                
                // Применяем одинаковую высоту ко всем плиткам
                foreach (var border in tileBorders)
                {
                    border.Height = maxHeight;
                }
                
                _logger?.LogDebug("Applied adaptive height {Height}px to {Count} tiles (was max content: {MaxContent}px)", 
                    maxHeight, tileBorders.Count, maxHeight - 10);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calculating adaptive tile heights");
            }
        }
        
        /// <summary>
        /// Рассчитать высоту содержимого плитки
        /// </summary>
        private double CalculateTileContentHeight(Border tileBorder)
        {
            try
            {
                var contentGrid = FindChildByName<Grid>(tileBorder, "AppTileContent");
                if (contentGrid == null) return 160; // Увеличиваем дефолтную высоту
                
                double totalHeight = 0;
                
                // Размеры компонентов плитки (точные значения из XAML)
                const double iconHeight = 50 + 12; // Icon (50px) + margin bottom (12px)
                const double categoryBadgeHeight = 18 + 8; // Badge height (~18px) + margin top (8px)
                const double tilePadding = 16; // Внутренние отступы Border (8px с каждой стороны)
                
                totalHeight += iconHeight;
                totalHeight += categoryBadgeHeight;
                totalHeight += tilePadding;
                
                // Рассчитываем высоту инфо-панели с точным измерением текста
                var infoPanel = FindChildByName<StackPanel>(contentGrid, "AppInfoPanel");
                if (infoPanel != null)
                {
                    double infoPanelHeight = 12; // Margin снизу у AppInfoPanel
                    
                    // Доступная ширина для текста (ширина плитки минус padding)
                    const double availableWidth = 220 - 32; // 220px ширина плитки - 16px padding с каждой стороны
                    
                    foreach (UIElement child in infoPanel.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            // Принудительно обновляем layout перед измерением
                            textBlock.UpdateLayout();
                            
                            // Создаем точную копию TextBlock для измерения
                            var measureTextBlock = new TextBlock
                            {
                                Text = textBlock.Text,
                                FontSize = textBlock.FontSize,
                                FontWeight = textBlock.FontWeight,
                                FontFamily = textBlock.FontFamily,
                                TextWrapping = textBlock.TextWrapping,
                                TextAlignment = textBlock.TextAlignment,
                                LineHeight = textBlock.LineHeight > 0 ? textBlock.LineHeight : double.NaN
                            };
                            
                            // Измеряем с доступной шириной
                            measureTextBlock.Measure(new Size(availableWidth, double.PositiveInfinity));
                            double textHeight = measureTextBlock.DesiredSize.Height;
                            
                            // Минимальная высота для одной строки
                            if (textHeight < textBlock.FontSize * 1.2)
                            {
                                textHeight = textBlock.FontSize * 1.2;
                            }
                            
                            infoPanelHeight += textHeight;
                            
                            // Добавляем margin если есть
                            infoPanelHeight += textBlock.Margin.Top + textBlock.Margin.Bottom;
                            
                            _logger?.LogTrace("Text measurement: '{Text}' = {Height}px", 
                                textBlock.Text?.Substring(0, Math.Min(textBlock.Text.Length, 20)), textHeight);
                        }
                    }
                    
                    totalHeight += infoPanelHeight;
                }
                else
                {
                    // Дефолтная высота инфо-панели если не нашли
                    totalHeight += 60; // Увеличиваем дефолтную высоту
                }
                
                // Минимальная высота плитки
                totalHeight = Math.Max(totalHeight, 160);
                
                _logger?.LogTrace("Calculated tile height: {Height}px", totalHeight);
                return totalHeight;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error calculating tile content height, using default");
                return 160;
            }
        }
        
        /// <summary>
        /// Найти дочерний элемент по имени
        /// </summary>
        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                // Проверяем текущий элемент
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                
                // Рекурсивный поиск в дочерних элементах
                var found = FindChildByName<T>(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Найти ItemsControl с приложениями
        /// </summary>
        private ItemsControl? FindApplicationsItemsControl()
        {
            try
            {
                // Ищем ItemsControl по привязке данных FilteredApplications
                return FindItemsControlByBinding(this, "FilteredApplications");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error finding applications ItemsControl");
                return null;
            }
        }
        
        /// <summary>
        /// Найти ItemsControl по привязке данных
        /// </summary>
        private ItemsControl? FindItemsControlByBinding(DependencyObject parent, string bindingPath)
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is ItemsControl itemsControl)
                {
                    // Проверяем привязку ItemsSource
                    var binding = itemsControl.GetBindingExpression(ItemsControl.ItemsSourceProperty);
                    if (binding?.ParentBinding?.Path?.Path == bindingPath)
                    {
                        return itemsControl;
                    }
                }
                
                // Рекурсивный поиск
                var found = FindItemsControlByBinding(child, bindingPath);
                if (found != null)
                {
                    return found;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Адаптация интерфейса MainWindow под Shell режим
        /// </summary>
        private void AdaptUIForShellMode(ShellMode shellMode)
        {
            try
            {
                if (shellMode == ShellMode.Shell)
                {
                    _logger?.LogInformation("Adapting MainWindow for Shell mode");
                    
                    // 1. Полноэкранный режим при старте
                    WindowState = WindowState.Maximized;
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;
                    
                    // 2. Скрываем статус бар (он есть на SystemTaskbar)
                    // WSA статус индикатор теперь показывается в SystemTaskbarWindow
                    if (FindName("StatusBar") is Border statusBar)
                    {
                        statusBar.Visibility = Visibility.Collapsed;
                    }
                    
                    // Альтернативно, если статус бар находится в Grid.Row="3"
                    var grid = Content as Grid;
                    if (grid != null && grid.Children.Count > 3)
                    {
                        // Скрываем последний элемент (статус бар в Row="3")
                        foreach (UIElement child in grid.Children)
                        {
                            if (Grid.GetRow(child) == 3)
                            {
                                child.Visibility = Visibility.Collapsed;
                                break;
                            }
                        }
                    }
                    
                    _logger?.LogDebug("MainWindow adapted for Shell mode: fullscreen, no chrome, no status bar");
                }
                else
                {
                    _logger?.LogInformation("MainWindow running in Normal mode - no adaptations needed");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adapting UI for Shell mode");
            }
        }
    }
}
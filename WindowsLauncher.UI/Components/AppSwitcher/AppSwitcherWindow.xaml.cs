using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using LocalResources = WindowsLauncher.UI.Properties.Resources.Resources;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Components.AppSwitcher
{
    /// <summary>
    /// Окно переключателя приложений (Alt+Tab аналог)
    /// </summary>
    public partial class AppSwitcherWindow : Window
    {
        private readonly IApplicationLifecycleService _lifecycleService;
        private readonly ILogger<AppSwitcherWindow> _logger;
        private readonly ObservableCollection<AppSwitcherItem> _runningApplications;
        private int _selectedIndex = 0;
        private ShellMode _shellMode = ShellMode.Normal;

        public ObservableCollection<AppSwitcherItem> RunningApplications => _runningApplications;

        public AppSwitcherWindow(IApplicationLifecycleService lifecycleService, ILogger<AppSwitcherWindow> logger)
        {
            InitializeComponent();
            
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runningApplications = new ObservableCollection<AppSwitcherItem>();
            
            DataContext = this;
            
            // Подписываемся на события клавиатуры и потери фокуса
            PreviewKeyDown += AppSwitcherWindow_PreviewKeyDown;
            Loaded += AppSwitcherWindow_Loaded;
            Deactivated += AppSwitcherWindow_Deactivated;
        }

        /// <summary>
        /// Показать переключатель приложений
        /// </summary>
        public async Task ShowSwitcherAsync(ShellMode shellMode = ShellMode.Normal)
        {
            try
            {
                _shellMode = shellMode;
                _logger.LogDebug("Showing application switcher in {Mode} mode", _shellMode);
                
                // Обновляем информацию о режиме в UI
                UpdateModeInfo();
                
                // Загружаем список запущенных приложений
                await LoadRunningApplicationsAsync();
                
                if (_runningApplications.Count == 0)
                {
                    _logger.LogInformation("No running applications to switch between");
                    return;
                }

                // Выбираем первое приложение
                _selectedIndex = 0;
                UpdateSelection();
                
                // Рассчитываем динамические размеры окна
                CalculateDynamicSize();
                
                // Показываем окно с анимацией
                Show();
                Activate();
                Focus();
                
                // Запускаем анимацию появления
                var fadeInStoryboard = (Storyboard)FindResource("FadeInAnimation");
                fadeInStoryboard?.Begin(this);
                
                _logger.LogInformation("Application switcher shown with {Count} applications in {Mode} mode", 
                    _runningApplications.Count, _shellMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing application switcher");
                await HideWithAnimationAsync();
            }
        }

        /// <summary>
        /// Обновить информацию о режиме в UI
        /// </summary>
        private void UpdateModeInfo()
        {
            try
            {
                var modeText = _shellMode switch
                {
                    ShellMode.Shell => LocalResources.AppSwitcher_ShellMode,
                    ShellMode.Normal => LocalResources.AppSwitcher_NormalMode,
                    _ => LocalResources.AppSwitcher_UnknownMode
                    
                };

                if (ModeInfoTextBlock != null)
                {
                    ModeInfoTextBlock.Text = modeText;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating mode info in UI");
            }
        }

        /// <summary>
        /// Скрыть переключатель и переключиться на выбранное приложение
        /// </summary>
        public async Task<bool> SwitchToSelectedApplicationAsync()
        {
            try
            {
                if (_selectedIndex >= 0 && _selectedIndex < _runningApplications.Count)
                {
                    var selectedApp = _runningApplications[_selectedIndex];
                    _logger.LogInformation("Switching to application: {Name} (PID: {ProcessId}, IsMinimized: {IsMinimized})", 
                        selectedApp.Name, selectedApp.ProcessId, selectedApp.IsMinimized);
                    
                    // Переключаемся на приложение по InstanceId
                    var success = await _lifecycleService.SwitchToAsync(selectedApp.InstanceId);
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully switched to {Name} - window should be restored if was minimized", selectedApp.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to switch to {Name} (IsMinimized: {IsMinimized})", selectedApp.Name, selectedApp.IsMinimized);
                    }
                    
                    await HideWithAnimationAsync();
                    return success;
                }
                
                await HideWithAnimationAsync();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to selected application");
                await HideWithAnimationAsync();
                return false;
            }
        }

        /// <summary>
        /// Перейти к следующему приложению
        /// </summary>
        public void SelectNext()
        {
            if (_runningApplications.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _runningApplications.Count;
            UpdateSelection();
            _logger.LogDebug("Selected next application: index {Index}", _selectedIndex);
        }

        /// <summary>
        /// Перейти к предыдущему приложению
        /// </summary>
        public void SelectPrevious()
        {
            if (_runningApplications.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _runningApplications.Count) % _runningApplications.Count;
            UpdateSelection();
            _logger.LogDebug("Selected previous application: index {Index}", _selectedIndex);
        }

        /// <summary>
        /// Загрузить список запущенных приложений
        /// </summary>
        private async Task LoadRunningApplicationsAsync()
        {
            try
            {
                var runningInstances = await _lifecycleService.GetRunningAsync();
                
                // Обновляем UI элементы в UI потоке
                await Dispatcher.InvokeAsync(() =>
                {
                    _runningApplications.Clear();
                    
                    foreach (var instance in runningInstances.OrderBy(i => i.Application.Name))
                    {
                        _runningApplications.Add(new AppSwitcherItem
                        {
                            ProcessId = instance.ProcessId,
                            ApplicationId = instance.Application.Id,
                            InstanceId = instance.InstanceId,
                            Name = instance.Application.Name,
                            IconText = GetAppIcon(instance.Application.Name),
                            IsResponding = instance.IsResponding,
                            IsMinimized = instance.IsMinimized,
                            MemoryUsageMB = instance.MemoryUsageMB,
                            IsSelected = false
                        });
                    }
                });
                
                _logger.LogDebug("Loaded {Count} running applications for switcher", _runningApplications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading running applications for switcher");
            }
        }

        /// <summary>
        /// Обновить список запущенных приложений (для интеграции с ApplicationLifecycleService)
        /// </summary>
        public async Task RefreshApplicationsAsync()
        {
            try
            {
                // Сохраняем текущий выбор
                var currentSelectedProcessId = _selectedIndex >= 0 && _selectedIndex < _runningApplications.Count
                    ? _runningApplications[_selectedIndex].ProcessId
                    : -1;

                // Загружаем обновленный список приложений
                await LoadRunningApplicationsAsync();

                // Пытаемся восстановить выбор
                if (currentSelectedProcessId > 0)
                {
                    var newIndex = _runningApplications
                        .Select((app, index) => new { app, index })
                        .FirstOrDefault(x => x.app.ProcessId == currentSelectedProcessId)?.index ?? 0;
                    
                    _selectedIndex = newIndex;
                }
                else
                {
                    _selectedIndex = Math.Min(_selectedIndex, _runningApplications.Count - 1);
                }

                // Обновляем UI выбор
                UpdateSelection();
                
                _logger.LogTrace("Refreshed application switcher with {Count} applications", _runningApplications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing applications in switcher");
            }
        }

        /// <summary>
        /// Обновить выделение в списке
        /// </summary>
        private void UpdateSelection()
        {
            for (int i = 0; i < _runningApplications.Count; i++)
            {
                _runningApplications[i].IsSelected = (i == _selectedIndex);
            }
        }

        /// <summary>
        /// Рассчитать динамические размеры окна на основе количества приложений
        /// </summary>
        private void CalculateDynamicSize()
        {
            try
            {
                var appCount = _runningApplications.Count;
                if (appCount == 0) return;

                // Получаем размеры экрана (или главного окна если возможно)
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                // Пытаемся найти главное окно для определения его размеров
                Window? mainWindow = null;
                foreach (Window window in WpfApplication.Current.Windows)
                {
                    if (window.GetType().Name == "MainWindow" && window != this)
                    {
                        mainWindow = window;
                        break;
                    }
                }

                // Если нашли главное окно, используем его размеры, иначе - экрана
                var parentWidth = mainWindow?.Width ?? screenWidth;
                var parentHeight = mainWindow?.Height ?? screenHeight;

                // ✅ ИСПРАВЛЕННЫЕ КОНСТАНТЫ: Точное соответствие XAML
                const double cardWidth = 150; // Ширина карточки (Width="150" в AppSwitcherCard)
                const double cardHeight = 110; // Высота карточки (Height="110" в AppSwitcherCard)
                const double cardMargin = 6; // Отступы карточки (Margin="6" в AppSwitcherCard)
                
                // ✅ ТОЧНЫЕ ОТСТУПЫ: Border Margin="2" + Grid Margin="20" = 22px с каждой стороны
                const double containerPadding = 44; // 22px * 2 (слева+справа или сверху+снизу)
                
                // ✅ РЕАЛЬНЫЕ РАЗМЕРЫ ЗАГОЛОВКА И ПОДВАЛА:
                // Заголовок: иконка (24px) + текст (~20px) + margin (20px) = ~64px
                // Подсказки: 2 строки текста (~50px) + margin (20px) = ~70px
                const double headerFooterHeight = 140; // Увеличено для корректного расчета
                
                // ✅ МИНИМАЛЬНАЯ ШИРИНА: Для полного отображения подсказок управления
                const double minimumWidth = 520; // Минимум для "↑↓←→ навигация Enter переключить Esc отмена"
                
                // Максимальная ширина окна (80% от родительского окна)
                var maxWidth = parentWidth * 0.8;
                
                // Количество колонок, которое поместится по ширине с учетом минимума
                var availableWidth = Math.Max(minimumWidth, maxWidth) - containerPadding;
                var maxColumnsWidth = (int)Math.Floor(availableWidth / (cardWidth + cardMargin * 2));
                maxColumnsWidth = Math.Max(1, Math.Min(maxColumnsWidth, 6)); // От 1 до 6 колонок
                
                // Определяем количество колонок на основе количества приложений
                var columns = Math.Min(appCount, maxColumnsWidth);
                
                // Количество строк
                var rows = (int)Math.Ceiling((double)appCount / columns);
                
                // ✅ КОРРЕКТНЫЙ РАСЧЕТ: Ширина с учетом минимума
                var contentWidth = columns * cardWidth + (columns * cardMargin * 2);
                var calculatedWidth = Math.Max(minimumWidth, contentWidth + containerPadding);
                calculatedWidth = Math.Min(calculatedWidth, maxWidth);
                
                // ✅ КОРРЕКТНЫЙ РАСЧЕТ: Высота с точными отступами
                var contentHeight = rows * cardHeight + (rows * cardMargin * 2);
                var calculatedHeight = contentHeight + headerFooterHeight + containerPadding;
                
                // Ограничиваем высоту экрана (максимум 80% высоты экрана)
                var maxHeight = parentHeight * 0.8;
                calculatedHeight = Math.Min(calculatedHeight, maxHeight);
                
                // Применяем размеры
                Width = calculatedWidth;
                Height = calculatedHeight;
                
                // Обновляем количество колонок в UniformGrid
                if (AppsItemsControl?.ItemsPanel != null)
                {
                    var itemsPanel = AppsItemsControl.ItemsPanel;
                    // Создаем новый ItemsPanelTemplate с обновленным количеством колонок
                    var factory = new FrameworkElementFactory(typeof(UniformGrid));
                    factory.SetValue(UniformGrid.ColumnsProperty, columns);
                    factory.SetValue(UniformGrid.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    
                    var newTemplate = new ItemsPanelTemplate(factory);
                    AppsItemsControl.ItemsPanel = newTemplate;
                }
                
                _logger.LogDebug("AppSwitcher size calculated: {Width:F0}x{Height:F0}, {Columns} columns, {Rows} rows for {AppCount} apps (min width: {MinWidth})", 
                    Width, Height, columns, rows, appCount, minimumWidth);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating dynamic size, using safe defaults");
                Width = 600; // Безопасная ширина по умолчанию
                Height = 400;
            }
        }

        /// <summary>
        /// Получить иконку приложения по имени
        /// </summary>
        private string GetAppIcon(string appName)
        {
            var name = appName.ToLowerInvariant();
            
            return name switch
            {
                var n when n.Contains("notepad") => "📝",
                var n when n.Contains("calculator") => "🧮",
                var n when n.Contains("paint") => "🎨",
                var n when n.Contains("chrome") => "🌐",
                var n when n.Contains("firefox") => "🦊",
                var n when n.Contains("edge") => "🌐",
                var n when n.Contains("word") => "📄",
                var n when n.Contains("excel") => "📊",
                var n when n.Contains("powerpoint") => "📽️",
                var n when n.Contains("cmd") || n.Contains("command") => "⌨️",
                var n when n.Contains("powershell") => "💻",
                var n when n.Contains("explorer") => "📁",
                var n when n.Contains("visual studio") => "👨‍💻",
                _ => "📱"
            };
        }

        #region Event Handlers

        private async void AppSwitcherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Устанавливаем фокус на окно
            Focusable = true;
            Focus();
        }

        private async void AppSwitcherWindow_Deactivated(object sender, EventArgs e)
        {
            // Закрываем окно при потере фокуса
            _logger.LogDebug("AppSwitcher lost focus, hiding window");
            await HideWithAnimationAsync();
        }

        private async void AppSwitcherWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Tab:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        SelectPrevious();
                    }
                    else
                    {
                        SelectNext();
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    SelectPrevious();
                    e.Handled = true;
                    break;

                case Key.Down:
                    SelectNext();
                    e.Handled = true;
                    break;

                case Key.Left:
                    SelectPrevious();
                    e.Handled = true;
                    break;

                case Key.Right:
                    SelectNext();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    await SwitchToSelectedApplicationAsync();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    _ = HideWithAnimationAsync();
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    // Alt нажат - может быть использовано для будущей логики
                    break;
            }
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Уже обрабатывается в PreviewKeyDown
        }

        private async void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                // Если Alt отпущен, переключаемся на выбранное приложение
                await SwitchToSelectedApplicationAsync();
            }
        }

        private async void AppCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AppSwitcherItem app)
            {
                // Находим индекс выбранного приложения
                _selectedIndex = _runningApplications.IndexOf(app);
                UpdateSelection();
                
                // Переключаемся на приложение
                await SwitchToSelectedApplicationAsync();
            }
        }

        #endregion

        /// <summary>
        /// Скрыть окно с анимацией
        /// </summary>
        private async Task HideWithAnimationAsync()
        {
            try
            {
                var fadeOutStoryboard = (Storyboard)FindResource("FadeOutAnimation");
                if (fadeOutStoryboard != null)
                {
                    // Создаем TaskCompletionSource для ожидания завершения анимации
                    var tcs = new TaskCompletionSource<bool>();
                    
                    EventHandler completedHandler = null;
                    completedHandler = (s, e) =>
                    {
                        Hide();
                        // Проверяем, что Task еще не завершен
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.SetResult(true);
                        }
                        // Отписываемся от события
                        fadeOutStoryboard.Completed -= completedHandler;
                    };
                    
                    fadeOutStoryboard.Completed += completedHandler;
                    fadeOutStoryboard.Begin(this);
                    await tcs.Task;
                }
                else
                {
                    Hide();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during hide animation");
                Hide();
            }
        }
    }

    /// <summary>
    /// Элемент для отображения в переключателе приложений
    /// </summary>
    public class AppSwitcherItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int ProcessId { get; set; }
        public int ApplicationId { get; set; }
        public string InstanceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string IconText { get; set; } = "📱";
        public bool IsResponding { get; set; }
        public bool IsMinimized { get; set; }
        public long MemoryUsageMB { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

}
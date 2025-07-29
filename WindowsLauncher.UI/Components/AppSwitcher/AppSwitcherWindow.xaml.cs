using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.UI.Components.AppSwitcher
{
    /// <summary>
    /// Окно переключателя приложений (Alt+Tab аналог)
    /// </summary>
    public partial class AppSwitcherWindow : Window
    {
        private readonly IRunningApplicationsService _runningAppsService;
        private readonly ILogger<AppSwitcherWindow> _logger;
        private readonly ObservableCollection<AppSwitcherItem> _runningApplications;
        private int _selectedIndex = 0;
        private ShellMode _shellMode = ShellMode.Normal;

        public ObservableCollection<AppSwitcherItem> RunningApplications => _runningApplications;

        public AppSwitcherWindow(IRunningApplicationsService runningAppsService, ILogger<AppSwitcherWindow> logger)
        {
            InitializeComponent();
            
            _runningAppsService = runningAppsService ?? throw new ArgumentNullException(nameof(runningAppsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runningApplications = new ObservableCollection<AppSwitcherItem>();
            
            DataContext = this;
            
            // Подписываемся на события клавиатуры
            PreviewKeyDown += AppSwitcherWindow_PreviewKeyDown;
            Loaded += AppSwitcherWindow_Loaded;
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
                    ShellMode.Shell => "Режим: Shell (Alt+Tab, Ctrl+Alt+Tab)",
                    ShellMode.Normal => "Режим: Обычный Windows (Win+`, Win+Shift+`)",
                    _ => "Режим: Неизвестный"
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
                    _logger.LogInformation("Switching to application: {Name} (PID: {ProcessId})", 
                        selectedApp.Name, selectedApp.ProcessId);
                    
                    var success = await _runningAppsService.SwitchToApplicationAsync(selectedApp.ProcessId);
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully switched to {Name}", selectedApp.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to switch to {Name}", selectedApp.Name);
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
                var runningApps = await _runningAppsService.GetRunningApplicationsAsync();
                
                _runningApplications.Clear();
                
                foreach (var app in runningApps.OrderBy(a => a.Name))
                {
                    _runningApplications.Add(new AppSwitcherItem
                    {
                        ProcessId = app.ProcessId,
                        ApplicationId = app.ApplicationId,
                        Name = app.Name,
                        IconText = GetAppIcon(app.Name),
                        IsResponding = app.IsResponding,
                        IsMinimized = app.IsMinimized,
                        MemoryUsageMB = app.MemoryUsageMB,
                        IsSelected = false
                    });
                }
                
                _logger.LogDebug("Loaded {Count} running applications for switcher", _runningApplications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading running applications for switcher");
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
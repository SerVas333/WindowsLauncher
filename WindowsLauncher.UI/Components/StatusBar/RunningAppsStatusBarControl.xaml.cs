using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Infrastructure.Commands;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace WindowsLauncher.UI.Components.StatusBar
{
    /// <summary>
    /// Контрол для отображения статуса запущенных приложений в статус-баре
    /// </summary>
    public partial class RunningAppsStatusBarControl : WpfUserControl, INotifyPropertyChanged
    {
        private ILogger<RunningAppsStatusBarControl>? _logger;
        private IRunningApplicationsService? _runningApplicationsService;
        private ISessionManagementService? _sessionManagementService;
        
        private int _runningApplicationsCount;
        private long _totalMemoryUsageMB;
        private bool _hasRunningApplications;

        public event PropertyChangedEventHandler? PropertyChanged;

        public RunningAppsStatusBarControl()
        {
            InitializeComponent();
            DataContext = this;
            Unloaded += OnUnloaded;
            
            // Откладываем инициализацию до загрузки контрола
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем что ServiceProvider доступен
                if (WpfApplication.Current == null || ((App)WpfApplication.Current).ServiceProvider == null)
                {
                    System.Diagnostics.Debug.WriteLine("ServiceProvider not ready, skipping RunningAppsStatusBarControl initialization");
                    return;
                }

                var serviceProvider = ((App)WpfApplication.Current).ServiceProvider;
                _logger = serviceProvider.GetRequiredService<ILogger<RunningAppsStatusBarControl>>();
                _runningApplicationsService = serviceProvider.GetRequiredService<IRunningApplicationsService>();
                _sessionManagementService = serviceProvider.GetRequiredService<ISessionManagementService>();

                await InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize RunningAppsStatusBarControl: {ex.Message}");
            }
        }

        #region Properties

        public int RunningApplicationsCount
        {
            get => _runningApplicationsCount;
            private set
            {
                if (_runningApplicationsCount != value)
                {
                    _runningApplicationsCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasRunningApplications));
                }
            }
        }

        public long TotalMemoryUsageMB
        {
            get => _totalMemoryUsageMB;
            private set
            {
                if (_totalMemoryUsageMB != value)
                {
                    _totalMemoryUsageMB = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedMemoryUsage));
                }
            }
        }

        public bool HasRunningApplications
        {
            get => _hasRunningApplications;
            private set
            {
                if (_hasRunningApplications != value)
                {
                    _hasRunningApplications = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FormattedMemoryUsage
        {
            get
            {
                if (TotalMemoryUsageMB < 1024)
                {
                    return $"{TotalMemoryUsageMB} МБ";
                }
                else
                {
                    var gb = TotalMemoryUsageMB / 1024.0;
                    return $"{gb:F1} ГБ";
                }
            }
        }

        public ICommand ShowRunningAppsMenuCommand { get; private set; }

        #endregion

        private async Task InitializeAsync()
        {
            try
            {
                // Проверяем что все сервисы доступны
                if (_logger == null || _runningApplicationsService == null || _sessionManagementService == null)
                {
                    System.Diagnostics.Debug.WriteLine("Services not ready for RunningAppsStatusBarControl initialization");
                    return;
                }

                // Инициализируем команды
                ShowRunningAppsMenuCommand = new RelayCommand(async () => await ShowRunningAppsMenu());

                // Подписываемся на события
                _runningApplicationsService.ApplicationStarted += OnApplicationStarted;
                _runningApplicationsService.ApplicationExited += OnApplicationExited;
                _runningApplicationsService.ApplicationStatusChanged += OnApplicationStatusChanged;

                // Обновляем начальное состояние
                await UpdateStatus();

                _logger.LogDebug("RunningAppsStatusBarControl initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize RunningAppsStatusBarControl");
                System.Diagnostics.Debug.WriteLine($"RunningAppsStatusBarControl initialization failed: {ex.Message}");
            }
        }

        private async Task UpdateStatus()
        {
            try
            {
                if (_sessionManagementService == null || _runningApplicationsService == null)
                {
                    return;
                }

                var currentUser = _sessionManagementService.CurrentUser;
                if (currentUser == null)
                {
                    RunningApplicationsCount = 0;
                    TotalMemoryUsageMB = 0;
                    HasRunningApplications = false;
                    return;
                }

                var runningApps = await _runningApplicationsService.GetUserRunningApplicationsAsync(currentUser.Username);
                
                RunningApplicationsCount = runningApps.Count;
                TotalMemoryUsageMB = runningApps.Sum(app => app.MemoryUsageMB);
                HasRunningApplications = runningApps.Any();

                _logger?.LogDebug("Status updated: {Count} apps, {Memory} MB, HasRunning: {HasRunning}", 
                    RunningApplicationsCount, TotalMemoryUsageMB, HasRunningApplications);
                    
                if (runningApps.Any())
                {
                    var appNames = string.Join(", ", runningApps.Select(a => a.Name));
                    _logger?.LogDebug("Running applications: {Apps}", appNames);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update status");
                System.Diagnostics.Debug.WriteLine($"RunningAppsStatusBarControl.UpdateStatus failed: {ex.Message}");
            }
        }

        private async Task ShowRunningAppsMenu()
        {
            try
            {
                if (_sessionManagementService == null || _runningApplicationsService == null)
                {
                    return;
                }

                var currentUser = _sessionManagementService.CurrentUser;
                if (currentUser == null)
                    return;

                var runningApps = await _runningApplicationsService.GetUserRunningApplicationsAsync(currentUser.Username);
                
                if (!runningApps.Any())
                {
                    WpfMessageBox.Show("Нет запущенных приложений", "Информация", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Создаем контекстное меню
                var contextMenu = new ContextMenu();

                foreach (var app in runningApps.OrderBy(a => a.Name))
                {
                    var menuItem = new MenuItem
                    {
                        Header = $"{app.IconText} {app.Name}",
                        Tag = app
                    };

                    // Добавляем информацию о статусе
                    var statusInfo = new MenuItem
                    {
                        Header = $"PID: {app.ProcessId}, Память: {app.MemoryUsageMB} МБ",
                        IsEnabled = false
                    };
                    menuItem.Items.Add(statusInfo);

                    menuItem.Items.Add(new Separator());

                    // Действия с приложением
                    var switchItem = new MenuItem { Header = "Переключиться" };
                    switchItem.Click += async (s, e) => await SwitchToApplication(app.ProcessId);
                    menuItem.Items.Add(switchItem);

                    if (app.IsMinimized)
                    {
                        var restoreItem = new MenuItem { Header = "Развернуть" };
                        restoreItem.Click += async (s, e) => await RestoreApplication(app.ProcessId);
                        menuItem.Items.Add(restoreItem);
                    }
                    else
                    {
                        var minimizeItem = new MenuItem { Header = "Свернуть" };
                        minimizeItem.Click += async (s, e) => await MinimizeApplication(app.ProcessId);
                        menuItem.Items.Add(minimizeItem);
                    }

                    var closeItem = new MenuItem { Header = "Закрыть" };
                    closeItem.Click += async (s, e) => await CloseApplication(app.ProcessId, app.Name);
                    menuItem.Items.Add(closeItem);

                    contextMenu.Items.Add(menuItem);
                }

                // Показываем меню рядом с кнопкой
                contextMenu.PlacementTarget = this;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                contextMenu.IsOpen = true;

                _logger?.LogDebug("Shown running apps context menu with {Count} items", runningApps.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show running apps menu");
                WpfMessageBox.Show("Произошла ошибка при отображении меню", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SwitchToApplication(int processId)
        {
            try
            {
                if (_runningApplicationsService == null) return;

                var success = await _runningApplicationsService.SwitchToApplicationAsync(processId);
                if (!success)
                {
                    WpfMessageBox.Show("Не удалось переключиться на приложение", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to switch to application PID {ProcessId}", processId);
            }
        }

        private async Task MinimizeApplication(int processId)
        {
            try
            {
                if (_runningApplicationsService == null) return;
                await _runningApplicationsService.MinimizeApplicationAsync(processId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to minimize application PID {ProcessId}", processId);
            }
        }

        private async Task RestoreApplication(int processId)
        {
            try
            {
                if (_runningApplicationsService == null) return;
                await _runningApplicationsService.RestoreApplicationAsync(processId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to restore application PID {ProcessId}", processId);
            }
        }

        private async Task CloseApplication(int processId, string appName)
        {
            try
            {
                if (_runningApplicationsService == null) return;

                var result = WpfMessageBox.Show(
                    $"Вы уверены, что хотите закрыть {appName}?",
                    "Подтверждение закрытия",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var success = await _runningApplicationsService.CloseApplicationAsync(processId);
                    if (!success)
                    {
                        WpfMessageBox.Show($"Не удалось корректно закрыть {appName}", "Предупреждение", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close application PID {ProcessId}", processId);
            }
        }

        #region Event Handlers

        private async void OnApplicationStarted(object? sender, RunningApplicationEventArgs e)
        {
            _logger?.LogDebug("Application started event received: {AppName}", e.Application.Name);
            await Dispatcher.InvokeAsync(async () => await UpdateStatus());
        }

        private async void OnApplicationExited(object? sender, RunningApplicationEventArgs e)
        {
            _logger?.LogDebug("Application exited event received: {AppName}", e.Application.Name);
            await Dispatcher.InvokeAsync(async () => await UpdateStatus());
        }

        private async void OnApplicationStatusChanged(object? sender, RunningApplicationEventArgs e)
        {
            _logger?.LogDebug("Application status changed event received: {AppName}, Action: {Action}", 
                e.Application.Name, e.Action);
            await Dispatcher.InvokeAsync(async () => await UpdateStatus());
        }

        #endregion

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отписываемся от событий
                if (_runningApplicationsService != null)
                {
                    _runningApplicationsService.ApplicationStarted -= OnApplicationStarted;
                    _runningApplicationsService.ApplicationExited -= OnApplicationExited;
                    _runningApplicationsService.ApplicationStatusChanged -= OnApplicationStatusChanged;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unsubscribe from events");
                System.Diagnostics.Debug.WriteLine($"RunningAppsStatusBarControl cleanup failed: {ex.Message}");
            }
        }
    }
}
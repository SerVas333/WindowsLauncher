using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.UI.Components.SystemTray
{
    /// <summary>
    /// Менеджер системного трея для управления запущенными приложениями
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private readonly ILogger<SystemTrayManager> _logger;
        private readonly IRunningApplicationsService _runningApplicationsService;
        private readonly ISessionManagementService _sessionManagementService;
        private readonly IServiceProvider _serviceProvider;
        
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _runningAppsMenuItem;
        private ToolStripSeparator? _separator;
        private ToolStripMenuItem? _showLauncherMenuItem;
        private ToolStripMenuItem? _exitMenuItem;
        
        private bool _disposed;
        private bool _isInitialized;

        public SystemTrayManager(
            ILogger<SystemTrayManager> logger,
            IRunningApplicationsService runningApplicationsService,
            ISessionManagementService sessionManagementService,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runningApplicationsService = runningApplicationsService ?? throw new ArgumentNullException(nameof(runningApplicationsService));
            _sessionManagementService = sessionManagementService ?? throw new ArgumentNullException(nameof(sessionManagementService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Инициализировать системный трей
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                await Task.Run(() =>
                {
                    // Создаем NotifyIcon
                    _notifyIcon = new NotifyIcon
                    {
                        Text = "KDV Corporate Portal",
                        Visible = true,
                        Icon = LoadApplicationIcon()
                    };

                    // Создаем контекстное меню
                    CreateContextMenu();
                    
                    // Привязываем события
                    _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
                    _notifyIcon.ContextMenuStrip = _contextMenu;

                    _logger.LogInformation("System tray initialized successfully");
                });

                // Подписываемся на события сервиса запущенных приложений
                _runningApplicationsService.ApplicationStarted += OnApplicationStarted;
                _runningApplicationsService.ApplicationExited += OnApplicationExited;
                _runningApplicationsService.ApplicationStatusChanged += OnApplicationStatusChanged;

                // Обновляем меню с текущими приложениями
                await UpdateRunningApplicationsMenu();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system tray");
                throw;
            }
        }

        /// <summary>
        /// Показать системный трей
        /// </summary>
        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
                _logger.LogDebug("System tray icon shown");
            }
        }

        /// <summary>
        /// Скрыть системный трей
        /// </summary>
        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _logger.LogDebug("System tray icon hidden");
            }
        }

        /// <summary>
        /// Показать уведомление в системном трее
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
                _logger.LogDebug("Shown notification: {Title} - {Message}", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show notification: {Title}", title);
            }
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // Пункт "Запущенные приложения"
            _runningAppsMenuItem = new ToolStripMenuItem("Запущенные приложения")
            {
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            _contextMenu.Items.Add(_runningAppsMenuItem);

            // Разделитель
            _separator = new ToolStripSeparator();
            _contextMenu.Items.Add(_separator);

            // Пункт "Показать лаунчер"
            _showLauncherMenuItem = new ToolStripMenuItem("Показать лаунчер");
            _showLauncherMenuItem.Click += OnShowLauncherClick;
            _contextMenu.Items.Add(_showLauncherMenuItem);

            // Пункт "Выход"
            _exitMenuItem = new ToolStripMenuItem("Выход");
            _exitMenuItem.Click += OnExitClick;
            _contextMenu.Items.Add(_exitMenuItem);

            _logger.LogDebug("Context menu created");
        }

        private Icon LoadApplicationIcon()
        {
            try
            {
                // Пытаемся загрузить иконку из ресурсов приложения
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "KDV_icon.ico");
                
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                // Если файл не найден, создаем простую иконку
                _logger.LogWarning("Application icon not found at {IconPath}, using default", iconPath);
                return SystemIcons.Application;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load application icon, using default");
                return SystemIcons.Application;
            }
        }

        private async Task UpdateRunningApplicationsMenu()
        {
            try
            {
                if (_runningAppsMenuItem == null)
                    return;

                // Очищаем существующие элементы
                _runningAppsMenuItem.DropDownItems.Clear();

                // Получаем запущенные приложения текущего пользователя
                var currentUser = _sessionManagementService.CurrentUser;
                if (currentUser == null)
                {
                    var noAppsItem = new ToolStripMenuItem("Нет запущенных приложений")
                    {
                        Enabled = false
                    };
                    _runningAppsMenuItem.DropDownItems.Add(noAppsItem);
                    return;
                }

                var runningApps = await _runningApplicationsService.GetUserRunningApplicationsAsync(currentUser.Username);

                if (!runningApps.Any())
                {
                    var noAppsItem = new ToolStripMenuItem("Нет запущенных приложений")
                    {
                        Enabled = false
                    };
                    _runningAppsMenuItem.DropDownItems.Add(noAppsItem);
                    return;
                }

                // Добавляем элементы для каждого запущенного приложения
                foreach (var app in runningApps.OrderBy(a => a.Name))
                {
                    var appItem = new ToolStripMenuItem($"{app.IconText} {app.Name}")
                    {
                        Tag = app,
                        ToolTipText = $"PID: {app.ProcessId}, Память: {app.MemoryUsageMB} МБ"
                    };

                    // Выделяем активные приложения
                    if (app.IsActive)
                    {
                        appItem.Font = new Font(_contextMenu?.Font ?? SystemFonts.MenuFont, FontStyle.Bold);
                    }

                    // Показываем свернутые приложения
                    if (app.IsMinimized)
                    {
                        appItem.Text += " (свернуто)";
                        appItem.ForeColor = Color.Gray;
                    }

                    // Показываем не отвечающие приложения
                    if (!app.IsResponding)
                    {
                        appItem.Text += " (не отвечает)";
                        appItem.ForeColor = Color.Red;
                    }

                    // Подменю для действий с приложением
                    var switchItem = new ToolStripMenuItem("Переключиться");
                    switchItem.Click += async (s, e) => await OnSwitchToApplicationClick(app.ProcessId);
                    appItem.DropDownItems.Add(switchItem);

                    if (app.IsMinimized)
                    {
                        var restoreItem = new ToolStripMenuItem("Развернуть");
                        restoreItem.Click += async (s, e) => await OnRestoreApplicationClick(app.ProcessId);
                        appItem.DropDownItems.Add(restoreItem);
                    }
                    else
                    {
                        var minimizeItem = new ToolStripMenuItem("Свернуть");
                        minimizeItem.Click += async (s, e) => await OnMinimizeApplicationClick(app.ProcessId);
                        appItem.DropDownItems.Add(minimizeItem);
                    }

                    var closeItem = new ToolStripMenuItem("Закрыть");
                    closeItem.Click += async (s, e) => await OnCloseApplicationClick(app.ProcessId);
                    appItem.DropDownItems.Add(closeItem);

                    _runningAppsMenuItem.DropDownItems.Add(appItem);
                }

                // Обновляем текст главного пункта
                _runningAppsMenuItem.Text = $"Запущенные приложения ({runningApps.Count})";

                _logger.LogDebug("Updated running applications menu with {Count} items", runningApps.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update running applications menu");
            }
        }

        private async void OnApplicationStarted(object? sender, RunningApplicationEventArgs e)
        {
            await UpdateRunningApplicationsMenu();
            ShowNotification("Приложение запущено", $"{e.Application.Name} было запущено", ToolTipIcon.Info);
        }

        private async void OnApplicationExited(object? sender, RunningApplicationEventArgs e)
        {
            await UpdateRunningApplicationsMenu();
            ShowNotification("Приложение завершено", $"{e.Application.Name} было закрыто", ToolTipIcon.Warning);
        }

        private async void OnApplicationStatusChanged(object? sender, RunningApplicationEventArgs e)
        {
            await UpdateRunningApplicationsMenu();
        }

        private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ShowMainWindowAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to show main window from tray");
                }
            });
        }

        private async void OnShowLauncherClick(object? sender, EventArgs e)
        {
            await ShowMainWindowAsync();
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            try
            {
                var result = System.Windows.Forms.MessageBox.Show(
                    "Вы уверены, что хотите закрыть приложение?",
                    "Подтверждение выхода",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Question);

                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        System.Windows.Application.Current.Shutdown();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle exit click");
            }
        }

        private async Task OnSwitchToApplicationClick(int processId)
        {
            try
            {
                var success = await _runningApplicationsService.SwitchToApplicationAsync(processId);
                if (!success)
                {
                    ShowNotification("Ошибка", "Не удалось переключиться на приложение", ToolTipIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch to application PID {ProcessId}", processId);
                ShowNotification("Ошибка", "Произошла ошибка при переключении", ToolTipIcon.Error);
            }
        }

        private async Task OnMinimizeApplicationClick(int processId)
        {
            try
            {
                await _runningApplicationsService.MinimizeApplicationAsync(processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to minimize application PID {ProcessId}", processId);
            }
        }

        private async Task OnRestoreApplicationClick(int processId)
        {
            try
            {
                await _runningApplicationsService.RestoreApplicationAsync(processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore application PID {ProcessId}", processId);
            }
        }

        private async Task OnCloseApplicationClick(int processId)
        {
            try
            {
                var app = await _runningApplicationsService.GetRunningApplicationByProcessIdAsync(processId);
                if (app != null)
                {
                    var result = System.Windows.Forms.MessageBox.Show(
                        $"Вы уверены, что хотите закрыть {app.Name}?",
                        "Подтверждение закрытия",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Question);

                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        var success = await _runningApplicationsService.CloseApplicationAsync(processId);
                        if (!success)
                        {
                            ShowNotification("Предупреждение", $"Не удалось корректно закрыть {app.Name}", ToolTipIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close application PID {ProcessId}", processId);
            }
        }

        private async Task ShowMainWindowAsync()
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                        {
                            mainWindow.WindowState = System.Windows.WindowState.Normal;
                        }

                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Topmost = true;
                        mainWindow.Topmost = false;
                        mainWindow.Focus();

                        _logger.LogDebug("Main window shown from system tray");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show main window");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
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

                    // Очищаем ресурсы UI
                    _notifyIcon?.Dispose();
                    _contextMenu?.Dispose();

                    _disposed = true;
                    _logger.LogDebug("System tray manager disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing system tray manager");
                }
            }
        }
    }
}
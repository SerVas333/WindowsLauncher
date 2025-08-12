using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle.Events;
using WindowsLauncher.UI.Components.AppSwitcher;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Сервис для управления переключателем приложений с интеграцией ApplicationLifecycleService
    /// </summary>
    public class AppSwitcherService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppSwitcherService> _logger;
        private readonly IApplicationLifecycleService _lifecycleService;
        private AppSwitcherWindow? _switcherWindow;
        private bool _disposed = false;
        private bool _eventsSubscribed = false;

        public AppSwitcherService(
            IServiceProvider serviceProvider,
            ILogger<AppSwitcherService> logger,
            IApplicationLifecycleService lifecycleService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            
            // Подписываемся на события жизненного цикла приложений
            SubscribeToLifecycleEvents();
        }

        /// <summary>
        /// Показать переключатель приложений
        /// </summary>
        public async Task ShowAppSwitcherAsync(ShellMode shellMode = ShellMode.Normal)
        {
            try
            {
                _logger.LogDebug("Showing application switcher");

                // Проверяем, есть ли запущенные приложения через новую архитектуру
                var runningAppsCount = await _lifecycleService.GetCountAsync();
                if (runningAppsCount == 0)
                {
                    _logger.LogInformation("No running applications to switch between");
                    return;
                }

                // Создаем новое окно переключателя если его нет
                if (_switcherWindow == null || !_switcherWindow.IsLoaded)
                {
                    CreateSwitcherWindow();
                }

                // Показываем переключатель
                if (_switcherWindow != null)
                {
                    await _switcherWindow.ShowSwitcherAsync(shellMode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing application switcher");
            }
        }

        /// <summary>
        /// Скрыть переключатель приложений
        /// </summary>
        public Task HideAppSwitcherAsync()
        {
            try
            {
                if (_switcherWindow != null && _switcherWindow.IsVisible)
                {
                    _switcherWindow.Hide();
                    _logger.LogDebug("Application switcher hidden");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding application switcher");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Перейти к следующему приложению в переключателе
        /// </summary>
        public async Task SelectNextApplicationAsync()
        {
            try
            {
                if (_switcherWindow != null && _switcherWindow.IsVisible)
                {
                    _switcherWindow.SelectNext();
                    _logger.LogDebug("Selected next application in switcher");
                }
                else
                {
                    // Если переключатель не открыт, показываем его
                    await ShowAppSwitcherAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting next application");
            }
        }

        /// <summary>
        /// Перейти к предыдущему приложению в переключателе
        /// </summary>
        public async Task SelectPreviousApplicationAsync()
        {
            try
            {
                if (_switcherWindow != null && _switcherWindow.IsVisible)
                {
                    _switcherWindow.SelectPrevious();
                    _logger.LogDebug("Selected previous application in switcher");
                }
                else
                {
                    // Если переключатель не открыт, показываем его
                    await ShowAppSwitcherAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting previous application");
            }
        }

        /// <summary>
        /// Переключиться на выбранное приложение
        /// </summary>
        public async Task<bool> SwitchToSelectedApplicationAsync()
        {
            try
            {
                if (_switcherWindow != null && _switcherWindow.IsVisible)
                {
                    bool success = await _switcherWindow.SwitchToSelectedApplicationAsync();
                    _logger.LogDebug("Switched to selected application: {Success}", success);
                    return success;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to selected application");
                return false;
            }
        }

        /// <summary>
        /// Создать новое окно переключателя
        /// </summary>
        private void CreateSwitcherWindow()
        {
            try
            {
                // Закрываем старое окно если есть
                if (_switcherWindow != null)
                {
                    _switcherWindow.Close();
                    _switcherWindow = null;
                }

                // Создаем новое окно через DI
                _switcherWindow = new AppSwitcherWindow(
                    _lifecycleService,
                    _serviceProvider.GetRequiredService<ILogger<AppSwitcherWindow>>()
                );

                _logger.LogDebug("Created new application switcher window");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating switcher window");
            }
        }

        /// <summary>
        /// Проверить, открыт ли переключатель
        /// </summary>
        public bool IsSwitcherVisible => _switcherWindow?.IsVisible ?? false;

        #region ApplicationLifecycleService Event Integration

        /// <summary>
        /// Подписка на события жизненного цикла приложений
        /// </summary>
        private void SubscribeToLifecycleEvents()
        {
            try
            {
                if (!_eventsSubscribed)
                {
                    _lifecycleService.InstanceStarted += OnApplicationInstanceStarted;
                    _lifecycleService.InstanceStopped += OnApplicationInstanceStopped;
                    _lifecycleService.InstanceStateChanged += OnApplicationInstanceStateChanged;
                    _lifecycleService.InstanceActivated += OnApplicationInstanceActivated;
                    
                    _eventsSubscribed = true;
                    _logger.LogDebug("Subscribed to ApplicationLifecycleService events");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to lifecycle events");
            }
        }

        /// <summary>
        /// Отписка от событий жизненного цикла приложений
        /// </summary>
        private void UnsubscribeFromLifecycleEvents()
        {
            try
            {
                if (_eventsSubscribed)
                {
                    _lifecycleService.InstanceStarted -= OnApplicationInstanceStarted;
                    _lifecycleService.InstanceStopped -= OnApplicationInstanceStopped;
                    _lifecycleService.InstanceStateChanged -= OnApplicationInstanceStateChanged;
                    _lifecycleService.InstanceActivated -= OnApplicationInstanceActivated;
                    
                    _eventsSubscribed = false;
                    _logger.LogDebug("Unsubscribed from ApplicationLifecycleService events");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from lifecycle events");
            }
        }

        /// <summary>
        /// Обработчик запуска экземпляра приложения
        /// </summary>
        private async void OnApplicationInstanceStarted(object? sender, ApplicationInstanceEventArgs e)
        {
            try
            {
                _logger.LogDebug("Application instance started: {AppName} (Instance: {InstanceId})", 
                    e.Instance.Application?.Name, e.Instance.InstanceId);
                
                // Обновляем переключатель если он открыт
                if (IsSwitcherVisible)
                {
                    await RefreshSwitcherAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling application instance started event");
            }
        }

        /// <summary>
        /// Обработчик остановки экземпляра приложения
        /// </summary>
        private async void OnApplicationInstanceStopped(object? sender, ApplicationInstanceEventArgs e)
        {
            try
            {
                _logger.LogDebug("Application instance stopped: {AppName} (Instance: {InstanceId})", 
                    e.Instance.Application?.Name, e.Instance.InstanceId);
                
                // Обновляем переключатель если он открыт
                if (IsSwitcherVisible)
                {
                    await RefreshSwitcherAsync();
                    
                    // Если не осталось приложений, скрываем переключатель
                    var runningCount = await _lifecycleService.GetCountAsync();
                    if (runningCount == 0)
                    {
                        await HideAppSwitcherAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling application instance stopped event");
            }
        }

        /// <summary>
        /// Обработчик изменения состояния экземпляра приложения
        /// </summary>
        private async void OnApplicationInstanceStateChanged(object? sender, ApplicationInstanceEventArgs e)
        {
            try
            {
                _logger.LogTrace("Application instance state changed: {AppName} -> {State}", 
                    e.Instance.Application?.Name, e.Instance.State);
                
                // Обновляем переключатель если он открыт и изменение значительное
                if (IsSwitcherVisible && IsSignificantStateChange(e))
                {
                    await RefreshSwitcherAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling application instance state changed event");
            }
        }

        /// <summary>
        /// Обработчик активации экземпляра приложения
        /// </summary>
        private async void OnApplicationInstanceActivated(object? sender, ApplicationInstanceEventArgs e)
        {
            try
            {
                _logger.LogDebug("Application instance activated: {AppName} (Instance: {InstanceId})", 
                    e.Instance.Application?.Name, e.Instance.InstanceId);
                
                // Скрываем переключатель при успешном переключении
                if (IsSwitcherVisible)
                {
                    await HideAppSwitcherAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling application instance activated event");
            }
        }

        /// <summary>
        /// Проверяет, является ли изменение состояния значительным для переключателя
        /// </summary>
        private bool IsSignificantStateChange(ApplicationInstanceEventArgs e)
        {
            // Значительные изменения: активация, сворачивание, восстановление, завершение
            return e.Reason?.Contains("Active") == true ||
                   e.Reason?.Contains("Minimized") == true ||
                   e.Reason?.Contains("Restored") == true ||
                   e.Reason?.Contains("Terminated") == true;
        }

        /// <summary>
        /// Обновить содержимое переключателя
        /// </summary>
        private async Task RefreshSwitcherAsync()
        {
            try
            {
                if (_switcherWindow != null && _switcherWindow.IsVisible)
                {
                    // Используем метод переключателя для обновления списка приложений
                    await _switcherWindow.RefreshApplicationsAsync();
                    _logger.LogTrace("Application switcher refreshed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing application switcher");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Отписываемся от событий
                    UnsubscribeFromLifecycleEvents();
                    
                    // Закрываем окно переключателя
                    _switcherWindow?.Close();
                    _switcherWindow = null;
                    
                    _logger.LogDebug("AppSwitcherService disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing AppSwitcherService");
                }
                
                _disposed = true;
            }
        }

        #endregion
    }
}
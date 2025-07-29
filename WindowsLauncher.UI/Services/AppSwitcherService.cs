using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.UI.Components.AppSwitcher;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Сервис для управления переключателем приложений
    /// </summary>
    public class AppSwitcherService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppSwitcherService> _logger;
        private readonly IRunningApplicationsService _runningAppsService;
        private AppSwitcherWindow? _switcherWindow;
        private bool _disposed = false;

        public AppSwitcherService(
            IServiceProvider serviceProvider,
            ILogger<AppSwitcherService> logger,
            IRunningApplicationsService runningAppsService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runningAppsService = runningAppsService ?? throw new ArgumentNullException(nameof(runningAppsService));
        }

        /// <summary>
        /// Показать переключатель приложений
        /// </summary>
        public async Task ShowAppSwitcherAsync(ShellMode shellMode = ShellMode.Normal)
        {
            try
            {
                _logger.LogDebug("Showing application switcher");

                // Проверяем, есть ли запущенные приложения
                var runningAppsCount = await _runningAppsService.GetRunningApplicationsCountAsync();
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
        public async Task HideAppSwitcherAsync()
        {
            await Task.CompletedTask;
            
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
        }

        /// <summary>
        /// Перейти к следующему приложению в переключателе
        /// </summary>
        public async Task SelectNextApplicationAsync()
        {
            await Task.CompletedTask;
            
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
            await Task.CompletedTask;
            
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
                    _runningAppsService,
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

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _switcherWindow?.Close();
                    _switcherWindow = null;
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
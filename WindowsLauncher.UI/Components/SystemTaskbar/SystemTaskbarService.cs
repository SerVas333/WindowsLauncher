using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Services;

namespace WindowsLauncher.UI.Components.SystemTaskbar
{
    /// <summary>
    /// Сервис для управления системной панелью задач
    /// </summary>
    public interface ISystemTaskbarService
    {
        /// <summary>
        /// Инициализировать панель задач
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Показать панель задач
        /// </summary>
        void ShowTaskbar();
        
        /// <summary>
        /// Скрыть панель задач
        /// </summary>
        void HideTaskbar();
        
        /// <summary>
        /// Проверить должна ли быть видна панель задач
        /// </summary>
        Task<bool> ShouldShowTaskbarAsync();
        
        /// <summary>
        /// Очистить ресурсы
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Реализация сервиса управления системной панелью задач
    /// </summary>
    public class SystemTaskbarService : ISystemTaskbarService, IDisposable
    {
        private readonly ILogger<SystemTaskbarService> _logger;
        private readonly ShellModeDetectionService _shellModeDetectionService;
        
        private SystemTaskbarWindow? _taskbarWindow;
        private bool _isInitialized = false;
        private bool _disposed = false;

        public SystemTaskbarService(
            ILogger<SystemTaskbarService> logger,
            ShellModeDetectionService shellModeDetectionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _shellModeDetectionService = shellModeDetectionService ?? throw new ArgumentNullException(nameof(shellModeDetectionService));
        }

        /// <summary>
        /// Инициализировать панель задач
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized || _disposed)
                return;

            try
            {
                _logger.LogInformation("Initializing SystemTaskbar service");

                // Определяем нужно ли показывать панель задач
                var shouldShow = await ShouldShowTaskbarAsync();
                
                if (shouldShow)
                {
                    ShowTaskbar();
                }
                else
                {
                    _logger.LogInformation("SystemTaskbar not needed in current mode, skipping initialization");
                }

                _isInitialized = true;
                _logger.LogInformation("SystemTaskbar service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SystemTaskbar service");
                throw;
            }
        }

        /// <summary>
        /// Показать панель задач
        /// </summary>
        public void ShowTaskbar()
        {
            try
            {
                if (_taskbarWindow != null)
                {
                    _logger.LogDebug("SystemTaskbar is already shown");
                    return;
                }

                // Создаем и показываем окно панели задач
                _taskbarWindow = new SystemTaskbarWindow();
                _taskbarWindow.Show();
                
                _logger.LogInformation("SystemTaskbar shown successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing SystemTaskbar");
                throw;
            }
        }

        /// <summary>
        /// Скрыть панель задач
        /// </summary>
        public void HideTaskbar()
        {
            try
            {
                if (_taskbarWindow != null)
                {
                    _taskbarWindow.Close();
                    _taskbarWindow = null;
                    _logger.LogInformation("SystemTaskbar hidden successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding SystemTaskbar");
            }
        }

        /// <summary>
        /// Проверить должна ли быть видна панель задач
        /// </summary>
        public async Task<bool> ShouldShowTaskbarAsync()
        {
            try
            {
                // Панель задач показываем только в Shell режиме
                var currentMode = await _shellModeDetectionService.DetectShellModeAsync();
                var shouldShow = currentMode == ShellMode.Shell;
                
                _logger.LogDebug("Shell mode: {Mode}, Should show taskbar: {ShouldShow}", currentMode, shouldShow);
                
                return shouldShow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining if taskbar should be shown");
                // В случае ошибки не показываем панель задач
                return false;
            }
        }

        /// <summary>
        /// Обновить видимость панели задач в зависимости от режима
        /// </summary>
        public async Task UpdateTaskbarVisibilityAsync()
        {
            try
            {
                var shouldShow = await ShouldShowTaskbarAsync();
                
                if (shouldShow && _taskbarWindow == null)
                {
                    ShowTaskbar();
                }
                else if (!shouldShow && _taskbarWindow != null)
                {
                    HideTaskbar();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating taskbar visibility");
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        HideTaskbar();
                        _logger.LogInformation("SystemTaskbar service disposed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing SystemTaskbar service");
                    }
                }
                
                _disposed = true;
            }
        }

        ~SystemTaskbarService()
        {
            Dispose(false);
        }

        #endregion
    }
}
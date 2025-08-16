using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.ViewModels.Base;
using WindowsLauncher.UI.Infrastructure.Services;
using WpfApplication = System.Windows.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления статусом Android подсистемы (WSA)
    /// </summary>
    public class WSAStatusViewModel : ViewModelBase
    {
        #region Fields

        private readonly IServiceScopeFactory _serviceScopeFactory;

        // WSA Status Fields
        private bool _showWSAStatus = false;
        private string _wsaStatusText = "";
        private string _wsaStatusTooltip = "";
        private string _wsaStatusColor = "#666666";

        #endregion

        #region Constructor

        public WSAStatusViewModel(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<WSAStatusViewModel> logger,
            IDialogService dialogService)
            : base(logger, dialogService)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Показывать ли индикатор статуса WSA в UI
        /// </summary>
        public bool ShowWSAStatus
        {
            get => _showWSAStatus;
            set => SetProperty(ref _showWSAStatus, value);
        }

        /// <summary>
        /// Текст статуса WSA
        /// </summary>
        public string WSAStatusText
        {
            get => _wsaStatusText;
            set => SetProperty(ref _wsaStatusText, value);
        }

        /// <summary>
        /// Подсказка для статуса WSA
        /// </summary>
        public string WSAStatusTooltip
        {
            get => _wsaStatusTooltip;
            set => SetProperty(ref _wsaStatusTooltip, value);
        }

        /// <summary>
        /// Цвет текста статуса WSA
        /// </summary>
        public string WSAStatusColor
        {
            get => _wsaStatusColor;
            set => SetProperty(ref _wsaStatusColor, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Инициализация статуса Android подсистемы
        /// </summary>
        public async Task InitializeWSAStatusAsync()
        {
            try
            {
                Logger.LogInformation("Starting WSA status initialization in WSAStatusViewModel");

                using var scope = _serviceScopeFactory.CreateScope();
                var androidSubsystem = scope.ServiceProvider.GetService<IAndroidSubsystemService>();

                if (androidSubsystem == null)
                {
                    Logger.LogWarning("Android subsystem service not available in WSAStatusViewModel");
                    ShowWSAStatus = false;
                    return;
                }

                var mode = androidSubsystem.CurrentMode;
                var currentStatus = androidSubsystem.WSAStatus;
                Logger.LogInformation("WSAStatusViewModel: AndroidSubsystemService mode: {Mode}, current status: {Status}", mode, currentStatus);

                // Показываем статус только если включено в конфигурациях
                ShowWSAStatus = androidSubsystem.CurrentMode != AndroidMode.Disabled;

                if (!ShowWSAStatus)
                {
                    Logger.LogDebug("WSA status hidden - Android subsystem disabled");
                    return;
                }

                // Подписываемся на изменения статуса
                androidSubsystem.StatusChanged += OnWSAStatusChanged;

                // Устанавливаем начальный статус
                await UpdateWSAStatusDisplayAsync(androidSubsystem);

                Logger.LogInformation("WSA status initialized for {Mode} mode", androidSubsystem.CurrentMode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize WSA status");
                ShowWSAStatus = false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Обработчик изменения статуса WSA
        /// </summary>
        private async void OnWSAStatusChanged(object? sender, string status)
        {
            try
            {
                if (sender is IAndroidSubsystemService androidSubsystem)
                {
                    await UpdateWSAStatusDisplayAsync(androidSubsystem);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling WSA status change");
            }
        }

        /// <summary>
        /// Обновление отображения статуса WSA
        /// </summary>
        private async Task UpdateWSAStatusDisplayAsync(IAndroidSubsystemService androidSubsystem)
        {
            try
            {
                var status = androidSubsystem.WSAStatus;
                var mode = androidSubsystem.CurrentMode;

                // Обновляем UI в главном потоке
                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    WSAStatusText = GetLocalizedStatusText(status);
                    WSAStatusColor = GetStatusColor(status);
                    WSAStatusTooltip = GetStatusTooltip(status, mode);
                });

                Logger.LogDebug("WSA status updated: {Status} in {Mode} mode", status, mode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update WSA status display");
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
                "Available" => "Доступен",
                "Unavailable" => "Недоступен",
                "Disabled" => "Отключен",
                "Error" => "Ошибка",
                "Initializing" => "Инициализация",
                "Suspended (Low Memory)" => "Приостановлен",
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
                "Available" => "#2196F3",       // Синий
                "Starting" or "Initializing" => "#FF9800",  // Оранжевый
                "Stopping" => "#FF9800",        // Оранжевый
                "Error" => "#F44336",           // Красный
                "Unavailable" or "Disabled" => "#757575",  // Серый
                "Suspended (Low Memory)" => "#FF5722",     // Темно-оранжевый
                _ => "#666666"                  // Серый по умолчанию
            };
        }

        /// <summary>
        /// Получить подсказку для статуса
        /// </summary>
        private string GetStatusTooltip(string status, AndroidMode mode)
        {
            var modeText = mode switch
            {
                AndroidMode.Disabled => "отключен",
                AndroidMode.OnDemand => "по требованию", 
                AndroidMode.Preload => "предзагрузка",
                _ => mode.ToString()
            };

            return status switch
            {
                "Ready" => $"Android подсистема готова к работе\nРежим: {modeText}",
                "Starting" => $"Запуск Android подсистемы...\nРежим: {modeText}",
                "Available" => $"Android подсистема доступна\nРежим: {modeText}",
                "Unavailable" => $"Android подсистема недоступна\nПроверьте установку WSA",
                "Error" => $"Ошибка Android подсистемы\nПроверьте логи для подробностей",
                "Disabled" => "Android функции отключены в настройках",
                "Suspended (Low Memory)" => "Android подсистема приостановлена\nНедостаточно свободной памяти",
                _ => $"Android подсистема: {status}\nРежим: {modeText}"
            };
        }

        #endregion
    }
}
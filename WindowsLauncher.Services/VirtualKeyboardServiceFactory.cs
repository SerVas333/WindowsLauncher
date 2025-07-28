using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Фабрика для создания подходящего сервиса виртуальной клавиатуры 
    /// в зависимости от версии Windows
    /// </summary>
    public class VirtualKeyboardServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VirtualKeyboardServiceFactory> _logger;

        public VirtualKeyboardServiceFactory(
            IServiceProvider serviceProvider,
            ILogger<VirtualKeyboardServiceFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Создать подходящий сервис виртуальной клавиатуры для текущей ОС
        /// </summary>
        public IVirtualKeyboardService CreateKeyboardService()
        {
            try
            {
                var windowsVersion = WindowsVersionHelper.GetWindowsVersion();
                var compatibility = WindowsVersionHelper.GetTouchKeyboardCompatibility();
                var versionDescription = WindowsVersionHelper.GetVersionDescription();

                _logger.LogInformation("Обнаружена версия Windows: {Version}", versionDescription);
                _logger.LogInformation("Совместимость с клавиатурой: {Compatibility}", compatibility);

                return compatibility switch
                {
                    TouchKeyboardCompatibility.TabTipWithCOM => CreateWindows10Service(),
                    TouchKeyboardCompatibility.TextInputHost => CreateWindows11Service(),
                    TouchKeyboardCompatibility.TabTipLegacy => CreateLegacyTabTipService(),
                    TouchKeyboardCompatibility.OSKOnly => CreateOSKService(),
                    _ => CreateFallbackService()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании сервиса клавиатуры, используем fallback");
                return CreateFallbackService();
            }
        }

        private IVirtualKeyboardService CreateWindows10Service()
        {
            _logger.LogInformation("Создание Windows 10 сервиса клавиатуры с COM интерфейсом");
            
            try
            {
                return _serviceProvider.GetRequiredService<Windows10TouchKeyboardService>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось создать Windows 10 сервис, используем универсальный");
                return CreateUniversalService();
            }
        }

        private IVirtualKeyboardService CreateWindows11Service()
        {
            _logger.LogInformation("Создание Windows 11 сервиса клавиатуры");
            
            // Пока используем универсальный сервис для Windows 11
            // В будущем можно создать специализированный Windows11TouchKeyboardService
            return CreateUniversalService();
        }

        private IVirtualKeyboardService CreateLegacyTabTipService()
        {
            _logger.LogInformation("Создание legacy TabTip сервиса для Windows 8/8.1");
            return CreateUniversalService();
        }

        private IVirtualKeyboardService CreateOSKService()
        {
            _logger.LogInformation("Создание OSK сервиса для Windows 7 и ранее");
            return CreateUniversalService();
        }

        private IVirtualKeyboardService CreateUniversalService()
        {
            _logger.LogInformation("Создание универсального сервиса клавиатуры");
            return _serviceProvider.GetRequiredService<VirtualKeyboardService>();
        }

        private IVirtualKeyboardService CreateFallbackService()
        {
            _logger.LogWarning("Создание fallback сервиса клавиатуры");
            return CreateUniversalService();
        }
    }

    /// <summary>
    /// Декоратор для автоматического выбора сервиса клавиатуры
    /// </summary>
    public class AdaptiveVirtualKeyboardService : IVirtualKeyboardService
    {
        private readonly IVirtualKeyboardService _innerService;
        private readonly ILogger<AdaptiveVirtualKeyboardService> _logger;

        public event EventHandler<VirtualKeyboardStateChangedEventArgs>? StateChanged;

        public AdaptiveVirtualKeyboardService(
            VirtualKeyboardServiceFactory factory,
            ILogger<AdaptiveVirtualKeyboardService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _innerService = factory.CreateKeyboardService();
            
            // Пробрасываем событие от внутреннего сервиса
            _innerService.StateChanged += OnInnerServiceStateChanged;
            
            var windowsVersion = WindowsVersionHelper.GetVersionDescription();
            _logger.LogInformation("Создан адаптивный сервис клавиатуры для {Version}", windowsVersion);
        }

        private void OnInnerServiceStateChanged(object? sender, VirtualKeyboardStateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        public async Task<bool> ShowVirtualKeyboardAsync()
        {
            _logger.LogDebug("Показ виртуальной клавиатуры через адаптивный сервис");
            return await _innerService.ShowVirtualKeyboardAsync();
        }

        public async Task<bool> HideVirtualKeyboardAsync()
        {
            _logger.LogDebug("Скрытие виртуальной клавиатуры через адаптивный сервис");
            return await _innerService.HideVirtualKeyboardAsync();
        }

        public async Task<bool> ToggleVirtualKeyboardAsync()
        {
            _logger.LogDebug("Переключение виртуальной клавиатуры через адаптивный сервис");
            return await _innerService.ToggleVirtualKeyboardAsync();
        }

        public async Task<bool> RepositionKeyboardAsync()
        {
            _logger.LogDebug("Репозиционирование клавиатуры через адаптивный сервис");
            return await _innerService.RepositionKeyboardAsync();
        }

        public bool IsVirtualKeyboardRunning()
        {
            return _innerService.IsVirtualKeyboardRunning();
        }

        public bool IsVirtualKeyboardAvailable()
        {
            return _innerService.IsVirtualKeyboardAvailable();
        }

        public async Task<string> DiagnoseVirtualKeyboardAsync()
        {
            var diagnosis = await _innerService.DiagnoseVirtualKeyboardAsync();
            var versionInfo = WindowsVersionHelper.GetVersionDescription();
            var compatibility = WindowsVersionHelper.GetTouchKeyboardCompatibility();
            
            return $"=== АДАПТИВНЫЙ СЕРВИС КЛАВИАТУРЫ ===\n" +
                   $"Версия Windows: {versionInfo}\n" +
                   $"Совместимость: {compatibility}\n" +
                   $"Используемый сервис: {_innerService.GetType().Name}\n" +
                   $"===================================\n\n" +
                   diagnosis;
        }
    }
}
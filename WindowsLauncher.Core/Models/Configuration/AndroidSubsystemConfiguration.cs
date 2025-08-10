using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models.Configuration
{
    /// <summary>
    /// Конфигурация Android подсистемы (WSA)
    /// </summary>
    public class AndroidSubsystemConfiguration
    {
        /// <summary>
        /// Режим работы Android подсистемы
        /// </summary>
        public AndroidMode Mode { get; set; } = AndroidMode.OnDemand;

        /// <summary>
        /// Задержка в секундах перед предзагрузкой WSA (для режима Preload)
        /// </summary>
        public int PreloadDelaySeconds { get; set; } = 30;

        /// <summary>
        /// Автоматически запускать WSA при необходимости
        /// </summary>
        public bool AutoStartWSA { get; set; } = true;

        /// <summary>
        /// Показывать статус WSA в пользовательском интерфейсе
        /// </summary>
        public bool ShowStatusInUI { get; set; } = true;

        /// <summary>
        /// Включить расширенную диагностику Android подсистемы
        /// </summary>
        public bool EnableDiagnostics { get; set; } = true;

        /// <summary>
        /// Настройки оптимизации ресурсов
        /// </summary>
        public ResourceOptimizationSettings ResourceOptimization { get; set; } = new ResourceOptimizationSettings();

        /// <summary>
        /// Настройки fallback поведения
        /// </summary>
        public FallbackSettings Fallback { get; set; } = new FallbackSettings();
    }

    /// <summary>
    /// Настройки оптимизации ресурсов WSA
    /// </summary>
    public class ResourceOptimizationSettings
    {
        /// <summary>
        /// Максимальное потребление памяти WSA в мегабайтах
        /// </summary>
        public int MaxMemoryMB { get; set; } = 2048;

        /// <summary>
        /// Останавливать WSA при простое
        /// </summary>
        public bool StopWSAOnIdle { get; set; } = false;

        /// <summary>
        /// Время простоя в минутах перед остановкой WSA
        /// </summary>
        public int IdleTimeoutMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Настройки fallback поведения при проблемах
    /// </summary>
    public class FallbackSettings
    {
        /// <summary>
        /// Отключить Android подсистему при недостатке памяти
        /// </summary>
        public bool DisableOnLowMemory { get; set; } = true;

        /// <summary>
        /// Порог свободной памяти в МБ для отключения Android функций
        /// </summary>
        public int MemoryThresholdMB { get; set; } = 4096;
    }
}
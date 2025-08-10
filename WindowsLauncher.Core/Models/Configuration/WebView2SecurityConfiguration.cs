using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models.Configuration
{
    /// <summary>
    /// Конфигурация безопасности WebView2
    /// </summary>
    public class WebView2SecurityConfiguration
    {
        /// <summary>
        /// Стратегия очистки персональных данных
        /// По умолчанию: OnUserSwitch для оптимального баланса
        /// </summary>
        public DataClearingStrategy DataClearingStrategy { get; set; } = DataClearingStrategy.OnUserSwitch;

        /// <summary>
        /// Очищать cookies немедленно при закрытии окна
        /// Даже если стратегия не Immediate, критичные cookies могут очищаться сразу
        /// </summary>
        public bool ClearCookiesImmediately { get; set; } = false;

        /// <summary>
        /// Очищать кэш при выходе из приложения
        /// Применимо для стратегий OnUserSwitch и OnAppExit
        /// </summary>
        public bool ClearCacheOnExit { get; set; } = true;

        /// <summary>
        /// Режим secure environment - принудительная немедленная очистка
        /// Переопределяет DataClearingStrategy на Immediate для максимальной безопасности
        /// </summary>
        public bool SecureEnvironment { get; set; } = false;

        /// <summary>
        /// Включить детальное логирование операций очистки
        /// </summary>
        public bool EnableAuditLogging { get; set; } = true;

        /// <summary>
        /// Таймаут операций очистки в миллисекундах
        /// </summary>
        public int CleanupTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Количество повторных попыток при ошибках очистки
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Получить эффективную стратегию с учетом SecureEnvironment
        /// </summary>
        public DataClearingStrategy GetEffectiveStrategy()
        {
            return SecureEnvironment ? DataClearingStrategy.Immediate : DataClearingStrategy;
        }

        /// <summary>
        /// Проверить валидность конфигурации
        /// </summary>
        public bool IsValid()
        {
            return CleanupTimeoutMs > 0 && 
                   RetryAttempts >= 0 && 
                   RetryAttempts <= 10; // Разумное ограничение на повторы
        }

        /// <summary>
        /// Применить значения по умолчанию для невалидных настроек
        /// </summary>
        public void ApplyDefaults()
        {
            if (CleanupTimeoutMs <= 0)
                CleanupTimeoutMs = 5000;

            if (RetryAttempts < 0 || RetryAttempts > 10)
                RetryAttempts = 3;
        }
    }
}
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models.Configuration;

namespace WindowsLauncher.Core.Interfaces.Security
{
    /// <summary>
    /// Интерфейс для управления конфигурацией безопасности WebView2
    /// </summary>
    public interface IWebView2SecurityConfigurationService
    {
        /// <summary>
        /// Получить текущую конфигурацию безопасности WebView2
        /// </summary>
        WebView2SecurityConfiguration GetConfiguration();

        /// <summary>
        /// Получить эффективную стратегию очистки данных
        /// </summary>
        DataClearingStrategy GetEffectiveDataClearingStrategy();

        /// <summary>
        /// Проверить, включено ли немедленное очищение cookies
        /// </summary>
        bool ShouldClearCookiesImmediately();

        /// <summary>
        /// Проверить, должен ли кэш очищаться при выходе
        /// </summary>
        bool ShouldClearCacheOnExit();

        /// <summary>
        /// Включено ли детальное логирование операций очистки
        /// </summary>
        bool IsAuditLoggingEnabled();

        /// <summary>
        /// Получить таймаут операций очистки
        /// </summary>
        int GetCleanupTimeoutMs();

        /// <summary>
        /// Получить количество повторных попыток
        /// </summary>
        int GetRetryAttempts();

        /// <summary>
        /// Проверить валидность конфигурации
        /// </summary>
        bool IsConfigurationValid();
    }
}
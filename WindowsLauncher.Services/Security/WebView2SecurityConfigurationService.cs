using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Security;
using WindowsLauncher.Core.Models.Configuration;

namespace WindowsLauncher.Services.Security
{
    /// <summary>
    /// Сервис для управления конфигурацией безопасности WebView2
    /// </summary>
    public class WebView2SecurityConfigurationService : IWebView2SecurityConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebView2SecurityConfigurationService> _logger;
        private WebView2SecurityConfiguration? _cachedConfiguration;

        public WebView2SecurityConfigurationService(
            IConfiguration configuration,
            ILogger<WebView2SecurityConfigurationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Получить текущую конфигурацию безопасности WebView2
        /// </summary>
        public WebView2SecurityConfiguration GetConfiguration()
        {
            if (_cachedConfiguration == null)
            {
                _cachedConfiguration = LoadConfiguration();
            }

            return _cachedConfiguration;
        }

        /// <summary>
        /// Получить эффективную стратегию очистки данных
        /// </summary>
        public DataClearingStrategy GetEffectiveDataClearingStrategy()
        {
            var config = GetConfiguration();
            var effectiveStrategy = config.GetEffectiveStrategy();
            
            _logger.LogTrace("Effective data clearing strategy: {Strategy} (SecureEnvironment: {SecureEnv})", 
                effectiveStrategy, config.SecureEnvironment);
                
            return effectiveStrategy;
        }

        /// <summary>
        /// Проверить, включено ли немедленное очищение cookies
        /// </summary>
        public bool ShouldClearCookiesImmediately()
        {
            var config = GetConfiguration();
            var shouldClear = config.ClearCookiesImmediately || config.SecureEnvironment;
            
            _logger.LogTrace("Should clear cookies immediately: {ShouldClear}", shouldClear);
            return shouldClear;
        }

        /// <summary>
        /// Проверить, должен ли кэш очищаться при выходе
        /// </summary>
        public bool ShouldClearCacheOnExit()
        {
            var config = GetConfiguration();
            return config.ClearCacheOnExit;
        }

        /// <summary>
        /// Включено ли детальное логирование операций очистки
        /// </summary>
        public bool IsAuditLoggingEnabled()
        {
            var config = GetConfiguration();
            return config.EnableAuditLogging;
        }

        /// <summary>
        /// Получить таймаут операций очистки
        /// </summary>
        public int GetCleanupTimeoutMs()
        {
            var config = GetConfiguration();
            return config.CleanupTimeoutMs;
        }

        /// <summary>
        /// Получить количество повторных попыток
        /// </summary>
        public int GetRetryAttempts()
        {
            var config = GetConfiguration();
            return config.RetryAttempts;
        }

        /// <summary>
        /// Проверить валидность конфигурации
        /// </summary>
        public bool IsConfigurationValid()
        {
            var config = GetConfiguration();
            var isValid = config.IsValid();
            
            if (!isValid)
            {
                _logger.LogWarning("WebView2 security configuration is invalid. Using defaults.");
            }
            
            return isValid;
        }

        /// <summary>
        /// Загрузить конфигурацию из appsettings.json с применением значений по умолчанию
        /// </summary>
        private WebView2SecurityConfiguration LoadConfiguration()
        {
            try
            {
                var config = new WebView2SecurityConfiguration();
                
                // Привязать к секции конфигурации
                _configuration.GetSection("WebView2Security").Bind(config);
                
                // Применить значения по умолчанию для невалидных настроек
                if (!config.IsValid())
                {
                    _logger.LogWarning("Invalid WebView2Security configuration detected. Applying defaults.");
                    config.ApplyDefaults();
                }
                
                _logger.LogDebug("WebView2 security configuration loaded: Strategy={Strategy}, SecureEnv={SecureEnv}, AuditLog={AuditLog}",
                    config.DataClearingStrategy, config.SecureEnvironment, config.EnableAuditLogging);
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load WebView2Security configuration. Using defaults.");
                
                // Возвращаем конфигурацию по умолчанию при ошибке
                var defaultConfig = new WebView2SecurityConfiguration();
                defaultConfig.ApplyDefaults();
                return defaultConfig;
            }
        }
    }
}
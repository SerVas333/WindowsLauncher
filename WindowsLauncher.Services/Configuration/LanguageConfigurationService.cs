using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace WindowsLauncher.Services.Configuration
{
    /// <summary>
    /// Конфигурация языка интерфейса
    /// </summary>
    public class LanguageConfiguration
    {
        /// <summary>
        /// Режим определения языка
        /// Auto - автоматически по системе
        /// Manual - вручную по PreferredLanguage
        /// </summary>
        public string Mode { get; set; } = "Auto";

        /// <summary>
        /// Предпочтительный язык (используется в режиме Manual)
        /// </summary>
        public string PreferredLanguage { get; set; } = "en-US";

        /// <summary>
        /// Резервный язык при недоступности предпочтительного
        /// </summary>
        public string FallbackLanguage { get; set; } = "en-US";

        /// <summary>
        /// Список поддерживаемых языков
        /// </summary>
        public string[] SupportedLanguages { get; set; } = { "en-US", "ru-RU" };
    }

    /// <summary>
    /// Сервис для управления конфигурацией языка приложения
    /// </summary>
    public interface ILanguageConfigurationService
    {
        /// <summary>
        /// Инициализировать язык при запуске приложения
        /// </summary>
        Task<string> InitializeLanguageAsync();

        /// <summary>
        /// Получить текущую конфигурацию языка
        /// </summary>
        LanguageConfiguration GetLanguageConfiguration();

        /// <summary>
        /// Определить язык по системным настройкам
        /// </summary>
        string DetectSystemLanguage();

        /// <summary>
        /// Проверить поддержку языка
        /// </summary>
        bool IsLanguageSupported(string languageCode);

        /// <summary>
        /// Получить рекомендуемый язык для установки
        /// </summary>
        string GetRecommendedLanguage();
    }

    /// <summary>
    /// Реализация сервиса управления языком
    /// </summary>
    public class LanguageConfigurationService : ILanguageConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LanguageConfigurationService> _logger;
        private LanguageConfiguration? _languageConfig;

        public LanguageConfigurationService(
            IConfiguration configuration,
            ILogger<LanguageConfigurationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Инициализировать язык при запуске приложения
        /// </summary>
        public async Task<string> InitializeLanguageAsync()
        {
            try
            {
                _logger.LogInformation("Initializing application language configuration");

                var config = GetLanguageConfiguration();
                string targetLanguage;

                switch (config.Mode.ToLowerInvariant())
                {
                    case "auto":
                        targetLanguage = DetectSystemLanguage();
                        _logger.LogInformation("Auto language mode: detected system language '{Language}'", targetLanguage);
                        break;

                    case "manual":
                        targetLanguage = config.PreferredLanguage;
                        _logger.LogInformation("Manual language mode: using preferred language '{Language}'", targetLanguage);
                        break;

                    default:
                        _logger.LogWarning("Unknown language mode '{Mode}', falling back to Auto", config.Mode);
                        targetLanguage = DetectSystemLanguage();
                        break;
                }

                // Проверяем поддержку языка
                if (!IsLanguageSupported(targetLanguage))
                {
                    _logger.LogWarning("Language '{Language}' is not supported, using fallback '{Fallback}'", 
                        targetLanguage, config.FallbackLanguage);
                    targetLanguage = config.FallbackLanguage;
                }

                // Финальная проверка fallback языка
                if (!IsLanguageSupported(targetLanguage))
                {
                    _logger.LogError("Fallback language '{Language}' is not supported, using en-US", targetLanguage);
                    targetLanguage = "en-US";
                }

                _logger.LogInformation("Language initialization completed: '{Language}'", targetLanguage);
                
                await Task.CompletedTask; // Для совместимости с async интерфейсом
                return targetLanguage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing language configuration");
                return "en-US"; // Возвращаем fallback язык
            }
        }

        /// <summary>
        /// Получить текущую конфигурацию языка
        /// </summary>
        public LanguageConfiguration GetLanguageConfiguration()
        {
            if (_languageConfig == null)
            {
                _languageConfig = new LanguageConfiguration();
                _configuration.GetSection("UI:Language").Bind(_languageConfig);

                // Проверяем валидность конфигурации
                if (_languageConfig.SupportedLanguages == null || _languageConfig.SupportedLanguages.Length == 0)
                {
                    _logger.LogWarning("No supported languages configured, using defaults");
                    _languageConfig.SupportedLanguages = new[] { "en-US", "ru-RU" };
                }

                if (string.IsNullOrEmpty(_languageConfig.FallbackLanguage))
                {
                    _languageConfig.FallbackLanguage = "en-US";
                }
            }

            return _languageConfig;
        }

        /// <summary>
        /// Определить язык по системным настройкам
        /// </summary>
        public string DetectSystemLanguage()
        {
            try
            {
                // Получаем язык UI культуры системы
                var systemCulture = CultureInfo.CurrentUICulture;
                var systemLanguage = systemCulture.Name; // например: "ru-RU", "en-US"

                _logger.LogDebug("System UI culture: {Culture}", systemLanguage);

                var config = GetLanguageConfiguration();

                // Проверяем полное совпадение культуры
                if (IsLanguageSupported(systemLanguage))
                {
                    _logger.LogDebug("Found exact match for system language: {Language}", systemLanguage);
                    return systemLanguage;
                }

                // Если полное совпадение не найдено, пробуем двухбуквенный код
                var twoLetterCode = systemCulture.TwoLetterISOLanguageName; // например: "ru", "en"
                _logger.LogDebug("Looking for language by two-letter code: {Code}", twoLetterCode);
                
                foreach (var supportedLang in config.SupportedLanguages)
                {
                    // Проверяем, начинается ли поддерживаемый язык с двухбуквенного кода
                    if (supportedLang.StartsWith(twoLetterCode + "-", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Found supported language by two-letter code: {Language}", supportedLang);
                        return supportedLang;
                    }
                    
                    // Также проверяем случай, когда в конфигурации только двухбуквенный код
                    if (string.Equals(supportedLang, twoLetterCode, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Found supported language by exact two-letter match: {Language}", supportedLang);
                        return supportedLang;
                    }
                }

                _logger.LogWarning("System language '{Language}' (two-letter: {TwoLetter}) not supported, using fallback '{Fallback}'", 
                    systemLanguage, twoLetterCode, config.FallbackLanguage);
                return config.FallbackLanguage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting system language");
                return "en-US"; // Hardcoded fallback
            }
        }

        /// <summary>
        /// Проверить поддержку языка
        /// </summary>
        public bool IsLanguageSupported(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return false;

            var config = GetLanguageConfiguration();
            return config.SupportedLanguages.Contains(languageCode, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Получить рекомендуемый язык для установки
        /// </summary>
        public string GetRecommendedLanguage()
        {
            var config = GetLanguageConfiguration();
            
            switch (config.Mode.ToLowerInvariant())
            {
                case "auto":
                    var systemLanguage = DetectSystemLanguage();
                    return IsLanguageSupported(systemLanguage) ? systemLanguage : config.FallbackLanguage;
                    
                case "manual":
                    return IsLanguageSupported(config.PreferredLanguage) ? config.PreferredLanguage : config.FallbackLanguage;
                    
                default:
                    return config.FallbackLanguage;
            }
        }
    }
}
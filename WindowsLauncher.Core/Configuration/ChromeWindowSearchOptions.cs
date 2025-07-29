namespace WindowsLauncher.Core.Configuration
{
    /// <summary>
    /// Конфигурация для поиска Chrome окон через Windows API
    /// </summary>
    public class ChromeWindowSearchOptions
    {
        /// <summary>
        /// Максимальное количество окон для перечисления (защита от зависания)
        /// </summary>
        public int MaxEnumCount { get; set; } = 30;

        /// <summary>
        /// Максимальное время перечисления окон в секундах
        /// </summary>
        public int MaxEnumTimeSeconds { get; set; } = 5;

        /// <summary>
        /// Максимальная длина заголовка окна
        /// </summary>
        public int MaxTitleLength { get; set; } = 150;

        /// <summary>
        /// Общий timeout для поиска Chrome окон в секундах
        /// </summary>
        public int SearchTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// Максимальное количество окон для диагностической информации
        /// </summary>
        public int MaxDiagnosticWindows { get; set; } = 20;

        /// <summary>
        /// Валидация настроек и установка значений по умолчанию
        /// </summary>
        public void Validate()
        {
            if (MaxEnumCount <= 0 || MaxEnumCount > 1000)
                MaxEnumCount = 30;

            if (MaxEnumTimeSeconds <= 0 || MaxEnumTimeSeconds > 30)
                MaxEnumTimeSeconds = 5;

            if (MaxTitleLength <= 0 || MaxTitleLength > 1000)
                MaxTitleLength = 150;

            if (SearchTimeoutSeconds <= 0 || SearchTimeoutSeconds > 30)
                SearchTimeoutSeconds = 5;

            if (MaxDiagnosticWindows <= 0 || MaxDiagnosticWindows > 100)
                MaxDiagnosticWindows = 20;
        }
    }
}
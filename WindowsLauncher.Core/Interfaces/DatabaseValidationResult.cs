namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Результат валидации конфигурации базы данных
    /// </summary>
    public class DatabaseValidationResult
    {
        /// <summary>
        /// Успешна ли валидация
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Ошибки валидации
        /// </summary>
        public string[] Errors { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Предупреждения валидации
        /// </summary>
        public string[] Warnings { get; set; } = Array.Empty<string>();
    }
}
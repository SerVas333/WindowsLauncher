namespace WindowsLauncher.Core.Services
{
    /// <summary>
    /// Сервис для управления запуском приложения
    /// </summary>
    public interface IApplicationStartupService
    {
        /// <summary>
        /// Определить, какое действие нужно выполнить при запуске
        /// </summary>
        Task<StartupAction> DetermineStartupActionAsync();
        
        /// <summary>
        /// Проверить готовность приложения к работе
        /// </summary>
        Task<bool> IsApplicationReadyAsync();
        
        /// <summary>
        /// Выполнить необходимые миграции и подготовку
        /// </summary>
        Task PrepareApplicationAsync();
    }
    
    /// <summary>
    /// Возможные действия при запуске приложения
    /// </summary>
    public enum StartupAction
    {
        /// <summary>
        /// Показать окно первоначальной настройки
        /// </summary>
        ShowSetup,
        
        /// <summary>
        /// Выполнить миграции БД и показать прогресс
        /// </summary>
        PerformMigrations,
        
        /// <summary>
        /// Сразу показать окно входа - все готово
        /// </summary>
        ShowLogin
    }
    
    /// <summary>
    /// Результат проверки состояния приложения
    /// </summary>
    public class ApplicationStatus
    {
        public bool ConfigurationExists { get; set; }
        public bool DatabaseConfigured { get; set; }
        public bool DatabaseAccessible { get; set; }
        public bool DatabaseVersionCurrent { get; set; }
        public bool AuthenticationConfigured { get; set; }
        public string? CurrentDatabaseVersion { get; set; }
        public string? RequiredDatabaseVersion { get; set; }
        public List<string> Issues { get; set; } = new();
    }
}
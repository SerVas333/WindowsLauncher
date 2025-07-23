using System.Reflection;

namespace WindowsLauncher.Core.Services
{
    /// <summary>
    /// Сервис для работы с версией приложения
    /// </summary>
    public interface IVersionService
    {
        /// <summary>
        /// Получить текущую версию приложения
        /// </summary>
        Version GetCurrentVersion();
        
        /// <summary>
        /// Получить версию в виде строки
        /// </summary>
        string GetVersionString();
        
        /// <summary>
        /// Получить полную информацию о версии
        /// </summary>
        ApplicationVersionInfo GetVersionInfo();
        
        /// <summary>
        /// Сравнить текущую версию с указанной
        /// </summary>
        int CompareVersion(Version version);
        
        /// <summary>
        /// Проверить, требуется ли обновление схемы БД
        /// </summary>
        bool RequiresDatabaseUpdate(string currentDbVersion);
    }
    
    /// <summary>
    /// Информация о версии приложения
    /// </summary>
    public class ApplicationVersionInfo
    {
        public Version Version { get; set; } = new Version(1, 0, 0, 0);
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string Copyright { get; set; } = string.Empty;
        public DateTime BuildDate { get; set; }
        public string Configuration { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Интерфейс для управления версией базы данных
    /// </summary>
    public interface IDatabaseVersionService
    {
        Task<string> GetCurrentDatabaseVersionAsync();
        Task SetDatabaseVersionAsync(string version);
        Task<bool> IsDatabaseUpToDateAsync();
    }
}
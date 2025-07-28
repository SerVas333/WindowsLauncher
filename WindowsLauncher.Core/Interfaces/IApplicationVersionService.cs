using System.Threading.Tasks;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для управления версиями приложения и проверки совместимости с БД
    /// </summary>
    public interface IApplicationVersionService
    {
        /// <summary>
        /// Получить версию приложения
        /// </summary>
        string GetApplicationVersion();
        
        /// <summary>
        /// Получить версию базы данных
        /// </summary>
        Task<string?> GetDatabaseVersionAsync();
        
        /// <summary>
        /// Установить версию базы данных
        /// </summary>
        Task SetDatabaseVersionAsync(string version, string? applicationVersion = null);
        
        /// <summary>
        /// Проверить совместимость версий приложения и БД
        /// </summary>
        Task<bool> IsDatabaseCompatibleAsync();
        
        /// <summary>
        /// Проверить инициализирована ли база данных
        /// </summary>
        Task<bool> IsDatabaseInitializedAsync();
    }
}
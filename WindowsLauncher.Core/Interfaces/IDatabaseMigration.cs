using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для миграций базы данных
    /// </summary>
    public interface IDatabaseMigration
    {
        /// <summary>
        /// Имя миграции
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Версия миграции (в формате YYYYMMDDHHMMSS)
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Описание миграции
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Применить миграцию
        /// </summary>
        Task UpAsync(IDatabaseMigrationContext context, DatabaseType databaseType);
        
        /// <summary>
        /// Откатить миграцию (если поддерживается)
        /// </summary>
        Task DownAsync(IDatabaseMigrationContext context, DatabaseType databaseType);
    }
}
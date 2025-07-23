using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для управления миграциями базы данных
    /// </summary>
    public interface IDatabaseMigrationService
    {
        /// <summary>
        /// Получить список всех миграций
        /// </summary>
        IReadOnlyList<IDatabaseMigration> GetAllMigrations();
        
        /// <summary>
        /// Получить список примененных миграций
        /// </summary>
        Task<IReadOnlyList<string>> GetAppliedMigrationsAsync();
        
        /// <summary>
        /// Получить список ожидающих миграций
        /// </summary>
        Task<IReadOnlyList<IDatabaseMigration>> GetPendingMigrationsAsync();
        
        /// <summary>
        /// Применить все ожидающие миграции
        /// </summary>
        Task MigrateAsync();
        
        /// <summary>
        /// Применить миграцию до определенной версии
        /// </summary>
        Task MigrateToAsync(string targetVersion);
        
        /// <summary>
        /// Проверить актуальность схемы базы данных
        /// </summary>
        Task<bool> IsDatabaseUpToDateAsync();
        
        /// <summary>
        /// Создать таблицу для отслеживания миграций
        /// </summary>
        Task EnsureMigrationTableExistsAsync();
    }
}
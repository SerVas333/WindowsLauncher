using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Контекст для выполнения миграций
    /// </summary>
    public interface IDatabaseMigrationContext
    {
        /// <summary>
        /// Выполнить SQL команду
        /// </summary>
        Task ExecuteSqlAsync(string sql);
        
        /// <summary>
        /// Выполнить SQL команду с возвращением результата
        /// </summary>
        Task<T> ExecuteScalarAsync<T>(string sql);
        
        /// <summary>
        /// Проверить существование таблицы
        /// </summary>
        Task<bool> TableExistsAsync(string tableName);
        
        /// <summary>
        /// Проверить существование колонки
        /// </summary>
        Task<bool> ColumnExistsAsync(string tableName, string columnName);
        
        /// <summary>
        /// Проверить существование индекса
        /// </summary>
        Task<bool> IndexExistsAsync(string indexName);
        
        /// <summary>
        /// Получить тип базы данных
        /// </summary>
        DatabaseType DatabaseType { get; }
    }
}
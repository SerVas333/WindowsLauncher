using System.Threading.Tasks;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Migrations
{
    /// <summary>
    /// Миграция v1.1.0.002 - Обновление категорий приложений
    /// </summary>
    public class UpdateCategories : IDatabaseMigration
    {
        public string Name => "UpdateCategories";
        public string Version => "1.1.0.002";
        public string Description => "Update application categories to new classification system";

        public async Task UpAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Обновляем существующие категории приложений на новые
            
            string timestampExpression = databaseType switch
            {
                DatabaseType.SQLite => "datetime('now')",
                DatabaseType.Firebird => "CURRENT_TIMESTAMP",
                _ => "CURRENT_TIMESTAMP"
            };
            
            // Меняем старую категорию 'Utilities' на 'Утилиты'
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'Утилиты', 
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'Utilities';");

            // Меняем старую категорию 'Web' на 'Приложения' 
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'Приложения',
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'Web';");

            // Меняем старую категорию 'System' на 'Утилиты'
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'Утилиты',
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'System';");

            // Проверяем количество обновленных записей
            var updatedCount = await context.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM APPLICATIONS 
                WHERE CATEGORY IN ('Утилиты', 'Приложения')");

            if (updatedCount == 0)
            {
                throw new System.InvalidOperationException("No applications were updated with new categories");
            }

            // Логируем результат
            System.Diagnostics.Debug.WriteLine($"Updated {updatedCount} applications with new categories");
        }

        public async Task DownAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Откатываем изменения - возвращаем старые категории
            
            string timestampExpression = databaseType switch
            {
                DatabaseType.SQLite => "datetime('now')",
                DatabaseType.Firebird => "CURRENT_TIMESTAMP",
                _ => "CURRENT_TIMESTAMP"
            };
            
            // Меняем 'Утилиты' обратно на 'Utilities' (для non-system приложений)
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'Utilities',
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'Утилиты' AND NAME IN ('Calculator', 'Notepad');");

            // Меняем 'Приложения' обратно на 'Web'
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'Web',
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'Приложения';");

            // Меняем системные приложения с 'Утилиты' обратно на 'System'
            await context.ExecuteSqlAsync($@"
                UPDATE APPLICATIONS 
                SET CATEGORY = 'System',
                    MODIFIED_DATE = {timestampExpression}
                WHERE CATEGORY = 'Утилиты' AND NAME IN ('Control Panel', 'Command Prompt');");
        }
    }
}
using System.Threading.Tasks;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Migrations
{
    /// <summary>
    /// Миграция v1.2.0.001 - добавление поддержки Android APK приложений
    /// Добавляет новый тип приложения Android и расширяет таблицу APPLICATIONS
    /// </summary>
    public class AddAndroidSupport : IDatabaseMigration
    {
        public string Name => "AddAndroidSupport";
        public string Version => "1.2.0.001";
        public string Description => "Add Android APK application support with metadata fields and new application type";

        public async Task UpAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // 1. Проверяем и добавляем новые колонки в таблицу APPLICATIONS
            await ExtendApplicationsTableAsync(context, databaseType);
            
            // 2. Создаем индексы для Android приложений
            await CreateAndroidIndexesAsync(context, databaseType);
        }


        private async Task ExtendApplicationsTableAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Список новых колонок для Android поддержки
            var androidColumns = new[]
            {
                "APK_PACKAGE_NAME",
                "APK_VERSION_CODE", 
                "APK_VERSION_NAME",
                "APK_MIN_SDK",
                "APK_TARGET_SDK",
                "APK_FILE_PATH",
                "APK_FILE_HASH",
                "APK_INSTALL_STATUS"
            };

            // Проверяем и добавляем каждую колонку если она не существует
            foreach (var columnName in androidColumns)
            {
                bool columnExists = await context.ColumnExistsAsync("APPLICATIONS", columnName);
                
                if (!columnExists)
                {
                    await AddColumnToApplicationsAsync(context, databaseType, columnName);
                }
            }
        }

        private async Task AddColumnToApplicationsAsync(IDatabaseMigrationContext context, DatabaseType databaseType, string columnName)
        {
            string alterSql = (databaseType, columnName) switch
            {
                // SQLite колонки
                (DatabaseType.SQLite, "APK_PACKAGE_NAME") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_PACKAGE_NAME TEXT;",
                (DatabaseType.SQLite, "APK_VERSION_CODE") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_CODE INTEGER;",
                (DatabaseType.SQLite, "APK_VERSION_NAME") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_NAME TEXT;",
                (DatabaseType.SQLite, "APK_MIN_SDK") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_MIN_SDK INTEGER;",
                (DatabaseType.SQLite, "APK_TARGET_SDK") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_TARGET_SDK INTEGER;",
                (DatabaseType.SQLite, "APK_FILE_PATH") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_FILE_PATH TEXT;",
                (DatabaseType.SQLite, "APK_FILE_HASH") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_FILE_HASH TEXT;",
                (DatabaseType.SQLite, "APK_INSTALL_STATUS") => "ALTER TABLE APPLICATIONS ADD COLUMN APK_INSTALL_STATUS TEXT DEFAULT 'NotInstalled';",
                
                // Firebird колонки
                (DatabaseType.Firebird, "APK_PACKAGE_NAME") => "ALTER TABLE APPLICATIONS ADD APK_PACKAGE_NAME VARCHAR(255);",
                (DatabaseType.Firebird, "APK_VERSION_CODE") => "ALTER TABLE APPLICATIONS ADD APK_VERSION_CODE INTEGER;",
                (DatabaseType.Firebird, "APK_VERSION_NAME") => "ALTER TABLE APPLICATIONS ADD APK_VERSION_NAME VARCHAR(100);",
                (DatabaseType.Firebird, "APK_MIN_SDK") => "ALTER TABLE APPLICATIONS ADD APK_MIN_SDK INTEGER;",
                (DatabaseType.Firebird, "APK_TARGET_SDK") => "ALTER TABLE APPLICATIONS ADD APK_TARGET_SDK INTEGER;",
                (DatabaseType.Firebird, "APK_FILE_PATH") => "ALTER TABLE APPLICATIONS ADD APK_FILE_PATH VARCHAR(500);",
                (DatabaseType.Firebird, "APK_FILE_HASH") => "ALTER TABLE APPLICATIONS ADD APK_FILE_HASH VARCHAR(64);",
                (DatabaseType.Firebird, "APK_INSTALL_STATUS") => "ALTER TABLE APPLICATIONS ADD APK_INSTALL_STATUS VARCHAR(50) DEFAULT 'NotInstalled';",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} or column {columnName} is not supported")
            };

            await context.ExecuteSqlAsync(alterSql);
        }

        private async Task CreateAndroidIndexesAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Индексы для быстрого поиска Android приложений
            string[] androidIndexes = new[]
            {
                // Индекс по APK package name для быстрого поиска
                "CREATE INDEX IDX_APPLICATIONS_APK_PACKAGE ON APPLICATIONS(APK_PACKAGE_NAME);",
                
                // Композитный индекс для фильтрации Android приложений по типу
                "CREATE INDEX IDX_APPLICATIONS_TYPE_ANDROID ON APPLICATIONS(APP_TYPE, APK_PACKAGE_NAME);",
                
                // Индекс по статусу установки APK
                "CREATE INDEX IDX_APPLICATIONS_APK_STATUS ON APPLICATIONS(APK_INSTALL_STATUS);",
                
                // Композитный индекс для поиска Android приложений требующих обновления
                "CREATE INDEX IDX_APPLICATIONS_APK_UPDATE_CHECK ON APPLICATIONS(APP_TYPE, APK_INSTALL_STATUS, APK_VERSION_CODE);",
                
                // Индекс по пути к APK файлу
                "CREATE INDEX IDX_APPLICATIONS_APK_FILE_PATH ON APPLICATIONS(APK_FILE_PATH);",
                
                // Индекс по хэшу APK файла для проверки целостности
                "CREATE INDEX IDX_APPLICATIONS_APK_FILE_HASH ON APPLICATIONS(APK_FILE_HASH);"
            };

            // Создаем все индексы
            foreach (var indexSql in androidIndexes)
            {
                try
                {
                    await context.ExecuteSqlAsync(indexSql);
                }
                catch (System.Exception ex)
                {
                    // Логируем ошибку, но не прерываем миграцию если индекс уже существует
                    // В реальном коде здесь будет использоваться ILogger
                    System.Console.WriteLine($"Warning: Could not create index: {ex.Message}");
                }
            }
        }


        public async Task DownAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Откат миграции - удаляем добавленные элементы
            
            // 1. Удаляем Android приложения из таблицы APPLICATIONS
            await context.ExecuteSqlAsync("DELETE FROM APPLICATIONS WHERE APP_TYPE = 5;"); // Android = 5

            // 2. Удаляем индексы (если они существуют)
            var indexesToDrop = new[]
            {
                "IDX_APPLICATIONS_APK_PACKAGE",
                "IDX_APPLICATIONS_TYPE_ANDROID", 
                "IDX_APPLICATIONS_APK_STATUS",
                "IDX_APPLICATIONS_APK_UPDATE_CHECK",
                "IDX_APPLICATIONS_APK_FILE_PATH",
                "IDX_APPLICATIONS_APK_FILE_HASH"
            };

            foreach (var indexName in indexesToDrop)
            {
                try
                {
                    string dropIndexSql = databaseType switch
                    {
                        DatabaseType.SQLite => $"DROP INDEX IF EXISTS {indexName};",
                        DatabaseType.Firebird => $"DROP INDEX {indexName};",
                        _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
                    };
                    
                    await context.ExecuteSqlAsync(dropIndexSql);
                }
                catch
                {
                    // Игнорируем ошибки при удалении индексов
                }
            }

            // Примечание: Колонки не удаляем для сохранения целостности данных
            // В production окружении удаление колонок может быть опасным
            // Вместо этого они останутся как NULL значения
        }
    }
}
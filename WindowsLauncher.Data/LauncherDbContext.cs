using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data.Configurations;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Data
{
    public class LauncherDbContext : DbContext
    {
        private readonly IDatabaseConfigurationService? _databaseConfigurationService;

        public LauncherDbContext(DbContextOptions<LauncherDbContext> options) : base(options)
        {
        }

        public LauncherDbContext(DbContextOptions<LauncherDbContext> options, 
            IDatabaseConfigurationService databaseConfigurationService) : base(options)
        {
            _databaseConfigurationService = databaseConfigurationService;
        }

        // DbSets для наших моделей
        public DbSet<User> Users { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && _databaseConfigurationService != null)
            {
                // Получаем конфигурацию асинхронно, но используем синхронную версию для OnConfiguring
                var configuration = GetDatabaseConfigurationSync();
                ConfigureDatabase(optionsBuilder, configuration);
            }
            
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ПРОСТОЕ РЕШЕНИЕ: Все таблицы и колонки в UPPERCASE для всех БД
            // SQLite нечувствителен к регистру, Firebird требует UPPERCASE
            
            // Применяем UNIFORM конфигурации с UPPERCASE именами
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
            modelBuilder.ApplyConfiguration(new UserSettingsConfiguration());
            modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

            // Применяем общие настройки для всех БД
            ApplyUniversalConfiguration(modelBuilder);
        }

        private DatabaseConfiguration GetDatabaseConfigurationSync()
        {
            try
            {
                // В контексте OnConfiguring мы не можем использовать async,
                // поэтому используем синхронную версию или значение по умолчанию
                return _databaseConfigurationService?.GetDefaultConfiguration() 
                    ?? new DatabaseConfiguration();
            }
            catch
            {
                return new DatabaseConfiguration();
            }
        }

        private static void ConfigureDatabase(DbContextOptionsBuilder optionsBuilder, DatabaseConfiguration configuration)
        {
            switch (configuration.DatabaseType)
            {
                case DatabaseType.SQLite:
                    optionsBuilder.UseSqlite(configuration.GetSQLiteConnectionString(), options =>
                    {
                        options.CommandTimeout(configuration.ConnectionTimeout);
                    });
                    break;

                case DatabaseType.Firebird:
                    optionsBuilder.UseFirebird(configuration.GetFirebirdConnectionString(), options =>
                    {
                        options.CommandTimeout(configuration.ConnectionTimeout);
                    });
                    break;

                default:
                    throw new ArgumentException($"Неподдерживаемый тип базы данных: {configuration.DatabaseType}");
            }
        }


        private static void ApplyUniversalConfiguration(ModelBuilder modelBuilder)
        {
            // UNIVERSAL настройки для всех БД - все в UPPERCASE
            // Обеспечиваем что все имена таблиц и колонок в верхнем регистре
            
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Убеждаемся что имена таблиц в UPPERCASE
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName) && tableName != tableName.ToUpper())
                {
                    entityType.SetTableName(tableName.ToUpper());
                }

                // Убеждаемся что имена колонок в UPPERCASE
                foreach (var property in entityType.GetProperties())
                {
                    var columnName = property.GetColumnName();
                    if (!string.IsNullOrEmpty(columnName) && columnName != columnName.ToUpper())
                    {
                        property.SetColumnName(columnName.ToUpper());
                    }
                }

                // Убеждаемся что имена индексов в UPPERCASE
                foreach (var index in entityType.GetIndexes())
                {
                    var indexName = index.GetDatabaseName();
                    if (!string.IsNullOrEmpty(indexName) && indexName != indexName.ToUpper())
                    {
                        index.SetDatabaseName(indexName.ToUpper());
                    }
                }

                // Убеждаемся что имена внешних ключей в UPPERCASE
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    var constraintName = foreignKey.GetConstraintName();
                    if (!string.IsNullOrEmpty(constraintName) && constraintName != constraintName.ToUpper())
                    {
                        foreignKey.SetConstraintName(constraintName.ToUpper());
                    }
                }
            }
        }
    }
}
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

            // Определяем тип базы данных для условных конфигураций
            var databaseType = GetCurrentDatabaseType();

            // Применяем конфигурации с учетом типа БД
            modelBuilder.ApplyConfiguration(new UserConfiguration(databaseType));
            modelBuilder.ApplyConfiguration(new ApplicationConfiguration(databaseType));
            modelBuilder.ApplyConfiguration(new UserSettingsConfiguration(databaseType));
            modelBuilder.ApplyConfiguration(new AuditLogConfiguration(databaseType));

            // Применяем специфичные для БД настройки
            ApplyDatabaseSpecificConfiguration(modelBuilder, databaseType);
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

        private DatabaseType GetCurrentDatabaseType()
        {
            try
            {
                return GetDatabaseConfigurationSync().DatabaseType;
            }
            catch
            {
                return DatabaseType.SQLite; // По умолчанию SQLite
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

        private static void ApplyDatabaseSpecificConfiguration(ModelBuilder modelBuilder, DatabaseType databaseType)
        {
            switch (databaseType)
            {
                case DatabaseType.SQLite:
                    // SQLite-специфичные настройки
                    ApplySQLiteConfiguration(modelBuilder);
                    break;

                case DatabaseType.Firebird:
                    // Firebird-специфичные настройки
                    ApplyFirebirdConfiguration(modelBuilder);
                    break;
            }
        }

        private static void ApplySQLiteConfiguration(ModelBuilder modelBuilder)
        {
            // Для SQLite используем стандартные настройки
            // DateTime сохраняется как TEXT
            // GUID как TEXT
            // Bool как INTEGER (0/1)
        }

        private static void ApplyFirebirdConfiguration(ModelBuilder modelBuilder)
        {
            // Настройки для Firebird
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Настройка имен таблиц (Firebird любит UPPERCASE)
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    entityType.SetTableName(tableName.ToUpper());
                }

                // Настройка имен колонок
                foreach (var property in entityType.GetProperties())
                {
                    var columnName = property.GetColumnName();
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        property.SetColumnName(columnName.ToUpper());
                    }

                    // Настройка типов данных для Firebird
                    var clrType = property.ClrType;
                    var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

                    if (underlyingType == typeof(string))
                    {
                        // Для строк используем VARCHAR с максимальной длиной или BLOB SUB_TYPE TEXT
                        var maxLength = property.GetMaxLength();
                        if (maxLength == null || maxLength > 8191)
                        {
                            property.SetColumnType("BLOB SUB_TYPE TEXT");
                        }
                        else
                        {
                            property.SetColumnType($"VARCHAR({maxLength})");
                        }
                    }
                    else if (underlyingType == typeof(DateTime))
                    {
                        property.SetColumnType("TIMESTAMP");
                    }
                    else if (underlyingType == typeof(bool))
                    {
                        property.SetColumnType("SMALLINT");
                    }
                    else if (underlyingType == typeof(Guid))
                    {
                        property.SetColumnType("CHAR(36)");
                    }
                    else if (underlyingType == typeof(long))
                    {
                        property.SetColumnType("BIGINT");
                    }
                    else if (underlyingType == typeof(int))
                    {
                        property.SetColumnType("INTEGER");
                    }
                    else if (underlyingType == typeof(decimal))
                    {
                        property.SetColumnType("DECIMAL(18,2)");
                    }
                    else if (underlyingType == typeof(double))
                    {
                        property.SetColumnType("DOUBLE PRECISION");
                    }
                    else if (underlyingType == typeof(float))
                    {
                        property.SetColumnType("FLOAT");
                    }
                }

                // Настройка индексов
                foreach (var index in entityType.GetIndexes())
                {
                    var indexName = index.GetDatabaseName();
                    if (!string.IsNullOrEmpty(indexName))
                    {
                        index.SetDatabaseName(indexName.ToUpper());
                    }
                }

                // Настройка внешних ключей
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    var constraintName = foreignKey.GetConstraintName();
                    if (!string.IsNullOrEmpty(constraintName))
                    {
                        foreignKey.SetConstraintName(constraintName.ToUpper());
                    }
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Services;
using WindowsLauncher.Data.Migrations;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления миграциями базы данных
    /// </summary>
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly LauncherDbContext _context;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly ILogger<DatabaseMigrationService> _logger;
        private readonly IApplicationVersionService _versionService;
        private readonly List<IDatabaseMigration> _migrations;
        
        public DatabaseMigrationService(
            LauncherDbContext context,
            IDatabaseConfigurationService dbConfigService,
            ILogger<DatabaseMigrationService> logger,
            IApplicationVersionService versionService)
        {
            _context = context;
            _dbConfigService = dbConfigService;
            _logger = logger;
            _versionService = versionService;
            
            // Список всех миграций по порядку версий
            _migrations = new List<IDatabaseMigration>
            {
                new InitialSchema(),        // v1.0.0.001
                new AddEmailSupport(),      // v1.1.0.001
                //new UpdateCategories(),     // v1.1.0.002
                new AddAndroidSupport()     // v1.2.0.001
            };
        }
        
        public IReadOnlyList<IDatabaseMigration> GetAllMigrations()
        {
            return _migrations.OrderBy(m => m.Version).ToList();
        }
        
        public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync()
        {
            if (!await CheckMigrationTableExistsAsync())
            {
                return new List<string>(); // Таблица не существует - возвращаем пустой список
            }
            
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            var sql = config.DatabaseType switch
            {
                DatabaseType.SQLite => "SELECT VERSION FROM MIGRATION_HISTORY ORDER BY VERSION",
                DatabaseType.Firebird => "SELECT VERSION FROM MIGRATION_HISTORY ORDER BY VERSION",
                _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
            };
            
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                
                if (command.Connection?.State != System.Data.ConnectionState.Open)
                {
                    await command.Connection!.OpenAsync();
                }
                
                var appliedMigrations = new List<string>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    appliedMigrations.Add(reader.GetString(0));
                }
                
                return appliedMigrations;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get applied migrations, assuming empty");
                return new List<string>();
            }
        }
        
        public async Task<IReadOnlyList<IDatabaseMigration>> GetPendingMigrationsAsync()
        {
            var allMigrations = GetAllMigrations();
            var appliedMigrations = await GetAppliedMigrationsAsync();
            
            return allMigrations
                .Where(m => !appliedMigrations.Contains(m.Version))
                .OrderBy(m => m.Version)
                .ToList();
        }
        
        public async Task MigrateAsync()
        {
            var pendingMigrations = await GetPendingMigrationsAsync();
            
            if (pendingMigrations.Count == 0)
            {
                _logger.LogInformation("No pending migrations found");
                return;
            }
            
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            // Проверяем что таблица миграций существует (создается в InitialSchema)
            if (!await CheckMigrationTableExistsAsync())
            {
                _logger.LogWarning("Migration table does not exist, this should only happen on first run before InitialSchema");
            }
            
            string? latestVersion = null;
            
            foreach (var migration in pendingMigrations)
            {
                _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);
                
                try
                {
                    await migration.UpAsync(migrationContext, config.DatabaseType);
                    await RecordMigrationAsync(migration, config.DatabaseType);
                    latestVersion = migration.Version;
                    
                    _logger.LogInformation("Successfully applied migration {Version}", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply migration {Version}: {Name}", migration.Version, migration.Name);
                    throw;
                }
            }
            
            // Обновляем текущую версию БД до последней применённой миграции
            if (!string.IsNullOrEmpty(latestVersion))
            {
                await _versionService.SetDatabaseVersionAsync(latestVersion);
                _logger.LogInformation("Updated database version to {Version}", latestVersion);
            }
            
            _logger.LogInformation("Applied {Count} migrations successfully", pendingMigrations.Count);
        }
        
        public async Task MigrateToAsync(string targetVersion)
        {
            var allMigrations = GetAllMigrations();
            var appliedMigrations = await GetAppliedMigrationsAsync();
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            // Проверяем что таблица миграций существует (создается в InitialSchema)
            if (!await CheckMigrationTableExistsAsync())
            {
                _logger.LogWarning("Migration table does not exist, this should only happen on first run before InitialSchema");
            }
            
            var targetMigrations = allMigrations
                .Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) <= 0)
                .Where(m => !appliedMigrations.Contains(m.Version))
                .OrderBy(m => m.Version)
                .ToList();
            
            string? latestVersion = null;
            
            foreach (var migration in targetMigrations)
            {
                _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);
                
                await migration.UpAsync(migrationContext, config.DatabaseType);
                await RecordMigrationAsync(migration, config.DatabaseType);
                latestVersion = migration.Version;
            }
            
            // Обновляем текущую версию БД до последней применённой миграции
            if (!string.IsNullOrEmpty(latestVersion))
            {
                await _versionService.SetDatabaseVersionAsync(latestVersion);
                _logger.LogInformation("Updated database version to {Version}", latestVersion);
            }
            
            _logger.LogInformation("Migrated to version {Version}", targetVersion);
        }
        
        public async Task<bool> IsDatabaseUpToDateAsync()
        {
            var pendingMigrations = await GetPendingMigrationsAsync();
            return pendingMigrations.Count == 0;
        }
        
        /// <summary>
        /// Проверяет существование таблицы истории миграций
        /// ПРИМЕЧАНИЕ: Таблица создается в InitialSchema миграции, а не здесь
        /// </summary>
        /// <returns>true если таблица существует</returns>
        private async Task<bool> CheckMigrationTableExistsAsync()
        {
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            var tableName = config.DatabaseType switch
            {
                DatabaseType.SQLite => "MIGRATION_HISTORY",
                DatabaseType.Firebird => "MIGRATION_HISTORY",
                _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
            };
            
            return await migrationContext.TableExistsAsync(tableName);
        }
        
        private async Task RecordMigrationAsync(IDatabaseMigration migration, DatabaseType databaseType)
        {
            // Используем полную схему таблицы с ID и ROLLBACK_SCRIPT колонками (как в InitialSchema)
            var sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT, ROLLBACK_SCRIPT)
                    VALUES (@p0, @p1, @p2, @p3, @p4)",
                DatabaseType.Firebird => @"
                    INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT, ROLLBACK_SCRIPT)
                    VALUES (@p0, @p1, @p2, @p3, @p4)",
                _ => throw new NotSupportedException($"Database type {databaseType} not supported")
            };
            
            object appliedAt = databaseType switch
            {
                DatabaseType.SQLite => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                DatabaseType.Firebird => DateTime.UtcNow,
                _ => (object)DateTime.UtcNow
            };
            
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var p0 = command.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = migration.Version;
            command.Parameters.Add(p0);
            
            var p1 = command.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = migration.Name;
            command.Parameters.Add(p1);
            
            var p2 = command.CreateParameter();
            p2.ParameterName = "@p2";
            p2.Value = migration.Description;
            command.Parameters.Add(p2);
            
            var p3 = command.CreateParameter();
            p3.ParameterName = "@p3";
            p3.Value = appliedAt;
            command.Parameters.Add(p3);
            
            var p4 = command.CreateParameter();
            p4.ParameterName = "@p4";
            p4.Value = (object?)null ?? DBNull.Value; // ROLLBACK_SCRIPT пока null (future feature)
            command.Parameters.Add(p4);
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection!.OpenAsync();
            }
            
            await command.ExecuteNonQueryAsync();
        }
    }
}
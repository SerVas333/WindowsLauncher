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
        private readonly List<IDatabaseMigration> _migrations;
        
        public DatabaseMigrationService(
            LauncherDbContext context,
            IDatabaseConfigurationService dbConfigService,
            ILogger<DatabaseMigrationService> logger)
        {
            _context = context;
            _dbConfigService = dbConfigService;
            _logger = logger;
            
            // Список миграций (будет заполняться по мере развития приложения)
            _migrations = new List<IDatabaseMigration>();
        }
        
        public IReadOnlyList<IDatabaseMigration> GetAllMigrations()
        {
            return _migrations.OrderBy(m => m.Version).ToList();
        }
        
        public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync()
        {
            await EnsureMigrationTableExistsAsync();
            
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            var sql = config.DatabaseType switch
            {
                DatabaseType.SQLite => "SELECT Version FROM __MigrationHistory ORDER BY Version",
                DatabaseType.Firebird => "SELECT VERSION FROM MIGRATION_HISTORY ORDER BY VERSION",
                _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
            };
            
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                
                if (command.Connection?.State != System.Data.ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
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
            
            await EnsureMigrationTableExistsAsync();
            
            foreach (var migration in pendingMigrations)
            {
                _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);
                
                try
                {
                    await migration.UpAsync(migrationContext, config.DatabaseType);
                    await RecordMigrationAsync(migration, config.DatabaseType);
                    
                    _logger.LogInformation("Successfully applied migration {Version}", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply migration {Version}: {Name}", migration.Version, migration.Name);
                    throw;
                }
            }
            
            _logger.LogInformation("Applied {Count} migrations successfully", pendingMigrations.Count);
        }
        
        public async Task MigrateToAsync(string targetVersion)
        {
            var allMigrations = GetAllMigrations();
            var appliedMigrations = await GetAppliedMigrationsAsync();
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            await EnsureMigrationTableExistsAsync();
            
            var targetMigrations = allMigrations
                .Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) <= 0)
                .Where(m => !appliedMigrations.Contains(m.Version))
                .OrderBy(m => m.Version)
                .ToList();
            
            foreach (var migration in targetMigrations)
            {
                _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);
                
                await migration.UpAsync(migrationContext, config.DatabaseType);
                await RecordMigrationAsync(migration, config.DatabaseType);
            }
            
            _logger.LogInformation("Migrated to version {Version}", targetVersion);
        }
        
        public async Task<bool> IsDatabaseUpToDateAsync()
        {
            var pendingMigrations = await GetPendingMigrationsAsync();
            return pendingMigrations.Count == 0;
        }
        
        public async Task EnsureMigrationTableExistsAsync()
        {
            var config = await _dbConfigService.GetConfigurationAsync();
            var migrationContext = new DatabaseMigrationContext(_context, config.DatabaseType);
            
            var tableName = config.DatabaseType switch
            {
                DatabaseType.SQLite => "__MigrationHistory",
                DatabaseType.Firebird => "MIGRATION_HISTORY",
                _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
            };
            
            if (await migrationContext.TableExistsAsync(tableName))
            {
                return;
            }
            
            var createTableSql = config.DatabaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE IF NOT EXISTS __MigrationHistory (
                        Version TEXT PRIMARY KEY NOT NULL,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        AppliedAt TEXT NOT NULL
                    )",
                DatabaseType.Firebird => @"
                    CREATE TABLE MIGRATION_HISTORY (
                        VERSION VARCHAR(50) PRIMARY KEY NOT NULL,
                        NAME VARCHAR(255) NOT NULL,
                        DESCRIPTION VARCHAR(1000),
                        APPLIED_AT TIMESTAMP NOT NULL
                    )",
                _ => throw new NotSupportedException($"Database type {config.DatabaseType} not supported")
            };
            
            try
            {
                await migrationContext.ExecuteSqlAsync(createTableSql);
                _logger.LogInformation("Created migration history table for {DatabaseType}", config.DatabaseType);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1 && config.DatabaseType == DatabaseType.SQLite)
            {
                // Table already exists - this is expected in concurrent scenarios
                _logger.LogDebug("Migration history table already exists for {DatabaseType} (concurrent creation)", config.DatabaseType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create migration history table for {DatabaseType}", config.DatabaseType);
                throw;
            }
        }
        
        private async Task RecordMigrationAsync(IDatabaseMigration migration, DatabaseType databaseType)
        {
            var sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    INSERT INTO __MigrationHistory (Version, Name, Description, AppliedAt)
                    VALUES (@p0, @p1, @p2, @p3)",
                DatabaseType.Firebird => @"
                    INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
                    VALUES (@p0, @p1, @p2, @p3)",
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
            
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            
            await command.ExecuteNonQueryAsync();
        }
    }
}
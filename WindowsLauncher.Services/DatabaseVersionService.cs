using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Services;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления версией базы данных
    /// </summary>
    public class DatabaseVersionService : IDatabaseVersionService
    {
        private readonly LauncherDbContext _context;
        private readonly IVersionService _versionService;
        private readonly ILogger<DatabaseVersionService> _logger;
        private readonly IDatabaseConfigurationService? _dbConfigService;
        
        public DatabaseVersionService(
            LauncherDbContext context,
            IVersionService versionService,
            ILogger<DatabaseVersionService> logger,
            IDatabaseConfigurationService? dbConfigService = null)
        {
            _context = context;
            _versionService = versionService;
            _logger = logger;
            _dbConfigService = dbConfigService;
        }
        
        public async Task<string> GetCurrentDatabaseVersionAsync()
        {
            try
            {
                // Определяем тип БД
                var config = _dbConfigService != null ? await _dbConfigService.GetConfigurationAsync() : null;
                var isFirebird = config?.DatabaseType == DatabaseType.Firebird;
                
                string tableName = isFirebird ? "DATABASE_VERSION" : "__DatabaseVersion";
                string versionColumn = isFirebird ? "VERSION_NUMBER" : "Version";
                string appliedColumn = isFirebird ? "APPLIED_AT" : "AppliedAt";
                
                // Проверяем наличие таблицы версий
                var versionTableExists = isFirebird 
                    ? await FirebirdTableExistsAsync(tableName)
                    : await TableExistsAsync(tableName);
                    
                if (!versionTableExists)
                {
                    return "0.0.0.0";
                }
                
                // Получаем последнюю версию через ExecuteScalar
                using var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                using var command = connection.CreateCommand();
                // Разный синтаксис для разных БД
                if (isFirebird)
                {
                    command.CommandText = $"SELECT FIRST 1 {versionColumn} FROM {tableName} ORDER BY {appliedColumn} DESC";
                }
                else
                {
                    command.CommandText = $"SELECT {versionColumn} FROM {tableName} ORDER BY {appliedColumn} DESC LIMIT 1";
                }
                
                var result = await command.ExecuteScalarAsync();
                var version = result?.ToString();
                
                return version ?? "0.0.0.0";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get database version, assuming 0.0.0.0");
                return "0.0.0.0";
            }
        }
        
        public async Task SetDatabaseVersionAsync(string version)
        {
            try
            {
                // Создаем таблицу версий если не существует
                await EnsureVersionTableExistsAsync();
                
                // Определяем тип БД
                var config = _dbConfigService != null ? await _dbConfigService.GetConfigurationAsync() : null;
                var isFirebird = config?.DatabaseType == DatabaseType.Firebird;
                
                string sql;
                object[] parameters;
                
                if (isFirebird)
                {
                    // Для Firebird используем INSERT OR UPDATE (MERGE)
                    sql = @"
                        UPDATE OR INSERT INTO DATABASE_VERSION (VERSION_NUMBER, APPLIED_AT, DESCRIPTION)
                        VALUES (?, ?, ?)
                        MATCHING (VERSION_NUMBER)";
                    parameters = new object[] 
                    {
                        version,
                        DateTime.UtcNow,
                        $"Updated by application version {_versionService.GetVersionString()}"
                    };
                }
                else
                {
                    // Для SQLite используем обычный INSERT
                    sql = @"
                        INSERT INTO __DatabaseVersion (Version, AppliedAt, ApplicationVersion) 
                        VALUES (@p0, @p1, @p2)";
                    parameters = new object[]
                    {
                        version,
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        _versionService.GetVersionString()
                    };
                }
                    
                await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                    
                _logger.LogInformation("Database version updated to {Version} (DB type: {DbType})", version, isFirebird ? "Firebird" : "SQLite");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set database version to {Version}", version);
                throw;
            }
        }
        
        public async Task<bool> IsDatabaseUpToDateAsync()
        {
            var dbVersion = await GetCurrentDatabaseVersionAsync();
            var appVersion = _versionService.GetVersionString();
            
            if (!Version.TryParse(dbVersion, out var db) || !Version.TryParse(appVersion, out var app))
            {
                return false;
            }
            
            // БД актуальна если версии совпадают или версия БД новее
            return db >= app;
        }
        
        private async Task<bool> TableExistsAsync(string tableName)
        {
            try
            {
                // Используем ExecuteSqlRaw для создания соединения и команды вручную
                using var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @tableName";
                
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);
                
                var result = await command.ExecuteScalarAsync();
                var count = Convert.ToInt32(result);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task EnsureVersionTableExistsAsync()
        {
            try
            {
                // Определяем тип БД
                var config = _dbConfigService != null ? await _dbConfigService.GetConfigurationAsync() : null;
                var isFirebird = config?.DatabaseType == DatabaseType.Firebird;
                
                if (isFirebird)
                {
                    // Для Firebird НЕ создаем таблицу автоматически
                    // Таблица DATABASE_VERSION должна быть создана через create_database.sql
                    _logger.LogInformation("Firebird detected - DATABASE_VERSION table should exist from create_database.sql");
                    
                    // Проверяем что таблица существует
                    var tableExists = await FirebirdTableExistsAsync("DATABASE_VERSION");
                    if (!tableExists)
                    {
                        _logger.LogWarning("DATABASE_VERSION table not found in Firebird - please run create_database.sql");
                        throw new InvalidOperationException("DATABASE_VERSION table missing - run create_database.sql first");
                    }
                    
                    _logger.LogDebug("Firebird DATABASE_VERSION table verified");
                }
                else
                {
                    // Для SQLite создаем таблицу автоматически
                    var sql = @"
                        CREATE TABLE IF NOT EXISTS __DatabaseVersion (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Version TEXT NOT NULL,
                            AppliedAt TEXT NOT NULL,
                            ApplicationVersion TEXT NOT NULL
                        )";
                        
                    await _context.Database.ExecuteSqlRawAsync(sql);
                    _logger.LogDebug("Ensured SQLite __DatabaseVersion table exists");
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Table already exists - this is expected in concurrent scenarios
                _logger.LogDebug("Table __DatabaseVersion already exists (concurrent creation)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure version table exists: {Error}", ex.Message);
                throw;
            }
        }
        
        private async Task<bool> FirebirdTableExistsAsync(string tableName)
        {
            try
            {
                using var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = @tableName";
                
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName.ToUpper();
                command.Parameters.Add(parameter);
                
                var result = await command.ExecuteScalarAsync();
                return result != null && Convert.ToInt32(result) > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления версиями приложения и проверки совместимости с БД
    /// </summary>
    public class ApplicationVersionService : IApplicationVersionService
    {
        private readonly LauncherDbContext _context;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly ILogger<ApplicationVersionService> _logger;
        
        public ApplicationVersionService(
            LauncherDbContext context,
            IDatabaseConfigurationService dbConfigService,
            ILogger<ApplicationVersionService> logger)
        {
            _context = context;
            _dbConfigService = dbConfigService;
            _logger = logger;
        }
        
        public string GetApplicationVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }
        
        public async Task<string?> GetDatabaseVersionAsync()
        {
            try
            {
                // Проверяем существование таблицы DATABASE_VERSION
                var config = await _dbConfigService.GetConfigurationAsync();
                string checkTableSql = config.DatabaseType switch
                {
                    DatabaseType.SQLite => "SELECT name FROM sqlite_master WHERE type='table' AND name='DATABASE_VERSION';",
                    DatabaseType.Firebird => "SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'DATABASE_VERSION';",
                    _ => throw new NotSupportedException($"Database type {config.DatabaseType} is not supported")
                };

                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = checkTableSql;
                var tableExists = await command.ExecuteScalarAsync();

                if (tableExists == null)
                {
                    return null; // Таблица не существует - БД не инициализирована
                }

                // Получаем текущую версию БД
                string versionSql = config.DatabaseType switch
                {
                    DatabaseType.SQLite => "SELECT VERSION FROM DATABASE_VERSION ORDER BY APPLIED_AT DESC LIMIT 1;",
                    DatabaseType.Firebird => "SELECT FIRST 1 VERSION FROM DATABASE_VERSION ORDER BY APPLIED_AT DESC;",
                    _ => throw new NotSupportedException($"Database type {config.DatabaseType} is not supported")
                };
                command.CommandText = versionSql;
                var result = await command.ExecuteScalarAsync();
                
                return result?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get database version");
                return null;
            }
        }
        
        public async Task SetDatabaseVersionAsync(string version, string? applicationVersion = null)
        {
            try
            {
                var config = await _dbConfigService.GetConfigurationAsync();
                string timestampValue = config.DatabaseType switch
                {
                    DatabaseType.SQLite => "datetime('now')",
                    DatabaseType.Firebird => "CURRENT_TIMESTAMP",
                    _ => "CURRENT_TIMESTAMP"
                };

                applicationVersion ??= GetApplicationVersion();

                // Удаляем старую запись и добавляем новую
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM DATABASE_VERSION");
                
                // Используем параметризованный запрос для предотвращения SQL injection
                await _context.Database.ExecuteSqlAsync($@"
                    INSERT INTO DATABASE_VERSION (VERSION, APPLIED_AT, APPLICATION_VERSION) 
                    VALUES ({version}, {timestampValue}, {applicationVersion})");

                _logger.LogInformation("Database version updated to {Version} for application {AppVersion}", 
                    version, applicationVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set database version to {Version}", version);
                throw;
            }
        }
        
        public async Task<bool> IsDatabaseCompatibleAsync()
        {
            try
            {
                var dbVersion = await GetDatabaseVersionAsync();
                if (dbVersion == null)
                {
                    _logger.LogWarning("Database version is null - database may not be initialized");
                    return false;
                }

                var appVersion = GetApplicationVersion();
                
                // Проверяем совместимость версий
                // Пока простая проверка: версии должны начинаться с одинакового мажорного номера
                var dbMajor = GetMajorVersion(dbVersion);
                var appMajor = GetMajorVersion(appVersion);
                
                bool compatible = dbMajor == appMajor;
                
                _logger.LogInformation("Compatibility check: DB version {DbVersion}, App version {AppVersion}, Compatible: {Compatible}",
                    dbVersion, appVersion, compatible);
                
                return compatible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check database compatibility");
                return false;
            }
        }
        
        public async Task<bool> IsDatabaseInitializedAsync()
        {
            var dbVersion = await GetDatabaseVersionAsync();
            return !string.IsNullOrEmpty(dbVersion);
        }
        
        private static int GetMajorVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return 0;
                
            var parts = version.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out int major))
            {
                return major;
            }
            
            return 0;
        }
    }
}
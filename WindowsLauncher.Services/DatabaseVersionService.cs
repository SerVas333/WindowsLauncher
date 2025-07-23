using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Services;
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
        
        public DatabaseVersionService(
            LauncherDbContext context,
            IVersionService versionService,
            ILogger<DatabaseVersionService> logger)
        {
            _context = context;
            _versionService = versionService;
            _logger = logger;
        }
        
        public async Task<string> GetCurrentDatabaseVersionAsync()
        {
            try
            {
                // Проверяем наличие таблицы версий
                var versionTableExists = await TableExistsAsync("__DatabaseVersion");
                if (!versionTableExists)
                {
                    return "0.0.0.0";
                }
                
                // Получаем последнюю версию
                var sql = "SELECT Version FROM __DatabaseVersion ORDER BY AppliedAt DESC LIMIT 1";
                var version = await _context.Database.SqlQueryRaw<string>(sql).FirstOrDefaultAsync();
                
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
                
                // Записываем новую версию
                var sql = @"
                    INSERT INTO __DatabaseVersion (Version, AppliedAt, ApplicationVersion) 
                    VALUES (@p0, @p1, @p2)";
                    
                await _context.Database.ExecuteSqlRawAsync(sql, 
                    version, 
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    _versionService.GetVersionString());
                    
                _logger.LogInformation("Database version updated to {Version}", version);
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
                var sql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @p0";
                var count = await _context.Database.SqlQueryRaw<int>(sql, tableName).FirstAsync();
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
                var sql = @"
                    CREATE TABLE IF NOT EXISTS __DatabaseVersion (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Version TEXT NOT NULL,
                        AppliedAt TEXT NOT NULL,
                        ApplicationVersion TEXT NOT NULL
                    )";
                    
                await _context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogDebug("Ensured __DatabaseVersion table exists");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Table already exists - this is expected in concurrent scenarios
                _logger.LogDebug("Table __DatabaseVersion already exists (concurrent creation)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure __DatabaseVersion table exists");
                throw;
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для обновления схемы БД при необходимости
    /// </summary>
    public class SchemaUpdateService
    {
        private readonly LauncherDbContext _context;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly ILogger<SchemaUpdateService> _logger;

        public SchemaUpdateService(
            LauncherDbContext context,
            IDatabaseConfigurationService dbConfigService,
            ILogger<SchemaUpdateService> logger)
        {
            _context = context;
            _dbConfigService = dbConfigService;
            _logger = logger;
        }

        /// <summary>
        /// Проверить и обновить схему БД если необходимо
        /// </summary>
        public async Task EnsureSchemaUpToDateAsync()
        {
            try
            {
                _logger.LogInformation("Checking database schema...");

                var config = await _dbConfigService.GetConfigurationAsync();
                
                // Проверяем наличие колонки ICONTEXT
                var hasIconTextColumn = await CheckIconTextColumnExistsAsync(config.DatabaseType);
                
                if (!hasIconTextColumn)
                {
                    _logger.LogInformation("Adding missing ICONTEXT column to APPLICATIONS table");
                    await AddIconTextColumnAsync(config.DatabaseType);
                    _logger.LogInformation("ICONTEXT column added successfully");
                }
                else
                {
                    _logger.LogDebug("ICONTEXT column already exists");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update database schema");
                throw;
            }
        }

        private async Task<bool> CheckIconTextColumnExistsAsync(DatabaseType databaseType)
        {
            try
            {
                string checkSql = databaseType switch
                {
                    DatabaseType.SQLite => @"
                        SELECT COUNT(*) 
                        FROM pragma_table_info('APPLICATIONS') 
                        WHERE name = 'ICONTEXT'",
                    
                    DatabaseType.Firebird => @"
                        SELECT COUNT(*) 
                        FROM RDB$RELATION_FIELDS 
                        WHERE RDB$RELATION_NAME = 'APPLICATIONS' 
                        AND RDB$FIELD_NAME = 'ICONTEXT'",
                    
                    _ => throw new NotSupportedException($"Database type {databaseType} is not supported")
                };

                var result = await _context.Database.ExecuteSqlRawAsync($"SELECT 1 FROM ({checkSql}) t WHERE t.count > 0");
                return true; // Если запрос выполнился без ошибки, колонка существует
            }
            catch
            {
                // Если запрос упал, скорее всего колонки нет
                return false;
            }
        }

        private async Task AddIconTextColumnAsync(DatabaseType databaseType)
        {
            string addColumnSql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    ALTER TABLE APPLICATIONS 
                    ADD COLUMN ICONTEXT TEXT DEFAULT '📱'",
                
                DatabaseType.Firebird => @"
                    ALTER TABLE APPLICATIONS 
                    ADD ICONTEXT VARCHAR(50) DEFAULT '📱'",
                
                _ => throw new NotSupportedException($"Database type {databaseType} is not supported")
            };

            await _context.Database.ExecuteSqlRawAsync(addColumnSql);
        }
    }
}
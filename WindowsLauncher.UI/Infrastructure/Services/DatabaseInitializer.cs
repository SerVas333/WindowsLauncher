// ===== WindowsLauncher.UI/Infrastructure/Services/DatabaseInitializer.cs - ПРОСТОЕ ИСПРАВЛЕНИЕ =====
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Extensions;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Services;
using WindowsLauncher.Services;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Исправленная инициализация базы данных
    /// </summary>
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly LauncherDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;
        private readonly IDatabaseMigrationService? _migrationService;
        private readonly IDatabaseConfigurationService? _dbConfigService;
        private readonly IApplicationVersionService? _applicationVersionService;

        public DatabaseInitializer(
            LauncherDbContext context, 
            ILogger<DatabaseInitializer> logger,
            IDatabaseMigrationService? migrationService = null,
            IDatabaseConfigurationService? dbConfigService = null,
            IApplicationVersionService? applicationVersionService = null)
        {
            _context = context;
            _logger = logger;
            _migrationService = migrationService;
            _dbConfigService = dbConfigService;
            _applicationVersionService = applicationVersionService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("=== STARTING DATABASE INITIALIZATION ===");

                // Сначала убеждаемся что база данных физически существует
                if (_dbConfigService != null)
                {
                    var config = await _dbConfigService.GetConfigurationAsync();
                    _logger.LogInformation("Database configuration loaded: Type={DatabaseType}, Path={Path}, Mode={Mode}", 
                        config.DatabaseType, config.DatabasePath, config.ConnectionMode);
                    
                    _logger.LogInformation("Ensuring database exists...");
                    var dbExists = await _dbConfigService.EnsureDatabaseExistsAsync(config);
                    if (!dbExists)
                    {
                        throw new InvalidOperationException("Failed to ensure database exists");
                    }
                    _logger.LogInformation("Database file/connection ensured successfully");
                }

                // Проверяем возможность подключения с таймаутом
                _logger.LogInformation("Testing database connection...");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                bool canConnect = false;
                try
                {
                    // Получаем конфигурацию для проверки типа БД
                    var config = _dbConfigService != null ? await _dbConfigService.GetConfigurationAsync() : null;
                    
                    if (config?.DatabaseType == Core.Models.DatabaseType.Firebird)
                    {
                        // Для Firebird тестируем подключение вручную, без EF Core
                        _logger.LogInformation("Testing Firebird connection manually...");
                        var connection = _context.Database.GetDbConnection();
                        try
                        {
                            await connection.OpenAsync(cts.Token);
                            canConnect = connection.State == System.Data.ConnectionState.Open;
                            _logger.LogInformation($"Firebird manual connection test: {canConnect}");
                        }
                        catch (Exception fbEx)
                        {
                            _logger.LogWarning(fbEx, "Firebird manual connection failed");
                            canConnect = false;
                        }
                        finally
                        {
                            if (connection.State == System.Data.ConnectionState.Open)
                            {
                                await connection.CloseAsync();
                            }
                        }
                    }
                    else
                    {
                        // Для SQLite используем стандартный EF Core метод
                        canConnect = await _context.Database.CanConnectAsync(cts.Token);
                    }
                    
                    _logger.LogInformation($"Database connection check result: {canConnect}");
                }
                catch (Exception connEx)
                {
                    _logger.LogWarning(connEx, "Database connection test failed");
                    canConnect = false;
                }

                // Проверяем состояние БД с новой системой версионирования
                if (_applicationVersionService != null)
                {
                    var isInitialized = await _applicationVersionService.IsDatabaseInitializedAsync();
                    
                    if (!isInitialized)
                    {
                        _logger.LogInformation("Database not initialized, running initial migration...");
                        
                        // Применяем базовую миграцию
                        if (_migrationService != null)
                        {
                            await _migrationService.MigrateAsync();
                            _logger.LogInformation("Initial migration completed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Migration service not available, using fallback creation");
                            await _context.Database.EnsureCreatedAsync();
                            await _applicationVersionService.SetDatabaseVersionAsync("1.0.0.001");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Database already initialized, checking compatibility...");
                        
                        var isCompatible = await _applicationVersionService.IsDatabaseCompatibleAsync();
                        if (!isCompatible)
                        {
                            var dbVersion = await _applicationVersionService.GetDatabaseVersionAsync();
                            var appVersion = _applicationVersionService.GetApplicationVersion();
                            
                            _logger.LogWarning("Database version {DbVersion} is not compatible with application version {AppVersion}", 
                                dbVersion, appVersion);
                                
                            // В продакшене здесь можно добавить логику обновления БД
                            // Пока просто продолжаем с предупреждением
                        }
                        
                        // Применяем новые миграции если есть
                        if (_migrationService != null)
                        {
                            try
                            {
                                await _migrationService.MigrateAsync();
                            }
                            catch (Exception migrationEx)
                            {
                                _logger.LogWarning(migrationEx, "Failed to apply pending migrations");
                            }
                        }
                    }
                }
                else
                {
                    // Fallback к старой логике если новый сервис недоступен
                    _logger.LogWarning("ApplicationVersionService not available, using fallback initialization");
                    
                    if (!canConnect)
                    {
                        await _context.Database.EnsureCreatedAsync();
                        _logger.LogInformation("Database created with fallback method");
                    }
                    
                    if (_migrationService != null)
                    {
                        try
                        {
                            await _migrationService.MigrateAsync();
                        }
                        catch (Exception migrationEx)
                        {
                            _logger.LogWarning(migrationEx, "Migration failed in fallback mode");
                        }
                    }
                }

                // Проверяем и добавляем seed данные если нужно
                await SeedDataIfNeededAsync();

                _logger.LogInformation("=== DATABASE INITIALIZATION COMPLETED ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== DATABASE INITIALIZATION FAILED ===");
                throw new InvalidOperationException($"Database initialization failed: {ex.Message}", ex);
            }
        }

        public async Task<bool> IsDatabaseReadyAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync() &&
                       await _context.Applications.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database readiness check failed");
                return false;
            }
        }

        private async Task SeedDataIfNeededAsync()
        {
            try
            {
                var existingApps = await _context.Applications.CountAsync();
                if (existingApps > 0)
                {
                    _logger.LogInformation("Database already contains {Count} applications, skipping seed", existingApps);
                    return;
                }

                _logger.LogInformation("Seeding initial data...");
                await SeedApplicationsAsync();

                _logger.LogInformation("Initial data seeded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed initial data - continuing anyway");
                // Не выбрасываем исключение - приложение может работать без seed данных
            }
        }

        private async Task SeedApplicationsAsync()
        {
            var seedApplications = new[]
            {
                new Application
                {
                    Name = "Calculator",
                    Description = "Windows Calculator",
                    ExecutablePath = "calc.exe",
                    Category = "Utilities",
                    Type = ApplicationType.Desktop,
                    MinimumRole = UserRole.Standard,
                    RequiredGroups = new List<string>(),
                    IsEnabled = true,
                    SortOrder = 1,
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new Application
                {
                    Name = "Notepad",
                    Description = "Text Editor",
                    ExecutablePath = "notepad.exe",
                    Category = "Utilities",
                    Type = ApplicationType.Desktop,
                    MinimumRole = UserRole.Standard,
                    RequiredGroups = new List<string>(),
                    IsEnabled = true,
                    SortOrder = 2,
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new Application
                {
                    Name = "Google",
                    Description = "Google Search",
                    ExecutablePath = "https://www.google.com",
                    Category = "Web",
                    Type = ApplicationType.Web,
                    MinimumRole = UserRole.Standard,
                    RequiredGroups = new List<string>(),
                    IsEnabled = true,
                    SortOrder = 3,
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new Application
                {
                    Name = "Control Panel",
                    Description = "Windows Control Panel",
                    ExecutablePath = "control.exe",
                    Category = "System",
                    Type = ApplicationType.Desktop,
                    MinimumRole = UserRole.PowerUser,
                    RequiredGroups = new List<string> { "LauncherPowerUsers", "LauncherAdmins" },
                    IsEnabled = true,
                    SortOrder = 4,
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new Application
                {
                    Name = "Command Prompt",
                    Description = "Windows Command Line",
                    ExecutablePath = "cmd.exe",
                    Category = "System",
                    Type = ApplicationType.Desktop,
                    MinimumRole = UserRole.Standard,
                    RequiredGroups = new List<string>(),
                    IsEnabled = true,
                    SortOrder = 5,
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                }
            };

            await _context.Applications.AddRangeAsync(seedApplications);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} applications", seedApplications.Length);
        }
        
        /// <summary>
        /// Помечает все существующие миграции как примененные (для новых баз данных)
        /// </summary>
        private async Task MarkExistingMigrationsAsAppliedAsync()
        {
            if (_migrationService == null) return;
            
            try
            {
                var allMigrations = _migrationService.GetAllMigrations();
                
                // Получаем конфигурацию БД для определения типа
                // Предполагаем что уже есть способ получить это
                var databaseType = DatabaseType.SQLite; // По умолчанию, но лучше получить из конфигурации
                
                foreach (var migration in allMigrations)
                {
                    await RecordMigrationAsAppliedAsync(migration, databaseType);
                }
                
                _logger.LogInformation("Marked {Count} migrations as applied", allMigrations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark existing migrations as applied");
            }
        }
        
        /// <summary>
        /// Записывает миграцию как примененную в таблице истории миграций
        /// </summary>
        private async Task RecordMigrationAsAppliedAsync(IDatabaseMigration migration, DatabaseType databaseType)
        {
            var sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
                    VALUES (@p0, @p1, @p2, @p3)",
                DatabaseType.Firebird => @"
                    INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
                    VALUES (@p0, @p1, @p2, @p3)",
                _ => throw new System.NotSupportedException($"Database type {databaseType} not supported")
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
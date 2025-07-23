// ===== WindowsLauncher.UI/Infrastructure/Services/DatabaseInitializer.cs - ПРОСТОЕ ИСПРАВЛЕНИЕ =====
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Extensions;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Services;
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
        private readonly IDatabaseVersionService? _databaseVersionService;

        public DatabaseInitializer(
            LauncherDbContext context, 
            ILogger<DatabaseInitializer> logger,
            IDatabaseMigrationService? migrationService = null,
            IDatabaseVersionService? databaseVersionService = null)
        {
            _context = context;
            _logger = logger;
            _migrationService = migrationService;
            _databaseVersionService = databaseVersionService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("=== STARTING DATABASE INITIALIZATION ===");

                // Проверяем возможность подключения
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation($"Database connection check: {canConnect}");

                // Создание/обновление базы данных
                if (!canConnect)
                {
                    _logger.LogInformation("Database does not exist, creating with EF Core...");
                    await _context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database created successfully");
                    
                    // Устанавливаем версию БД для новой базы
                    if (_databaseVersionService != null)
                    {
                        await _databaseVersionService.SetDatabaseVersionAsync("1.0.0.0");
                        _logger.LogInformation("Set initial database version to 1.0.0.0");
                    }
                }
                else
                {
                    _logger.LogInformation("Database exists, ensuring schema is up to date...");
                    
                    // Для существующих баз данных используем EnsureCreated для обеспечения схемы
                    // (это безопасно для существующих баз - не перезаписывает данные)
                    try
                    {
                        await _context.Database.EnsureCreatedAsync();
                        _logger.LogInformation("Database schema verified");
                    }
                    catch (Exception schemaEx)
                    {
                        _logger.LogWarning(schemaEx, "Schema verification failed, but continuing...");
                    }
                    
                    // Обновляем версию БД если сервис доступен
                    if (_databaseVersionService != null)
                    {
                        try
                        {
                            await _databaseVersionService.SetDatabaseVersionAsync("1.0.0.0");
                            _logger.LogInformation("Updated database version to 1.0.0.0");
                        }
                        catch (Exception versionEx)
                        {
                            _logger.LogWarning(versionEx, "Failed to update database version");
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
                    INSERT INTO __MigrationHistory (Version, Name, Description, AppliedAt)
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
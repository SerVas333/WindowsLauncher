using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Data;
using System.Linq;
using System.Collections.Generic;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Инициализация базы данных для UI слоя
    /// </summary>
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly LauncherDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(LauncherDbContext context, ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");

                // Получаем информацию о состоянии базы
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation("Database connection status: {CanConnect}", canConnect);

                if (!canConnect)
                {
                    _logger.LogInformation("Database doesn't exist, creating...");
                    await _context.Database.EnsureCreatedAsync();
                }
                else
                {
                    _logger.LogInformation("Database exists, checking for pending migrations...");

                    // Проверяем миграции только если база уже существует
                    var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                        await _context.Database.MigrateAsync();
                    }
                    else
                    {
                        _logger.LogInformation("No pending migrations found");
                    }
                }

                // Проверяем и инициализируем данные
                await SeedDataAsync();

                _logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");

                // Попытка решить проблему с существующими таблицами
                if (ex.Message.Contains("already exists"))
                {
                    _logger.LogWarning("Tables already exist, attempting to continue with existing database...");

                    try
                    {
                        // Просто проверяем что можем читать из базы
                        var appCount = await _context.Applications.CountAsync();
                        _logger.LogInformation("Found {Count} applications in existing database", appCount);

                        // Если данных нет - добавляем
                        if (appCount == 0)
                        {
                            await SeedDataAsync();
                        }

                        return; // Успешно продолжили с существующей базой
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to work with existing database");
                        throw;
                    }
                }

                throw;
            }
        }

        public async Task<bool> IsDatabaseReadyAsync()
        {
            try
            {
                // Проверяем подключение и что можем читать основные таблицы
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect) return false;

                // Проверяем что таблица Applications доступна
                var appCount = await _context.Applications.CountAsync();
                _logger.LogDebug("Database ready check: {Count} applications found", appCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database readiness check failed");
                return false;
            }
        }

        private async Task SeedDataAsync()
        {
            try
            {
                // Проверяем есть ли уже данные
                var existingAppCount = await _context.Applications.CountAsync();
                if (existingAppCount > 0)
                {
                    _logger.LogInformation("Database already contains {Count} applications, skipping seed", existingAppCount);
                    return;
                }

                _logger.LogInformation("Seeding initial data...");

                // Добавляем тестовые приложения
                var applications = new[]
                {
                    new WindowsLauncher.Core.Models.Application
                    {
                        Name = "Calculator",
                        Description = "Windows Calculator",
                        ExecutablePath = "calc.exe",
                        Category = "Utilities",
                        IsEnabled = true,
                        RequiredGroups = new List<string> { "Users" }
                    },
                    new WindowsLauncher.Core.Models.Application
                    {
                        Name = "Notepad",
                        Description = "Text Editor",
                        ExecutablePath = "notepad.exe",
                        Category = "Utilities",
                        IsEnabled = true,
                        RequiredGroups = new List<string> { "Users" }
                    },
                    new WindowsLauncher.Core.Models.Application
                    {
                        Name = "Control Panel",
                        Description = "Windows Control Panel",
                        ExecutablePath = "control.exe",
                        Category = "System",
                        IsEnabled = true,
                        RequiredGroups = new List<string> { "Administrators" }
                    },
                    new WindowsLauncher.Core.Models.Application
                    {
                        Name = "Command Prompt",
                        Description = "Windows Command Line",
                        ExecutablePath = "cmd.exe",
                        Category = "System",
                        IsEnabled = true,
                        RequiredGroups = new List<string> { "Users" }
                    },
                    new WindowsLauncher.Core.Models.Application
                    {
                        Name = "Registry Editor",
                        Description = "Windows Registry Editor",
                        ExecutablePath = "regedit.exe",
                        Category = "System",
                        IsEnabled = true,
                        RequiredGroups = new List<string> { "Administrators" }
                    }
                };

                await _context.Applications.AddRangeAsync(applications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded {Count} test applications", applications.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed initial data");
                // Не выбрасываем исключение - приложение может работать без seed данных
            }
        }
    }
}

// ===== АЛЬТЕРНАТИВНОЕ РЕШЕНИЕ: Простая инициализация без миграций =====

/*
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Data;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    public class SimpleDatabaseInitializer : IDatabaseInitializer
    {
        private readonly LauncherDbContext _context;
        private readonly ILogger<SimpleDatabaseInitializer> _logger;

        public SimpleDatabaseInitializer(LauncherDbContext context, ILogger<SimpleDatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Simple database initialization...");
                
                // Просто проверяем что база доступна
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation("Database connection: {Status}", canConnect ? "OK" : "Failed");

                if (canConnect)
                {
                    var appCount = await _context.Applications.CountAsync();
                    _logger.LogInformation("Found {Count} applications in database", appCount);
                }
                else
                {
                    throw new InvalidOperationException("Cannot connect to database");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                throw;
            }
        }

        public async Task<bool> IsDatabaseReadyAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }
    }
}
*/
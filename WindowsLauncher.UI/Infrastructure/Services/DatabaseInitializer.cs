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

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Исправленная инициализация базы данных
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
                _logger.LogInformation("=== STARTING DATABASE INITIALIZATION ===");

                // 🆕 ИСПОЛЬЗУЕМ МИГРАЦИИ ВМЕСТО EnsureCreated
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations applied successfully");

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
    }
}
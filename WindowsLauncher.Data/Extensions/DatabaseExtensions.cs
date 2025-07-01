// WindowsLauncher.Data/Extensions/DatabaseExtensions.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Data.Extensions
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Extension метод для инициализации seed данных
        /// </summary>
        public static async Task SeedDataAsync(this LauncherDbContext context)
        {
            try
            {
                // Проверяем, есть ли уже данные
                if (await context.Applications.AnyAsync())
                {
                    return; // Данные уже есть
                }

                var seedDate = new DateTime(2024, 1, 1, 12, 0, 0);

                // Добавляем начальные приложения
                var applications = new[]
                {
                    new Application
                    {
                        Name = "Calculator",
                        Description = "Windows Calculator",
                        ExecutablePath = "calc.exe",
                        Arguments = "",
                        IconPath = "",
                        Category = "Tools",
                        Type = ApplicationType.Desktop,
                        MinimumRole = UserRole.Standard,
                        RequiredGroups = new List<string>(),
                        IsEnabled = true,
                        SortOrder = 1,
                        CreatedDate = seedDate,
                        ModifiedDate = seedDate,
                        CreatedBy = "System"
                    },
                    new Application
                    {
                        Name = "Notepad",
                        Description = "Text Editor",
                        ExecutablePath = "notepad.exe",
                        Arguments = "",
                        IconPath = "",
                        Category = "Tools",
                        Type = ApplicationType.Desktop,
                        MinimumRole = UserRole.Standard,
                        RequiredGroups = new List<string>(),
                        IsEnabled = true,
                        SortOrder = 2,
                        CreatedDate = seedDate,
                        ModifiedDate = seedDate,
                        CreatedBy = "System"
                    },
                    new Application
                    {
                        Name = "Google",
                        Description = "Google Search",
                        ExecutablePath = "https://www.google.com",
                        Arguments = "",
                        IconPath = "",
                        Category = "Web",
                        Type = ApplicationType.Web,
                        MinimumRole = UserRole.Standard,
                        RequiredGroups = new List<string>(),
                        IsEnabled = true,
                        SortOrder = 3,
                        CreatedDate = seedDate,
                        ModifiedDate = seedDate,
                        CreatedBy = "System"
                    },
                    new Application
                    {
                        Name = "Control Panel",
                        Description = "Windows Control Panel",
                        ExecutablePath = "control.exe",
                        Arguments = "",
                        IconPath = "",
                        Category = "System",
                        Type = ApplicationType.Desktop,
                        MinimumRole = UserRole.PowerUser,
                        RequiredGroups = new List<string> { "LauncherPowerUsers", "LauncherAdmins" },
                        IsEnabled = true,
                        SortOrder = 4,
                        CreatedDate = seedDate,
                        ModifiedDate = seedDate,
                        CreatedBy = "System"
                    },
                    new Application
                    {
                        Name = "Command Prompt",
                        Description = "Windows Command Line",
                        ExecutablePath = "cmd.exe",
                        Arguments = "",
                        IconPath = "",
                        Category = "System",
                        Type = ApplicationType.Desktop,
                        MinimumRole = UserRole.Standard,
                        RequiredGroups = new List<string>(),
                        IsEnabled = true,
                        SortOrder = 5,
                        CreatedDate = seedDate,
                        ModifiedDate = seedDate,
                        CreatedBy = "System"
                    }
                };

                await context.Applications.AddRangeAsync(applications);
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Логируем ошибку, но не прерываем инициализацию
                throw;
            }
        }
    }
}
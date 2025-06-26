using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(LauncherDbContext context)
        {
            // Проверяем, есть ли уже данные
            if (context.Applications.Any())
                return; // Данные уже есть

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
                    RequiredGroups = new List<string>(), // Пустой список
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
                    MinimumRole = UserRole.PowerUser, // Только для PowerUser и выше
                    RequiredGroups = new List<string> { "LauncherPowerUsers", "LauncherAdmins" },
                    IsEnabled = true,
                    SortOrder = 4,
                    CreatedDate = seedDate,
                    ModifiedDate = seedDate,
                    CreatedBy = "System"
                }
            };

            await context.Applications.AddRangeAsync(applications);
            await context.SaveChangesAsync();
        }
    }
}
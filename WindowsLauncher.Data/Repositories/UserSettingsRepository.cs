// WindowsLauncher.Data/Repositories/UserSettingsRepository.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Data.Repositories
{
    public class UserSettingsRepository : BaseRepositoryWithFactory<UserSettings>, IUserSettingsRepository
    {
        public UserSettingsRepository(IDbContextFactory<LauncherDbContext> contextFactory) : base(contextFactory)
        {
        }

        public async Task<UserSettings?> GetByUsernameAsync(string username)
        {
            // Получаем настройки через связь с пользователем по Username
            return await ExecuteWithContextAsync(async context =>
                await context.UserSettings
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.User != null && s.User.Username == username));
        }

        public async Task<UserSettings?> GetByUserIdAsync(int userId)
        {
            return await ExecuteWithContextAsync(async context =>
                await context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId));
        }

        public async Task<UserSettings> CreateDefaultSettingsAsync(string username)
        {
            return await ExecuteWithContextAsync(async context =>
            {
                // Сначала находим пользователя по username
                var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    throw new ArgumentException($"User with username '{username}' not found");
                }

                var settings = new UserSettings
                {
                    UserId = user.Id,
                    Theme = "Light",
                    AccentColor = "Blue",
                    TileSize = 150,
                    ShowCategories = true,
                    DefaultCategory = "All",
                    AutoRefresh = true,
                    RefreshIntervalMinutes = 30,
                    ShowDescriptions = true,
                    LastModified = DateTime.Now
                };

                context.UserSettings.Add(settings);
                await context.SaveChangesAsync();
                return settings;
            });
        }

        public async Task<UserSettings> CreateDefaultSettingsAsync(int userId)
        {
            return await ExecuteWithContextAsync(async context =>
            {
                var settings = new UserSettings
                {
                    UserId = userId,
                    Theme = "Light",
                    AccentColor = "Blue",
                    TileSize = 150,
                    ShowCategories = true,
                    DefaultCategory = "All",
                    AutoRefresh = true,
                    RefreshIntervalMinutes = 30,
                    ShowDescriptions = true,
                    LastModified = DateTime.Now
                };

                context.UserSettings.Add(settings);
                await context.SaveChangesAsync();
                return settings;
            });
        }
    }
}

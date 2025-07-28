// WindowsLauncher.Data/Repositories/UserSettingsRepository.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Data.Repositories
{
    public class UserSettingsRepository : BaseRepository<UserSettings>, IUserSettingsRepository
    {
        public UserSettingsRepository(LauncherDbContext context) : base(context)
        {
        }

        public async Task<UserSettings?> GetByUsernameAsync(string username)
        {
            // Получаем настройки через связь с пользователем по Username
            return await _dbSet
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.User != null && s.User.Username == username);
        }

        public async Task<UserSettings?> GetByUserIdAsync(int userId)
        {
            return await _dbSet.FirstOrDefaultAsync(s => s.UserId == userId);
        }

        public async Task<UserSettings> CreateDefaultSettingsAsync(string username)
        {
            // Сначала находим пользователя по username
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
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

            await AddAsync(settings);
            await SaveChangesAsync();
            return settings;
        }

        public async Task<UserSettings> CreateDefaultSettingsAsync(int userId)
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

            await AddAsync(settings);
            await SaveChangesAsync();
            return settings;
        }
    }
}

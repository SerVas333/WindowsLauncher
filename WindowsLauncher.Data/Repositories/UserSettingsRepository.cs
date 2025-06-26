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
            return await _dbSet.FirstOrDefaultAsync(s => s.Username == username);
        }

        public async Task<UserSettings> CreateDefaultSettingsAsync(string username)
        {
            var settings = new UserSettings
            {
                Username = username,
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

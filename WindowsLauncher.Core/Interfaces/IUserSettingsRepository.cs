// WindowsLauncher.Core/Interfaces/IUserSettingsRepository.cs
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IUserSettingsRepository : IRepository<UserSettings>
    {
        /// <summary>
        /// Получить настройки пользователя по имени
        /// </summary>
        Task<UserSettings?> GetByUsernameAsync(string username);

        /// <summary>
        /// Получить настройки пользователя по ID
        /// </summary>
        Task<UserSettings?> GetByUserIdAsync(int userId);

        /// <summary>
        /// Создать настройки по умолчанию для пользователя (по username)
        /// </summary>
        Task<UserSettings> CreateDefaultSettingsAsync(string username);

        /// <summary>
        /// Создать настройки по умолчанию для пользователя (по userId)
        /// </summary>
        Task<UserSettings> CreateDefaultSettingsAsync(int userId);
    }
}

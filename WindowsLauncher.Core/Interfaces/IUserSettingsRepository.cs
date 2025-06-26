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
        /// Создать настройки по умолчанию для пользователя
        /// </summary>
        Task<UserSettings> CreateDefaultSettingsAsync(string username);
    }
}

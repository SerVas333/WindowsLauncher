// WindowsLauncher.Core/Interfaces/IUserRepository.cs
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        /// <summary>
        /// Найти пользователя по имени пользователя
        /// </summary>
        Task<User?> GetByUsernameAsync(string username);

        /// <summary>
        /// Создать или обновить пользователя
        /// </summary>
        Task<User> UpsertUserAsync(User user);

        /// <summary>
        /// Обновить время последнего входа
        /// </summary>
        Task UpdateLastLoginAsync(string username);
    }
}


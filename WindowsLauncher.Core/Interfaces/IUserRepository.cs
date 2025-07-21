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

        #region Методы для гибридной авторизации

        /// <summary>
        /// Получить пользователей определенного типа аутентификации
        /// </summary>
        Task<List<User>> GetByAuthenticationTypeAsync(AuthenticationType authType);

        /// <summary>
        /// Получить локальных пользователей
        /// </summary>
        Task<List<User>> GetLocalUsersAsync();

        /// <summary>
        /// Получить кэшированных доменных пользователей
        /// </summary>
        Task<List<User>> GetCachedDomainUsersAsync();

        /// <summary>
        /// Найти пользователя по доменному имени
        /// </summary>
        Task<User?> GetByDomainUsernameAsync(string domainUsername);

        /// <summary>
        /// Получить пользователей, которым нужна синхронизация с доменом
        /// </summary>
        Task<List<User>> GetUsersRequiringSyncAsync(TimeSpan maxAge);

        /// <summary>
        /// Обновить время синхронизации с доменом
        /// </summary>
        Task UpdateDomainSyncAsync(int userId);

        /// <summary>
        /// Создать или обновить кэшированного доменного пользователя
        /// </summary>
        Task<User> UpsertCachedDomainUserAsync(User domainUser, string domainUsername);

        #endregion
    }
}


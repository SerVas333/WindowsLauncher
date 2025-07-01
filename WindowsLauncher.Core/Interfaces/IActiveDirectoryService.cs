// WindowsLauncher.Core/Interfaces/IActiveDirectoryService.cs - ПОЛНОСТЬЮ ОЧИЩЕННАЯ ВЕРСИЯ
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис работы с Active Directory
    /// </summary>
    public interface IActiveDirectoryService
    {
        /// <summary>
        /// Тестировать подключение к AD серверу
        /// </summary>
        Task<bool> TestConnectionAsync(string server, int port);

        /// <summary>
        /// Аутентифицировать пользователя в AD
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Получить информацию о пользователе из AD
        /// </summary>
        Task<AdUserInfo?> GetUserInfoAsync(string username);

        /// <summary>
        /// Получить группы пользователя
        /// </summary>
        Task<List<string>> GetUserGroupsAsync(string username);

        /// <summary>
        /// Проверить принадлежность к группе
        /// </summary>
        Task<bool> IsUserInGroupAsync(string username, string groupName);

        /// <summary>
        /// Найти пользователей в AD
        /// </summary>
        Task<List<AdUserInfo>> SearchUsersAsync(string searchTerm, int maxResults = 50);

        /// <summary>
        /// Получить список групп в AD
        /// </summary>
        Task<List<string>> GetGroupsAsync();

        /// <summary>
        /// Проверить доступность домена
        /// </summary>
        Task<bool> IsDomainAvailableAsync();
    }
}
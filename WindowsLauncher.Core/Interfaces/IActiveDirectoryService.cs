// WindowsLauncher.Core/Interfaces/IActiveDirectoryService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
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
        Task<AdUserInfo> GetUserInfoAsync(string username);

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

    /// <summary>
    /// Результат аутентификации в AD
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccessful { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public UserRole Role { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime AuthenticatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Информация о пользователе AD
    /// </summary>
    public class AdUserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastLogon { get; set; }
        public DateTime? PasswordLastSet { get; set; }
    }
}
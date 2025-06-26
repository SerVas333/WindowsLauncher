// WindowsLauncher.Core/Interfaces/IAuthenticationService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IAuthenticationService
    {
        /// <summary>
        /// Аутентификация пользователя (автоматическая через Windows или с учетными данными)
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(string? username = null, string? password = null);

        /// <summary>
        /// Получить текущего аутентифицированного пользователя
        /// </summary>
        Task<User?> GetCurrentUserAsync();

        /// <summary>
        /// Получить список групп пользователя из AD
        /// </summary>
        Task<List<string>> GetUserGroupsAsync(string username);

        /// <summary>
        /// Определить роль пользователя на основе его групп AD
        /// </summary>
        Task<UserRole> DetermineUserRoleAsync(List<string> groups);

        /// <summary>
        /// Проверить принадлежность пользователя к группе
        /// </summary>
        Task<bool> IsUserInGroupAsync(string username, string groupName);

        /// <summary>
        /// Выход из системы
        /// </summary>
        void Logout();

        /// <summary>
        /// Проверка, аутентифицирован ли пользователь
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Событие изменения состояния аутентификации
        /// </summary>
        event EventHandler<User>? UserAuthenticated;
        event EventHandler? UserLoggedOut;
    }
}

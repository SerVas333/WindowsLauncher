// WindowsLauncher.Core/Interfaces/IAuthorizationService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IAuthorizationService
    {
        /// <summary>
        /// Проверить может ли пользователь получить доступ к приложению
        /// </summary>
        Task<bool> CanAccessApplicationAsync(User user, Application application);

        /// <summary>
        /// Получить список приложений доступных пользователю
        /// </summary>
        Task<List<Application>> GetAuthorizedApplicationsAsync(User user);

        /// <summary>
        /// Получить настройки пользователя (создать если не существуют)
        /// </summary>
        Task<UserSettings> GetUserSettingsAsync(User user);

        /// <summary>
        /// Сохранить настройки пользователя
        /// </summary>
        Task SaveUserSettingsAsync(UserSettings settings);

        /// <summary>
        /// Может ли пользователь управлять приложениями (добавлять/редактировать)
        /// </summary>
        bool CanManageApplications(User user);

        /// <summary>
        /// Может ли пользователь просматривать системную информацию
        /// </summary>
        bool CanViewSystemInfo(User user);

        /// <summary>
        /// Может ли пользователь просматривать логи аудита
        /// </summary>
        bool CanViewAuditLogs(User user);

        /// <summary>
        /// Обновить кэш прав пользователя
        /// </summary>
        Task RefreshUserPermissionsAsync(string username);
    }
}

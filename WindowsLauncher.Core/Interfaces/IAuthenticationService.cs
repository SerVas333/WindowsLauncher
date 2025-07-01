// WindowsLauncher.Core/Interfaces/IAuthenticationService.cs
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Упрощенный интерфейс сервиса аутентификации
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Аутентифицировать пользователя Windows
        /// </summary>
        Task<User> AuthenticateWindowsUserAsync();

        /// <summary>
        /// Аутентифицировать пользователя по логину/паролю
        /// </summary>
        Task<User> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Аутентифицировать сервисного администратора
        /// </summary>
        Task<User> AuthenticateServiceAdminAsync(string username, string password);

        /// <summary>
        /// Создать сервисного администратора
        /// </summary>
        Task CreateServiceAdminAsync(string username, string password);

        /// <summary>
        /// Изменить пароль сервисного администратора
        /// </summary>
        Task ChangeServiceAdminPasswordAsync(string username, string oldPassword, string newPassword);

        /// <summary>
        /// Проверить права пользователя
        /// </summary>
        Task<bool> HasPermissionAsync(int userId, string permission);

        /// <summary>
        /// Получить роль пользователя
        /// </summary>
        Task<UserRole> GetUserRoleAsync(string username);

        /// <summary>
        /// Выход из системы
        /// </summary>
        Task LogoutAsync(int userId);

        /// <summary>
        /// Обновить сессию пользователя
        /// </summary>
        Task RefreshSessionAsync(int userId);

        /// <summary>
        /// Проверить валидность сессии
        /// </summary>
        Task<bool> IsSessionValidAsync(int userId);
    }
}
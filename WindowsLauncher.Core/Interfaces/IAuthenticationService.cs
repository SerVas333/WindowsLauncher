// WindowsLauncher.Core/Interfaces/IAuthenticationService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса аутентификации с поддержкой всех методов
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Аутентифицировать пользователя Windows (без параметров - использует текущего пользователя)
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync();

        /// <summary>
        /// Аутентифицировать пользователя по логину/паролю
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Аутентифицировать пользователя с учетными данными
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(AuthenticationCredentials credentials);

        /// <summary>
        /// Аутентифицировать пользователя Windows (получить объект User)
        /// </summary>
        Task<User> AuthenticateWindowsUserAsync();

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
        /// Выход из системы (перегрузка без параметров)
        /// </summary>
        void Logout();

        /// <summary>
        /// Обновить сессию пользователя
        /// </summary>
        Task RefreshSessionAsync(int userId);

        /// <summary>
        /// Проверить валидность сессии
        /// </summary>
        Task<bool> IsSessionValidAsync(int userId);

        /// <summary>
        /// Проверить доступность домена
        /// </summary>
        Task<bool> IsDomainAvailableAsync(string domain);

        /// <summary>
        /// Проверить, настроен ли сервисный администратор
        /// </summary>
        bool IsServiceAdminConfigured();
    }
}
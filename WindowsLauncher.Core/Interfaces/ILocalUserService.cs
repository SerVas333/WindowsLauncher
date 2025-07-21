using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для работы с локальными пользователями
    /// </summary>
    public interface ILocalUserService
    {
        #region CRUD операции

        /// <summary>
        /// Создать нового локального пользователя
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <param name="displayName">Отображаемое имя</param>
        /// <param name="email">Email адрес</param>
        /// <param name="role">Роль пользователя</param>
        /// <returns>Созданный пользователь</returns>
        Task<User> CreateLocalUserAsync(string username, string password, string displayName = "", string email = "", UserRole role = UserRole.Standard);

        /// <summary>
        /// Получить всех локальных пользователей
        /// </summary>
        /// <returns>Список локальных пользователей</returns>
        Task<List<User>> GetLocalUsersAsync();

        /// <summary>
        /// Получить локального пользователя по имени
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <returns>Пользователь или null</returns>
        Task<User?> GetLocalUserByUsernameAsync(string username);

        /// <summary>
        /// Получить локального пользователя по ID
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <returns>Пользователь или null</returns>
        Task<User?> GetLocalUserByIdAsync(int userId);

        /// <summary>
        /// Обновить локального пользователя
        /// </summary>
        /// <param name="user">Пользователь для обновления</param>
        /// <returns>Обновленный пользователь</returns>
        Task<User> UpdateLocalUserAsync(User user);

        /// <summary>
        /// Удалить локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <returns>True если удален успешно</returns>
        Task<bool> DeleteLocalUserAsync(int userId);

        #endregion

        #region Аутентификация

        /// <summary>
        /// Валидировать учетные данные локального пользователя
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <returns>True если учетные данные верны</returns>
        Task<bool> ValidateLocalUserAsync(string username, string password);

        /// <summary>
        /// Аутентификация локального пользователя
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <returns>Результат аутентификации</returns>
        Task<AuthenticationResult> AuthenticateLocalUserAsync(string username, string password);

        #endregion

        #region Управление паролями

        /// <summary>
        /// Обновить пароль локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="newPassword">Новый пароль</param>
        /// <returns>True если пароль обновлен успешно</returns>
        Task<bool> UpdateLocalUserPasswordAsync(int userId, string newPassword);

        /// <summary>
        /// Сбросить пароль локального пользователя (для администраторов)
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="newPassword">Новый пароль</param>
        /// <param name="adminUserId">ID администратора</param>
        /// <returns>True если пароль сброшен успешно</returns>
        Task<bool> ResetLocalUserPasswordAsync(int userId, string newPassword, int adminUserId);

        /// <summary>
        /// Валидировать пароль согласно политикам безопасности
        /// </summary>
        /// <param name="password">Пароль для проверки</param>
        /// <returns>Результат валидации</returns>
        Task<PasswordValidationResult> ValidatePasswordAsync(string password);

        #endregion

        #region Управление ролями

        /// <summary>
        /// Изменить роль локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="newRole">Новая роль</param>
        /// <param name="adminUserId">ID администратора</param>
        /// <returns>True если роль изменена успешно</returns>
        Task<bool> ChangeLocalUserRoleAsync(int userId, UserRole newRole, int adminUserId);

        #endregion

        #region Управление активностью аккаунтов

        /// <summary>
        /// Активировать локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="adminUserId">ID администратора</param>
        /// <returns>True если пользователь активирован</returns>
        Task<bool> ActivateLocalUserAsync(int userId, int adminUserId);

        /// <summary>
        /// Деактивировать локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="adminUserId">ID администратора</param>
        /// <returns>True если пользователь деактивирован</returns>
        Task<bool> DeactivateLocalUserAsync(int userId, int adminUserId);

        /// <summary>
        /// Заблокировать локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="lockDuration">Продолжительность блокировки</param>
        /// <param name="reason">Причина блокировки</param>
        /// <returns>True если пользователь заблокирован</returns>
        Task<bool> LockLocalUserAsync(int userId, TimeSpan lockDuration, string reason);

        /// <summary>
        /// Разблокировать локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="adminUserId">ID администратора</param>
        /// <returns>True если пользователь разблокирован</returns>
        Task<bool> UnlockLocalUserAsync(int userId, int adminUserId);

        #endregion

        #region Аудит и статистика

        /// <summary>
        /// Получить статистику входов локального пользователя
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="fromDate">Начальная дата</param>
        /// <param name="toDate">Конечная дата</param>
        /// <returns>Статистика входов</returns>
        Task<UserLoginStatistics> GetLocalUserLoginStatisticsAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Проверить права администратора
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <returns>True если пользователь является администратором</returns>
        Task<bool> IsAdministratorAsync(int userId);

        #endregion
    }

    /// <summary>
    /// Результат валидации пароля
    /// </summary>
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public static PasswordValidationResult Success() => new() { IsValid = true };
        public static PasswordValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
        
        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    /// <summary>
    /// Статистика входов пользователя
    /// </summary>
    public class UserLoginStatistics
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int TotalLogins { get; set; }
        public int SuccessfulLogins { get; set; }
        public int FailedLogins { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? LastFailedLogin { get; set; }
        public List<DateTime> RecentLogins { get; set; } = new();
        public List<string> RecentFailures { get; set; } = new();
    }
}
using System;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис аутентификации с поддержкой различных типов входа
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Текущая сессия пользователя
        /// </summary>
        AuthenticationSession CurrentSession { get; }

        /// <summary>
        /// Событие изменения статуса аутентификации
        /// </summary>
        event EventHandler<AuthenticationResult> AuthenticationChanged;

        /// <summary>
        /// Автоматическая аутентификация (сначала Windows SSO, затем запрос учетных данных)
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync();

        /// <summary>
        /// Аутентификация с явными учетными данными
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(AuthenticationCredentials credentials);

        /// <summary>
        /// Аутентификация Windows SSO (только для доменных компьютеров)
        /// </summary>
        Task<AuthenticationResult> AuthenticateWindowsAsync();

        /// <summary>
        /// Аутентификация через LDAP (для недоменных компьютеров)
        /// </summary>
        Task<AuthenticationResult> AuthenticateLdapAsync(AuthenticationCredentials credentials);

        /// <summary>
        /// Аутентификация сервисного администратора
        /// </summary>
        Task<AuthenticationResult> AuthenticateServiceAdminAsync(string username, string password);

        /// <summary>
        /// Проверка доступности домена
        /// </summary>
        Task<bool> IsDomainAvailableAsync(string domain = null);

        /// <summary>
        /// Проверка, находится ли компьютер в домене
        /// </summary>
        bool IsComputerInDomain();

        /// <summary>
        /// Выход из системы
        /// </summary>
        Task LogoutAsync();

        /// <summary>
        /// Обновление текущей сессии
        /// </summary>
        Task RefreshSessionAsync();

        /// <summary>
        /// Проверка активности сессии
        /// </summary>
        bool IsSessionValid();

        /// <summary>
        /// Настройка пароля сервисного администратора (только при первом запуске)
        /// </summary>
        Task<bool> SetupServiceAdminPasswordAsync(string password);

        /// <summary>
        /// Проверка, настроен ли сервисный администратор
        /// </summary>
        bool IsServiceAdminConfigured();
    }

    /// <summary>
    /// Сервис для работы с Active Directory
    /// </summary>
    public interface IActiveDirectoryService
    {
        /// <summary>
        /// Получение пользователя из AD по имени
        /// </summary>
        Task<User> GetUserAsync(string username, string domain = null);

        /// <summary>
        /// Получение групп пользователя
        /// </summary>
        Task<string[]> GetUserGroupsAsync(string username, string domain = null);

        /// <summary>
        /// Проверка учетных данных в AD
        /// </summary>
        Task<bool> ValidateCredentialsAsync(string username, string password, string domain = null);

        /// <summary>
        /// Поиск пользователей в AD
        /// </summary>
        Task<User[]> SearchUsersAsync(string searchTerm, string domain = null);

        /// <summary>
        /// Проверка подключения к серверу AD
        /// </summary>
        Task<bool> TestConnectionAsync(string server, int port = 389);
    }

    /// <summary>
    /// Сервис для работы с локальными настройками аутентификации
    /// </summary>
    public interface IAuthenticationConfigurationService
    {
        /// <summary>
        /// Получение конфигурации AD
        /// </summary>
        ActiveDirectoryConfiguration GetConfiguration();

        /// <summary>
        /// Сохранение конфигурации AD
        /// </summary>
        Task SaveConfigurationAsync(ActiveDirectoryConfiguration config);

        /// <summary>
        /// Сброс конфигурации к настройкам по умолчанию
        /// </summary>
        Task ResetConfigurationAsync();

        /// <summary>
        /// Экспорт конфигурации (без паролей)
        /// </summary>
        string ExportConfiguration();

        /// <summary>
        /// Импорт конфигурации
        /// </summary>
        Task<bool> ImportConfigurationAsync(string configJson);

        /// <summary>
        /// Валидация конфигурации
        /// </summary>
        ValidationResult ValidateConfiguration(ActiveDirectoryConfiguration config);
    }

    /// <summary>
    /// Результат валидации конфигурации
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();
    }
}
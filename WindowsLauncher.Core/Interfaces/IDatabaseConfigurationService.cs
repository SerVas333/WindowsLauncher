using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для управления конфигурацией базы данных
    /// </summary>
    public interface IDatabaseConfigurationService
    {
        /// <summary>
        /// Получить текущую конфигурацию базы данных
        /// </summary>
        Task<DatabaseConfiguration> GetConfigurationAsync();

        /// <summary>
        /// Сохранить конфигурацию базы данных
        /// </summary>
        Task SaveConfigurationAsync(DatabaseConfiguration configuration);

        /// <summary>
        /// Проверить подключение к базе данных
        /// </summary>
        Task<bool> TestConnectionAsync(DatabaseConfiguration configuration);

        /// <summary>
        /// Получить конфигурацию по умолчанию
        /// </summary>
        DatabaseConfiguration GetDefaultConfiguration();

        /// <summary>
        /// Проверить, настроена ли база данных
        /// </summary>
        Task<bool> IsConfiguredAsync();

        /// <summary>
        /// Создать базу данных если она не существует
        /// </summary>
        Task<bool> EnsureDatabaseExistsAsync(DatabaseConfiguration configuration);

        /// <summary>
        /// Получить информацию о базе данных
        /// </summary>
        Task<DatabaseInfo> GetDatabaseInfoAsync(DatabaseConfiguration configuration);

        /// <summary>
        /// Удалить файл конфигурации базы данных
        /// </summary>
        Task DeleteConfigurationAsync();

        /// <summary>
        /// Проверить существование файла конфигурации
        /// </summary>
        bool ConfigurationFileExists();

        /// <summary>
        /// Получить путь к файлу конфигурации
        /// </summary>
        string GetConfigurationFilePath();
    }

}
// WindowsLauncher.Core/Interfaces/IAuthenticationConfigurationService.cs - ЗАМЕНА СУЩЕСТВУЮЩЕГО
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис управления конфигурацией аутентификации
    /// </summary>
    public interface IAuthenticationConfigurationService
    {
        /// <summary>
        /// Получить текущую конфигурацию
        /// </summary>
        AuthenticationConfiguration GetConfiguration();

        /// <summary>
        /// Сохранить конфигурацию
        /// </summary>
        Task SaveConfigurationAsync(AuthenticationConfiguration configuration);

        /// <summary>
        /// Проверить, настроена ли система
        /// </summary>
        bool IsConfigured();

        /// <summary>
        /// Сбросить конфигурацию к значениям по умолчанию
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// Валидировать конфигурацию
        /// </summary>
        Task<ValidationResult> ValidateConfigurationAsync(AuthenticationConfiguration configuration);

        /// <summary>
        /// Экспортировать конфигурацию
        /// </summary>
        Task<string> ExportConfigurationAsync();

        /// <summary>
        /// Импортировать конфигурацию
        /// </summary>
        Task ImportConfigurationAsync(string configurationJson);
    }

    /// <summary>
    /// Результат валидации конфигурации
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
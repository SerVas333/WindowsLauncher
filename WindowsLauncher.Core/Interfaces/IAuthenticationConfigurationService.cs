// WindowsLauncher.Core/Interfaces/IAuthenticationConfigurationService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
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
        public string[] Errors { get; set; } = System.Array.Empty<string>();
        public string[] Warnings { get; set; } = System.Array.Empty<string>();
    }
}
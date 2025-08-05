using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Репозиторий для управления настройками SMTP серверов
    /// </summary>
    public interface ISmtpSettingsRepository
    {
        /// <summary>
        /// Получить все активные настройки SMTP
        /// </summary>
        Task<IReadOnlyList<SmtpSettings>> GetActiveSettingsAsync();
        
        /// <summary>
        /// Получить настройки SMTP по ID
        /// </summary>
        Task<SmtpSettings?> GetByIdAsync(int id);
        
        /// <summary>
        /// Получить настройки по типу сервера
        /// </summary>
        Task<SmtpSettings?> GetByTypeAsync(SmtpServerType serverType);
        
        /// <summary>
        /// Создать новые настройки SMTP
        /// </summary>
        Task<SmtpSettings> CreateAsync(SmtpSettings settings);
        
        /// <summary>
        /// Обновить настройки SMTP
        /// </summary>
        Task<SmtpSettings> UpdateAsync(SmtpSettings settings);
        
        /// <summary>
        /// Удалить настройки SMTP
        /// </summary>
        Task<bool> DeleteAsync(int id);
        
        /// <summary>
        /// Проверить существование основного сервера
        /// </summary>
        Task<bool> HasPrimaryServerAsync();
        
        /// <summary>
        /// Проверить существование резервного сервера
        /// </summary>
        Task<bool> HasBackupServerAsync();
    }
}
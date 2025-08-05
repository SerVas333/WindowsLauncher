using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Core.Interfaces.Email
{
    /// <summary>
    /// Интерфейс для управления локальной адресной книгой
    /// </summary>
    public interface IAddressBookService
    {
        /// <summary>
        /// Получить все активные контакты
        /// </summary>
        Task<IReadOnlyList<Contact>> GetAllContactsAsync();
        
        /// <summary>
        /// Получить контакты по группе
        /// </summary>
        /// <param name="group">Название группы (null для контактов без группы)</param>
        Task<IReadOnlyList<Contact>> GetContactsByGroupAsync(string? group);
        
        /// <summary>
        /// Найти контакты по имени или email
        /// </summary>
        /// <param name="searchTerm">Поисковый запрос</param>
        Task<IReadOnlyList<Contact>> SearchContactsAsync(string searchTerm);
        
        /// <summary>
        /// Получить контакт по ID
        /// </summary>
        Task<Contact?> GetContactByIdAsync(int id);
        
        /// <summary>
        /// Получить контакт по email адресу
        /// </summary>
        Task<Contact?> GetContactByEmailAsync(string email);
        
        /// <summary>
        /// Создать новый контакт
        /// </summary>
        Task<Contact> CreateContactAsync(Contact contact);
        
        /// <summary>
        /// Обновить существующий контакт
        /// </summary>
        Task<Contact> UpdateContactAsync(Contact contact);
        
        /// <summary>
        /// Удалить контакт (мягкое удаление - IsActive = false)
        /// </summary>
        Task<bool> DeleteContactAsync(int id);
        
        /// <summary>
        /// Восстановить удаленный контакт
        /// </summary>
        Task<bool> RestoreContactAsync(int id);
        
        /// <summary>
        /// Получить все используемые группы
        /// </summary>
        Task<IReadOnlyList<string>> GetAllGroupsAsync();
        
        /// <summary>
        /// Импортировать контакты из CSV
        /// </summary>
        /// <param name="csvContent">Содержимое CSV файла</param>
        /// <param name="createdBy">Кто импортирует</param>
        /// <returns>Количество импортированных контактов</returns>
        Task<ImportResult> ImportContactsFromCsvAsync(string csvContent, string createdBy);
        
        /// <summary>
        /// Экспортировать контакты в CSV
        /// </summary>
        /// <param name="includeInactive">Включить неактивные контакты</param>
        Task<string> ExportContactsToCsvAsync(bool includeInactive = false);
        
        /// <summary>
        /// Проверить существование контакта с таким email
        /// </summary>
        Task<bool> EmailExistsAsync(string email, int? excludeId = null);
    }
    
    /// <summary>
    /// Результат импорта контактов
    /// </summary>
    public class ImportResult
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyImported { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        
        public bool IsSuccess => Errors == 0 && SuccessfullyImported > 0;
    }
}
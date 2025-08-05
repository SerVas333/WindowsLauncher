using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Репозиторий для работы с контактами адресной книги
    /// Использует единую архитектуру БД приложения (SQLite/Firebird)
    /// </summary>
    public interface IContactRepository
    {
        /// <summary>
        /// Получить все контакты
        /// </summary>
        /// <param name="includeInactive">Включить неактивные контакты</param>
        Task<IReadOnlyList<Contact>> GetAllAsync(bool includeInactive = false);
        
        /// <summary>
        /// Получить контакт по ID
        /// </summary>
        Task<Contact?> GetByIdAsync(int id);
        
        /// <summary>
        /// Получить контакт по email
        /// </summary>
        Task<Contact?> GetByEmailAsync(string email);
        
        /// <summary>
        /// Найти контакты по поисковому запросу
        /// </summary>
        /// <param name="searchTerm">Поиск по имени, email, компании</param>
        /// <param name="includeInactive">Включить неактивные контакты</param>
        Task<IReadOnlyList<Contact>> SearchAsync(string searchTerm, bool includeInactive = false);
        
        /// <summary>
        /// Получить контакты по группе
        /// </summary>
        /// <param name="group">Название группы (null для контактов без группы)</param>
        /// <param name="includeInactive">Включить неактивные контакты</param>
        Task<IReadOnlyList<Contact>> GetByGroupAsync(string? group, bool includeInactive = false);
        
        /// <summary>
        /// Создать новый контакт
        /// </summary>
        Task<Contact> CreateAsync(Contact contact);
        
        /// <summary>
        /// Обновить контакт
        /// </summary>
        Task<Contact> UpdateAsync(Contact contact);
        
        /// <summary>
        /// Удалить контакт физически
        /// </summary>
        Task<bool> DeleteAsync(int id);
        
        /// <summary>
        /// Получить все используемые группы
        /// </summary>
        Task<IReadOnlyList<string>> GetAllGroupsAsync();
        
        /// <summary>
        /// Проверить существование email
        /// </summary>
        /// <param name="email">Email для проверки</param>
        /// <param name="excludeId">ID контакта для исключения (при обновлении)</param>
        Task<bool> EmailExistsAsync(string email, int? excludeId = null);
        
        /// <summary>
        /// Массовое создание контактов (для импорта)
        /// </summary>
        Task<int> CreateBatchAsync(IEnumerable<Contact> contacts);
    }
}
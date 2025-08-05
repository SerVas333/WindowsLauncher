using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Email;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Services.Email
{
    /// <summary>
    /// Сервис для управления локальной адресной книгой
    /// Предоставляет высокоуровневую логику для работы с контактами, включая импорт/экспорт CSV
    /// </summary>
    public class AddressBookService : IAddressBookService
    {
        private readonly IContactRepository _contactRepository;
        private readonly ILogger<AddressBookService> _logger;
        
        public AddressBookService(
            IContactRepository contactRepository,
            ILogger<AddressBookService> logger)
        {
            _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Получить все активные контакты
        /// </summary>
        public async Task<IReadOnlyList<Contact>> GetAllContactsAsync()
        {
            try
            {
                var contacts = await _contactRepository.GetAllAsync(includeInactive: false);
                _logger.LogDebug("Retrieved {Count} active contacts", contacts.Count);
                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all contacts");
                throw;
            }
        }
        
        /// <summary>
        /// Получить контакты по группе
        /// </summary>
        public async Task<IReadOnlyList<Contact>> GetContactsByGroupAsync(string? group)
        {
            try
            {
                var contacts = await _contactRepository.GetByGroupAsync(group, includeInactive: false);
                _logger.LogDebug("Retrieved {Count} contacts from group '{Group}'", contacts.Count, group ?? "No Group");
                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contacts by group '{Group}'", group);
                throw;
            }
        }
        
        /// <summary>
        /// Найти контакты по имени или email
        /// </summary>
        public async Task<IReadOnlyList<Contact>> SearchContactsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Contact>().AsReadOnly();
            
            try
            {
                var contacts = await _contactRepository.SearchAsync(searchTerm, includeInactive: false);
                _logger.LogDebug("Found {Count} contacts matching '{SearchTerm}'", contacts.Count, searchTerm);
                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }
        
        /// <summary>
        /// Получить контакт по ID
        /// </summary>
        public async Task<Contact?> GetContactByIdAsync(int id)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(id);
                if (contact != null && !contact.IsActive)
                {
                    _logger.LogDebug("Contact {Id} found but is inactive", id);
                    return null; // Не возвращаем неактивные контакты
                }
                return contact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact by ID {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Получить контакт по email адресу
        /// </summary>
        public async Task<Contact?> GetContactByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;
            
            try
            {
                var contact = await _contactRepository.GetByEmailAsync(email);
                return contact; // Repository уже фильтрует по IsActive
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact by email '{Email}'", email);
                throw;
            }
        }
        
        /// <summary>
        /// Создать новый контакт с валидацией
        /// </summary>
        public async Task<Contact> CreateContactAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            
            // Валидация данных
            await ValidateContactAsync(contact);
            
            try
            {
                // Проверяем уникальность email
                if (await _contactRepository.EmailExistsAsync(contact.Email))
                {
                    throw new InvalidOperationException($"Контакт с email '{contact.Email}' уже существует");
                }
                
                // Нормализуем данные
                NormalizeContactData(contact);
                
                var createdContact = await _contactRepository.CreateAsync(contact);
                
                _logger.LogInformation("Created new contact: {Name} ({Email}) in group '{Group}'", 
                    createdContact.Name, createdContact.Email, createdContact.Group ?? "No Group");
                
                return createdContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact {Name} ({Email})", contact.Name, contact.Email);
                throw;
            }
        }
        
        /// <summary>
        /// Обновить существующий контакт
        /// </summary>
        public async Task<Contact> UpdateContactAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            
            // Валидация данных
            await ValidateContactAsync(contact);
            
            try
            {
                // Проверяем существование контакта
                var existingContact = await _contactRepository.GetByIdAsync(contact.Id);
                if (existingContact == null)
                {
                    throw new InvalidOperationException($"Контакт с ID {contact.Id} не найден");
                }
                
                // Нормализуем данные
                NormalizeContactData(contact);
                
                var updatedContact = await _contactRepository.UpdateAsync(contact);
                
                _logger.LogInformation("Updated contact {Id}: {Name} ({Email})", 
                    updatedContact.Id, updatedContact.Name, updatedContact.Email);
                
                return updatedContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact {Id}", contact.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Удалить контакт (мягкое удаление - IsActive = false)
        /// </summary>
        public async Task<bool> DeleteContactAsync(int id)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(id);
                if (contact == null)
                {
                    _logger.LogWarning("Contact {Id} not found for deletion", id);
                    return false;
                }
                
                // Мягкое удаление - устанавливаем IsActive = false
                contact.IsActive = false;
                contact.UpdatedAt = DateTime.Now;
                
                await _contactRepository.UpdateAsync(contact);
                
                _logger.LogInformation("Soft deleted contact {Id}: {Name} ({Email})", 
                    id, contact.Name, contact.Email);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Восстановить удаленный контакт
        /// </summary>
        public async Task<bool> RestoreContactAsync(int id)
        {
            try
            {
                // Получаем контакт включая неактивные
                var allContacts = await _contactRepository.GetAllAsync(includeInactive: true);
                var contact = allContacts.FirstOrDefault(c => c.Id == id);
                
                if (contact == null)
                {
                    _logger.LogWarning("Contact {Id} not found for restoration", id);
                    return false;
                }
                
                if (contact.IsActive)
                {
                    _logger.LogDebug("Contact {Id} is already active", id);
                    return true;
                }
                
                // Проверяем конфликт email при восстановлении
                if (await _contactRepository.EmailExistsAsync(contact.Email, contact.Id))
                {
                    throw new InvalidOperationException($"Нельзя восстановить контакт: email '{contact.Email}' уже используется другим активным контактом");
                }
                
                // Восстанавливаем контакт
                contact.IsActive = true;
                contact.UpdatedAt = DateTime.Now;
                
                await _contactRepository.UpdateAsync(contact);
                
                _logger.LogInformation("Restored contact {Id}: {Name} ({Email})", 
                    id, contact.Name, contact.Email);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring contact {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Получить все используемые группы
        /// </summary>
        public async Task<IReadOnlyList<string>> GetAllGroupsAsync()
        {
            try
            {
                var groups = await _contactRepository.GetAllGroupsAsync();
                _logger.LogDebug("Retrieved {Count} contact groups", groups.Count);
                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact groups");
                throw;
            }
        }
        
        /// <summary>
        /// Импортировать контакты из CSV
        /// Формат: Name,Email,Company,Group,Notes
        /// </summary>
        public async Task<ImportResult> ImportContactsFromCsvAsync(string csvContent, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(csvContent))
                throw new ArgumentException("CSV content cannot be empty", nameof(csvContent));
            
            if (string.IsNullOrWhiteSpace(createdBy))
                throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
            
            var result = new ImportResult();
            var contactsToImport = new List<Contact>();
            
            try
            {
                var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Пропускаем заголовок если он есть
                var startIndex = 0;
                if (lines.Length > 0 && IsHeaderLine(lines[0]))
                {
                    startIndex = 1;
                    _logger.LogDebug("Detected CSV header, skipping first line");
                }
                
                for (int i = startIndex; i < lines.Length; i++)
                {
                    var lineNumber = i + 1;
                    result.TotalProcessed++;
                    
                    try
                    {
                        var contact = ParseCsvLine(lines[i], createdBy);
                        if (contact != null)
                        {
                            // Проверяем дубликаты в импортируемых данных
                            if (contactsToImport.Any(c => string.Equals(c.Email, contact.Email, StringComparison.OrdinalIgnoreCase)))
                            {
                                result.Skipped++;
                                result.ErrorMessages.Add($"Строка {lineNumber}: Дублирующийся email '{contact.Email}' в импортируемых данных");
                                continue;
                            }
                            
                            // Проверяем существование в БД
                            if (await _contactRepository.EmailExistsAsync(contact.Email))
                            {
                                result.Skipped++;
                                result.ErrorMessages.Add($"Строка {lineNumber}: Контакт с email '{contact.Email}' уже существует в базе");
                                continue;
                            }
                            
                            contactsToImport.Add(contact);
                        }
                        else
                        {
                            result.Skipped++;
                            result.ErrorMessages.Add($"Строка {lineNumber}: Пустая или некорректная строка");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Строка {lineNumber}: {ex.Message}");
                        _logger.LogWarning(ex, "Error parsing CSV line {LineNumber}", lineNumber);
                    }
                }
                
                // Массовое создание контактов
                if (contactsToImport.Count > 0)
                {
                    var importedCount = await _contactRepository.CreateBatchAsync(contactsToImport);
                    result.SuccessfullyImported = importedCount;
                    
                    _logger.LogInformation("Successfully imported {Count} contacts from CSV by {CreatedBy}", 
                        importedCount, createdBy);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contacts from CSV");
                result.Errors++;
                result.ErrorMessages.Add($"Общая ошибка импорта: {ex.Message}");
                return result;
            }
        }
        
        /// <summary>
        /// Экспортировать контакты в CSV
        /// </summary>
        public async Task<string> ExportContactsToCsvAsync(bool includeInactive = false)
        {
            try
            {
                var contacts = await _contactRepository.GetAllAsync(includeInactive);
                
                var csv = new StringBuilder();
                
                // Заголовок CSV
                csv.AppendLine("Name,Email,Company,Group,Notes,IsActive,CreatedAt");
                
                // Данные контактов
                foreach (var contact in contacts)
                {
                    var line = string.Join(",", 
                        EscapeCsvField(contact.Name),
                        EscapeCsvField(contact.Email),
                        EscapeCsvField(contact.Company ?? ""),
                        EscapeCsvField(contact.Group ?? ""),
                        EscapeCsvField(contact.Notes ?? ""),
                        contact.IsActive ? "1" : "0",
                        contact.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    );
                    
                    csv.AppendLine(line);
                }
                
                _logger.LogInformation("Exported {Count} contacts to CSV (includeInactive: {IncludeInactive})", 
                    contacts.Count, includeInactive);
                
                return csv.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting contacts to CSV");
                throw;
            }
        }
        
        /// <summary>
        /// Проверить существование контакта с таким email
        /// </summary>
        public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
        {
            try
            {
                return await _contactRepository.EmailExistsAsync(email, excludeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence '{Email}'", email);
                throw;
            }
        }
        
        #region Private Helper Methods
        
        /// <summary>
        /// Валидация данных контакта
        /// </summary>
        private async Task ValidateContactAsync(Contact contact)
        {
            var errors = new List<string>();
            
            // Проверка имени
            if (string.IsNullOrWhiteSpace(contact.Name))
                errors.Add("Имя контакта обязательно для заполнения");
            else if (contact.Name.Length > 200)
                errors.Add("Имя контакта не может быть длиннее 200 символов");
            
            // Проверка email
            if (string.IsNullOrWhiteSpace(contact.Email))
                errors.Add("Email обязателен для заполнения");
            else if (contact.Email.Length > 250)
                errors.Add("Email не может быть длиннее 250 символов");
            else if (!IsValidEmail(contact.Email))
                errors.Add("Некорректный формат email адреса");
            
            // Проверка дополнительных полей
            if (!string.IsNullOrEmpty(contact.Company) && contact.Company.Length > 200)
                errors.Add("Название компании не может быть длиннее 200 символов");
            
            if (!string.IsNullOrEmpty(contact.Group) && contact.Group.Length > 100)
                errors.Add("Название группы не может быть длиннее 100 символов");
            
            if (!string.IsNullOrEmpty(contact.Notes) && contact.Notes.Length > 1000)
                errors.Add("Заметки не могут быть длиннее 1000 символов");
            
            if (errors.Count > 0)
            {
                var errorMessage = string.Join("; ", errors);
                throw new ArgumentException($"Ошибки валидации: {errorMessage}");
            }
        }
        
        /// <summary>
        /// Нормализация данных контакта
        /// </summary>
        private void NormalizeContactData(Contact contact)
        {
            contact.Name = contact.Name?.Trim() ?? "";
            contact.Email = contact.Email?.Trim().ToLowerInvariant() ?? "";
            contact.Company = string.IsNullOrWhiteSpace(contact.Company) ? null : contact.Company.Trim();
            contact.Group = string.IsNullOrWhiteSpace(contact.Group) ? null : contact.Group.Trim();
            contact.Notes = string.IsNullOrWhiteSpace(contact.Notes) ? null : contact.Notes.Trim();
        }
        
        /// <summary>
        /// Проверка корректности email
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Проверка является ли строка заголовком CSV
        /// </summary>
        private bool IsHeaderLine(string line)
        {
            var normalizedLine = line.ToLowerInvariant();
            return normalizedLine.Contains("name") && normalizedLine.Contains("email");
        }
        
        /// <summary>
        /// Парсинг строки CSV в контакт
        /// </summary>
        private Contact? ParseCsvLine(string line, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;
            
            var fields = ParseCsvFields(line);
            
            if (fields.Count < 2) // Минимум Name и Email
                return null;
            
            var name = fields[0]?.Trim();
            var email = fields[1]?.Trim();
            
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                return null;
            
            var contact = new Contact
            {
                Name = name,
                Email = email.ToLowerInvariant(),
                Company = fields.Count > 2 ? fields[2]?.Trim() : null,
                Group = fields.Count > 3 ? fields[3]?.Trim() : null,
                Notes = fields.Count > 4 ? fields[4]?.Trim() : null,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            
            // Очищаем пустые строки
            if (string.IsNullOrWhiteSpace(contact.Company)) contact.Company = null;
            if (string.IsNullOrWhiteSpace(contact.Group)) contact.Group = null;
            if (string.IsNullOrWhiteSpace(contact.Notes)) contact.Notes = null;
            
            return contact;
        }
        
        /// <summary>
        /// Парсинг полей CSV с учетом кавычек
        /// </summary>
        private List<string> ParseCsvFields(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            var inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                
                if (c == '"' && !inQuotes)
                {
                    inQuotes = true;
                }
                else if (c == '"' && inQuotes)
                {
                    // Проверяем двойные кавычки
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Пропускаем следующую кавычку
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            
            fields.Add(currentField.ToString());
            return fields;
        }
        
        /// <summary>
        /// Экранирование поля для CSV
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";
            
            // Если поле содержит запятую, кавычки или переносы строк - экранируем
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                // Заменяем двойные кавычки на двойные двойные кавычки
                var escaped = field.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }
            
            return field;
        }
        
        #endregion
    }
}
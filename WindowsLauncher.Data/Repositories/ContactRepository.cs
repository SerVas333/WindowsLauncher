using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.Data;

namespace WindowsLauncher.Data.Repositories
{
    /// <summary>
    /// Репозиторий для работы с контактами
    /// Поддерживает SQLite и Firebird БД
    /// </summary>
    public class ContactRepository : IContactRepository
    {
        private readonly LauncherDbContext _context;
        private readonly ILogger<ContactRepository> _logger;
        
        public ContactRepository(LauncherDbContext context, ILogger<ContactRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Получить все контакты
        /// </summary>
        public async Task<IReadOnlyList<Contact>> GetAllAsync(bool includeInactive = false)
        {
            try
            {
                var query = _context.Contacts.AsQueryable();
                
                if (!includeInactive)
                {
                    query = query.Where(c => c.IsActive);
                }
                
                // Используем совместимые с обеими БД методы сортировки
                var contacts = await query
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ThenBy(c => c.Email)
                    .ToListAsync();
                
                _logger.LogDebug("Retrieved {Count} contacts (includeInactive: {IncludeInactive})", 
                    contacts.Count, includeInactive);
                
                return contacts.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all contacts");
                throw;
            }
        }
        
        /// <summary>
        /// Получить контакт по ID
        /// </summary>
        public async Task<Contact?> GetByIdAsync(int id)
        {
            try
            {
                var contact = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.Id == id);
                
                if (contact != null)
                {
                    _logger.LogDebug("Found contact {Id}: {FullName} ({Email})", id, contact.FullName, contact.Email);
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
        /// Получить контакт по email
        /// </summary>
        public async Task<Contact?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;
            
            try
            {
                // Используем ToUpper() для совместимости с обеими БД (UPPERCASE поля)
                var normalizedEmail = email.Trim().ToUpper();
                
                var contact = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.Email.ToUpper() == normalizedEmail && c.IsActive);
                
                if (contact != null)
                {
                    _logger.LogDebug("Found contact by email {Email}: {FullName}", email, contact.FullName);
                }
                
                return contact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact by email {Email}", email);
                throw;
            }
        }
        
        /// <summary>
        /// Поиск контактов
        /// </summary>
        public async Task<IReadOnlyList<Contact>> SearchAsync(string searchTerm, bool includeInactive = false)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Contact>().AsReadOnly();
            
            try
            {
                var normalizedSearch = searchTerm.Trim().ToUpper();
                
                var query = _context.Contacts.AsQueryable();
                
                if (!includeInactive)
                {
                    query = query.Where(c => c.IsActive);
                }
                
                // Поиск по имени, фамилии, email и компании (совместимо с SQLite и Firebird)
                query = query.Where(c => 
                    c.FirstName.ToUpper().Contains(normalizedSearch) ||
                    c.LastName.ToUpper().Contains(normalizedSearch) ||
                    c.Email.ToUpper().Contains(normalizedSearch) ||
                    (c.Company != null && c.Company.ToUpper().Contains(normalizedSearch)) ||
                    (c.Department != null && c.Department.ToUpper().Contains(normalizedSearch)));
                
                var contacts = await query
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();
                
                _logger.LogDebug("Found {Count} contacts matching '{SearchTerm}'", contacts.Count, searchTerm);
                
                return contacts.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }
        
        /// <summary>
        /// Получить контакты по группе
        /// </summary>
        public async Task<IReadOnlyList<Contact>> GetByGroupAsync(string? group, bool includeInactive = false)
        {
            try
            {
                var query = _context.Contacts.AsQueryable();
                
                if (!includeInactive)
                {
                    query = query.Where(c => c.IsActive);
                }
                
                if (string.IsNullOrWhiteSpace(group))
                {
                    // Контакты без группы
                    query = query.Where(c => c.Group == null || c.Group == "");
                }
                else
                {
                    // Контакты определенной группы
                    var normalizedGroup = group.Trim().ToUpper();
                    query = query.Where(c => c.Group != null && c.Group.ToUpper() == normalizedGroup);
                }
                
                var contacts = await query
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();
                
                _logger.LogDebug("Found {Count} contacts in group '{Group}'", contacts.Count, group ?? "No Group");
                
                return contacts.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contacts by group '{Group}'", group);
                throw;
            }
        }
        
        /// <summary>
        /// Создать новый контакт
        /// </summary>
        public async Task<Contact> CreateAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            
            try
            {
                // Проверяем уникальность email
                var existingContact = await GetByEmailAsync(contact.Email);
                if (existingContact != null)
                {
                    throw new InvalidOperationException($"Контакт с email '{contact.Email}' уже существует");
                }
                
                // Устанавливаем даты
                contact.CreatedAt = DateTime.Now;
                contact.UpdatedAt = null;
                
                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created new contact {Id}: {FullName} ({Email})", 
                    contact.Id, contact.FullName, contact.Email);
                
                return contact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact {FullName} ({Email})", contact.FullName, contact.Email);
                throw;
            }
        }
        
        /// <summary>
        /// Обновить контакт
        /// </summary>
        public async Task<Contact> UpdateAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            
            try
            {
                var existingContact = await GetByIdAsync(contact.Id);
                if (existingContact == null)
                {
                    throw new InvalidOperationException($"Контакт с ID {contact.Id} не найден");
                }
                
                // Проверяем уникальность email (исключая текущий контакт)
                var emailExists = await EmailExistsAsync(contact.Email, contact.Id);
                if (emailExists)
                {
                    throw new InvalidOperationException($"Контакт с email '{contact.Email}' уже существует");
                }
                
                // Обновляем поля
                existingContact.FirstName = contact.FirstName;
                existingContact.LastName = contact.LastName;
                existingContact.Email = contact.Email;
                existingContact.Phone = contact.Phone;
                existingContact.Company = contact.Company;
                existingContact.Department = contact.Department;
                existingContact.Group = contact.Group;
                existingContact.Notes = contact.Notes;
                existingContact.IsActive = contact.IsActive;
                existingContact.CreatedBy = contact.CreatedBy;
                existingContact.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated contact {Id}: {FullName} ({Email})", 
                    contact.Id, contact.FullName, contact.Email);
                
                return existingContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact {Id}", contact.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Удалить контакт физически
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var contact = await GetByIdAsync(id);
                if (contact == null)
                {
                    _logger.LogWarning("Contact {Id} not found for deletion", id);
                    return false;
                }
                
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted contact {Id}: {FullName} ({Email})", 
                    id, contact.FullName, contact.Email);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Получить все группы
        /// </summary>
        public async Task<IReadOnlyList<string>> GetAllGroupsAsync()
        {
            try
            {
                // Используем совместимый с обеими БД запрос
                var groups = await _context.Contacts
                    .Where(c => c.IsActive && c.Group != null && c.Group != "")
                    .Select(c => c.Group!)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToListAsync();
                
                _logger.LogDebug("Found {Count} contact groups", groups.Count);
                
                return groups.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact groups");
                throw;
            }
        }
        
        /// <summary>
        /// Проверить существование email
        /// </summary>
        public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            
            try
            {
                var normalizedEmail = email.Trim().ToUpper();
                
                var query = _context.Contacts
                    .Where(c => c.Email.ToUpper() == normalizedEmail && c.IsActive);
                
                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.Id != excludeId.Value);
                }
                
                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence {Email}", email);
                throw;
            }
        }
        
        /// <summary>
        /// Массовое создание контактов
        /// </summary>
        public async Task<int> CreateBatchAsync(IEnumerable<Contact> contacts)
        {
            if (contacts == null)
                throw new ArgumentNullException(nameof(contacts));
            
            try
            {
                var contactList = contacts.ToList();
                if (contactList.Count == 0)
                    return 0;
                
                // Устанавливаем даты для всех контактов
                var now = DateTime.Now;
                foreach (var contact in contactList)
                {
                    contact.CreatedAt = now;
                    contact.UpdatedAt = null;
                }
                
                _context.Contacts.AddRange(contactList);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created batch of {Count} contacts", contactList.Count);
                
                return contactList.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating batch of contacts");
                throw;
            }
        }
    }
}
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
    /// Репозиторий для управления настройками SMTP серверов
    /// Поддерживает SQLite и Firebird БД
    /// </summary>
    public class SmtpSettingsRepository : ISmtpSettingsRepository
    {
        private readonly IDbContextFactory<LauncherDbContext> _contextFactory;
        private readonly ILogger<SmtpSettingsRepository> _logger;
        
        public SmtpSettingsRepository(IDbContextFactory<LauncherDbContext> contextFactory, ILogger<SmtpSettingsRepository> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Получить все активные настройки SMTP
        /// </summary>
        public async Task<IReadOnlyList<SmtpSettings>> GetActiveSettingsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SmtpSettings
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.ServerType) // Primary first, then Backup
                    .ToListAsync();
                
                _logger.LogDebug("Retrieved {Count} active SMTP settings", settings.Count);
                
                return settings.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active SMTP settings");
                throw;
            }
        }
        
        /// <summary>
        /// Получить настройки SMTP по ID
        /// </summary>
        public async Task<SmtpSettings?> GetByIdAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SmtpSettings
                    .FirstOrDefaultAsync(s => s.Id == id);
                
                if (settings != null)
                {
                    _logger.LogDebug("Found SMTP settings {Id}: {Host}:{Port} ({ServerType})", 
                        id, settings.Host, settings.Port, settings.ServerType);
                }
                
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMTP settings by ID {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Получить настройки по типу сервера
        /// </summary>
        public async Task<SmtpSettings?> GetByTypeAsync(SmtpServerType serverType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var settings = await context.SmtpSettings
                    .FirstOrDefaultAsync(s => s.ServerType == serverType && s.IsActive);
                
                if (settings != null)
                {
                    _logger.LogDebug("Found {ServerType} SMTP settings: {Host}:{Port}", 
                        serverType, settings.Host, settings.Port);
                }
                else
                {
                    _logger.LogDebug("No active {ServerType} SMTP settings found", serverType);
                }
                
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMTP settings by type {ServerType}", serverType);
                throw;
            }
        }
        
        /// <summary>
        /// Создать новые настройки SMTP
        /// </summary>
        public async Task<SmtpSettings> CreateAsync(SmtpSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            try
            {
                // Проверяем, что нет конфликта типов серверов
                await ValidateServerTypeAsync(settings, isUpdate: false);
                
                // Устанавливаем даты
                settings.CreatedAt = DateTime.Now;
                settings.UpdatedAt = null;
                
                using var context = await _contextFactory.CreateDbContextAsync();
                context.SmtpSettings.Add(settings);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Created new {ServerType} SMTP settings {Id}: {Host}:{Port}", 
                    settings.ServerType, settings.Id, settings.Host, settings.Port);
                
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SMTP settings {Host}:{Port} ({ServerType})", 
                    settings.Host, settings.Port, settings.ServerType);
                throw;
            }
        }
        
        /// <summary>
        /// Обновить настройки SMTP
        /// </summary>
        public async Task<SmtpSettings> UpdateAsync(SmtpSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var existingSettings = await context.SmtpSettings.FirstOrDefaultAsync(s => s.Id == settings.Id);
                if (existingSettings == null)
                {
                    throw new InvalidOperationException($"SMTP настройки с ID {settings.Id} не найдены");
                }
                
                // Проверяем конфликт типов серверов (только если тип изменился)
                if (existingSettings.ServerType != settings.ServerType)
                {
                    var hasConflict = await context.SmtpSettings
                        .AnyAsync(s => s.ServerType == settings.ServerType && s.IsActive && s.Id != settings.Id);
                    if (hasConflict)
                    {
                        throw new InvalidOperationException($"Активный {settings.ServerType} сервер уже существует");
                    }
                }
                
                // Обновляем поля
                existingSettings.Host = settings.Host;
                existingSettings.Port = settings.Port;
                existingSettings.Username = settings.Username;
                existingSettings.EncryptedPassword = settings.EncryptedPassword;
                existingSettings.UseSSL = settings.UseSSL;
                existingSettings.UseStartTLS = settings.UseStartTLS;
                existingSettings.ServerType = settings.ServerType;
                existingSettings.DefaultFromEmail = settings.DefaultFromEmail;
                existingSettings.DefaultFromName = settings.DefaultFromName;
                existingSettings.IsActive = settings.IsActive;
                existingSettings.ConsecutiveErrors = settings.ConsecutiveErrors;
                existingSettings.LastSuccessfulSend = settings.LastSuccessfulSend;
                existingSettings.UpdatedAt = DateTime.Now;
                
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Updated {ServerType} SMTP settings {Id}: {Host}:{Port}", 
                    settings.ServerType, settings.Id, settings.Host, settings.Port);
                
                return existingSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SMTP settings {Id}", settings.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Удалить настройки SMTP
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var settings = await context.SmtpSettings.FirstOrDefaultAsync(s => s.Id == id);
                if (settings == null)
                {
                    _logger.LogWarning("SMTP settings {Id} not found for deletion", id);
                    return false;
                }
                
                context.SmtpSettings.Remove(settings);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted {ServerType} SMTP settings {Id}: {Host}:{Port}", 
                    settings.ServerType, id, settings.Host, settings.Port);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SMTP settings {Id}", id);
                throw;
            }
        }
        
        /// <summary>
        /// Проверить существование основного сервера
        /// </summary>
        public async Task<bool> HasPrimaryServerAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var hasPrimary = await context.SmtpSettings
                    .AnyAsync(s => s.ServerType == SmtpServerType.Primary && s.IsActive);
                
                _logger.LogDebug("Primary SMTP server exists: {HasPrimary}", hasPrimary);
                
                return hasPrimary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking primary SMTP server existence");
                throw;
            }
        }
        
        /// <summary>
        /// Проверить существование резервного сервера
        /// </summary>
        public async Task<bool> HasBackupServerAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var hasBackup = await context.SmtpSettings
                    .AnyAsync(s => s.ServerType == SmtpServerType.Backup && s.IsActive);
                
                _logger.LogDebug("Backup SMTP server exists: {HasBackup}", hasBackup);
                
                return hasBackup;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking backup SMTP server existence");
                throw;
            }
        }
        
        #region Private Helper Methods
        
        /// <summary>
        /// Валидация типа сервера для предотвращения дубликатов
        /// </summary>
        private async Task ValidateServerTypeAsync(SmtpSettings settings, bool isUpdate)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                // Проверяем, что нет другого активного сервера того же типа
                var query = context.SmtpSettings
                    .Where(s => s.ServerType == settings.ServerType && s.IsActive);
                
                if (isUpdate)
                {
                    // При обновлении исключаем текущую запись
                    query = query.Where(s => s.Id != settings.Id);
                }
                
                var existingServerOfSameType = await query.FirstOrDefaultAsync();
                
                if (existingServerOfSameType != null)
                {
                    throw new InvalidOperationException(
                        $"Активный {settings.ServerType} SMTP сервер уже существует (ID: {existingServerOfSameType.Id}, {existingServerOfSameType.Host}:{existingServerOfSameType.Port}). " +
                        $"Деактивируйте существующий сервер или измените тип.");
                }
                
                // Дополнительная проверка: нельзя создать более 2 серверов вообще
                var totalActiveServers = await context.SmtpSettings
                    .CountAsync(s => s.IsActive && (!isUpdate || s.Id != settings.Id));
                
                if (!isUpdate && totalActiveServers >= 2)
                {
                    throw new InvalidOperationException(
                        "Нельзя создать более 2 активных SMTP серверов. Система поддерживает только один основной и один резервный сервер.");
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Error validating server type for {ServerType}", settings.ServerType);
                throw;
            }
        }
        
        #endregion
    }
}
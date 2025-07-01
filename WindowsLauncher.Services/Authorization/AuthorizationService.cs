// WindowsLauncher.Services/Authorization/AuthorizationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Services.Authorization
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IUserSettingsRepository _settingsRepository;
        private readonly ILogger<AuthorizationService> _logger;
        private readonly IMemoryCache _cache;

        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

        public AuthorizationService(
            IApplicationRepository applicationRepository,
            IUserSettingsRepository settingsRepository,
            ILogger<AuthorizationService> logger,
            IMemoryCache cache)
        {
            _applicationRepository = applicationRepository;
            _settingsRepository = settingsRepository;
            _logger = logger;
            _cache = cache;
        }

        public async Task<bool> CanAccessApplicationAsync(User user, Application application)
        {
            try
            {
                // Проверяем кэш
                var cacheKey = $"access_{user.Username}_{application.Id}";
                if (_cache.TryGetValue(cacheKey, out bool cachedResult))
                {
                    return cachedResult;
                }

                // Проверка активности приложения
                if (!application.IsEnabled)
                {
                    _logger.LogDebug("Application {AppName} is disabled", application.Name);
                    return false;
                }

                // Проверка минимальной роли
                if (user.Role < application.MinimumRole)
                {
                    _logger.LogDebug("User {Username} role {UserRole} is below minimum required {MinRole} for app {AppName}",
                        user.Username, user.Role, application.MinimumRole, application.Name);
                    return false;
                }

                // Если группы не указаны - доступно всем с подходящей ролью
                if (!application.RequiredGroups.Any())
                {
                    _cache.Set(cacheKey, true, _cacheExpiry);
                    return true;
                }

                // Проверка пересечения групп пользователя и требуемых групп
                var hasAccess = application.RequiredGroups.Any(reqGroup =>
                    user.Groups.Contains(reqGroup, StringComparer.OrdinalIgnoreCase));

                _logger.LogDebug("User {Username} access to {AppName}: {HasAccess} (Required: {Required}, User: {UserGroups})",
                    user.Username, application.Name, hasAccess,
                    string.Join(",", application.RequiredGroups),
                    string.Join(",", user.Groups));

                // Кэшируем результат
                _cache.Set(cacheKey, hasAccess, _cacheExpiry);

                return hasAccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking access for user {Username} to application {AppName}",
                    user.Username, application.Name);
                return false;
            }
        }

        public async Task<List<Application>> GetAuthorizedApplicationsAsync(User user)
        {
            try
            {
                var allApplications = await _applicationRepository.GetActiveApplicationsAsync();
                var authorizedApps = new List<Application>();

                foreach (var app in allApplications)
                {
                    if (await CanAccessApplicationAsync(user, app))
                    {
                        authorizedApps.Add(app);
                    }
                }

                _logger.LogInformation("User {Username} has access to {AppCount} applications",
                    user.Username, authorizedApps.Count);

                return authorizedApps.OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authorized applications for user {Username}", user.Username);
                return new List<Application>();
            }
        }

        public async Task<UserSettings> GetUserSettingsAsync(User user)
        {
            try
            {
                var settings = await _settingsRepository.GetByUsernameAsync(user.Username);

                if (settings == null)
                {
                    _logger.LogInformation("Creating default settings for user {Username}", user.Username);
                    settings = CreateDefaultSettings(user);
                    await _settingsRepository.AddAsync(settings);
                    await _settingsRepository.SaveChangesAsync();
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting settings for user {Username}", user.Username);
                return CreateDefaultSettings(user);
            }
        }

        public async Task SaveUserSettingsAsync(UserSettings settings)
        {
            try
            {
                settings.LastModified = DateTime.UtcNow;
                await _settingsRepository.UpdateAsync(settings);
                await _settingsRepository.SaveChangesAsync();

                _logger.LogInformation("Settings saved for user {Username}", settings.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings for user {Username}", settings.Username);
                throw;
            }
        }

        public bool CanManageApplications(User user)
        {
            return user.Role >= UserRole.PowerUser;
        }

        public bool CanViewSystemInfo(User user)
        {
            return user.Role >= UserRole.PowerUser;
        }

        public bool CanViewAuditLogs(User user)
        {
            return user.Role >= UserRole.Administrator;
        }

        public async Task RefreshUserPermissionsAsync(string username)
        {
            try
            {
                // Очищаем кэш прав для пользователя
                var keysToRemove = new List<object>();

                // В реальном приложении здесь бы был более эффективный способ очистки кэша
                // Для простоты просто очищаем весь кэш
                if (_cache is MemoryCache memCache)
                {
                    memCache.Clear();
                }

                _logger.LogInformation("Permissions cache refreshed for user {Username}", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing permissions for user {Username}", username);
            }
        }

        private UserSettings CreateDefaultSettings(User user)
        {
            var settings = new UserSettings
            {
                Username = user.Username,
                Theme = "Light",
                AccentColor = "Blue",
                TileSize = 150,
                ShowCategories = true,
                DefaultCategory = "All",
                AutoRefresh = true,
                RefreshIntervalMinutes = 30,
                ShowDescriptions = true,
                LastModified = DateTime.UtcNow
            };

            // Настройки по ролям
            switch (user.Role)
            {
                case UserRole.Administrator:
                    settings.Theme = "Dark";
                    settings.AccentColor = "Red";
                    settings.TileSize = 180;
                    break;

                case UserRole.PowerUser:
                    settings.AccentColor = "Orange";
                    settings.TileSize = 165;
                    break;

                case UserRole.Standard:
                    settings.HiddenCategories.Add("System");
                    settings.HiddenCategories.Add("Admin");
                    settings.ShowDescriptions = false;
                    break;
            }

            return settings;
        }
    }
}
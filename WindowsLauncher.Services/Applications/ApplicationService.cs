// WindowsLauncher.Services/Applications/ApplicationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Configuration;

namespace WindowsLauncher.Services.Applications
{
    /// <summary>
    /// Обновленный ApplicationService с интеграцией ApplicationLifecycleService
    /// Обеспечивает управление приложениями с современной архитектурой жизненного цикла
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditService _auditService;
        private readonly IApplicationLifecycleService _lifecycleService;
        private readonly ILogger<ApplicationService> _logger;
        private readonly ChromeWindowSearchOptions _chromeWindowSearchOptions;
        private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(3) };

        public event EventHandler<Application>? ApplicationLaunched;

        public ApplicationService(
            IApplicationRepository applicationRepository,
            IAuthorizationService authorizationService,
            IAuditService auditService,
            IApplicationLifecycleService lifecycleService,
            ILogger<ApplicationService> logger,
            IOptions<ChromeWindowSearchOptions>? chromeWindowSearchOptions = null)
        {
            _applicationRepository = applicationRepository ?? throw new ArgumentNullException(nameof(applicationRepository));
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Инициализируем настройки Chrome window search (для legacy совместимости)
            _chromeWindowSearchOptions = chromeWindowSearchOptions?.Value ?? new ChromeWindowSearchOptions();
            _chromeWindowSearchOptions.Validate();
            
            _logger.LogDebug("ApplicationService initialized with ApplicationLifecycleService integration");
        }

        #region Application Launch (New Architecture)

        /// <summary>
        /// Запустить приложение с использованием новой архитектуры ApplicationLifecycleService
        /// </summary>
        public async Task<LaunchResult> LaunchApplicationAsync(Application application, User user)
        {
            try
            {
                _logger.LogInformation("Launch request for application {AppName} by user {Username} (Type: {Type})", 
                    application.Name, user.Username, application.Type);

                // Проверяем права доступа
                var canAccess = await _authorizationService.CanAccessApplicationAsync(user, application);
                if (!canAccess)
                {
                    var errorMsg = $"Access denied to application {application.Name} for user {user.Username}";
                    _logger.LogWarning(errorMsg);
                    await _auditService.LogAccessDeniedAsync(user.Username, application.Name, "Insufficient permissions");
                    return LaunchResult.Failure(errorMsg);
                }

                // ===== НОВАЯ АРХИТЕКТУРА: Используем ApplicationLifecycleService =====
                _logger.LogDebug("Delegating application launch to ApplicationLifecycleService");
                var result = await _lifecycleService.LaunchAsync(application, user.Username);

                // Логируем результат (ApplicationLifecycleService уже логирует, но мы дублируем для совместимости)
                await _auditService.LogApplicationLaunchAsync(
                    application.Id, application.Name, user.Username, result.IsSuccess, result.ErrorMessage);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("User {Username} successfully launched {AppName} via ApplicationLifecycleService",
                        user.Username, application.Name);
                    
                    // Генерируем событие для legacy кода
                    ApplicationLaunched?.Invoke(this, application);
                    
                    // Добавляем информацию о launch duration если доступна
                    if (result.LaunchDuration != TimeSpan.Zero)
                    {
                        _logger.LogDebug("Application {AppName} launched in {Duration}ms", 
                            application.Name, result.LaunchDuration.TotalMilliseconds);
                    }
                }
                else
                {
                    _logger.LogError("Failed to launch application {AppName} for user {Username}: {Error}",
                        application.Name, user.Username, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error launching application {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Failed to launch application {AppName} for user {Username}",
                    application.Name, user.Username);

                await _auditService.LogApplicationLaunchAsync(
                    application.Id, application.Name, user.Username, false, ex.Message);

                return LaunchResult.Failure(errorMsg);
            }
        }

        #endregion

        #region Application Management

        public async Task<List<Application>> GetAllApplicationsAsync()
        {
            try
            {
                return await _applicationRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all applications");
                return new List<Application>();
            }
        }

        public async Task<List<Application>> GetApplicationsByCategoryAsync(string category)
        {
            try
            {
                return await _applicationRepository.GetByCategoryAsync(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting applications by category {Category}", category);
                return new List<Application>();
            }
        }

        public async Task<List<Application>> SearchApplicationsAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetAllApplicationsAsync();

                return await _applicationRepository.SearchAsync(searchTerm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching applications with term {SearchTerm}", searchTerm);
                return new List<Application>();
            }
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            try
            {
                return await _applicationRepository.GetCategoriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return new List<string>();
            }
        }

        public async Task<bool> AddApplicationAsync(Application application, User user)
        {
            try
            {
                if (!_authorizationService.CanManageApplications(user))
                {
                    _logger.LogWarning("User {Username} attempted to add application without permissions", user.Username);
                    await _auditService.LogAccessDeniedAsync(user.Username, "Add Application", "Insufficient permissions");
                    return false;
                }

                // Для ChromeApp с URL - предварительно извлекаем и кэшируем title
                if (application.Type == ApplicationType.ChromeApp)
                {
                    await PreCacheWebTitleForChromeAppAsync(application);
                }

                application.CreatedBy = user.Username;
                application.CreatedDate = DateTime.Now;
                application.ModifiedDate = DateTime.Now;

                await _applicationRepository.AddAsync(application);
                await _applicationRepository.SaveChangesAsync();

                _logger.LogInformation("User {Username} added application {AppName}", user.Username, application.Name);
                await _auditService.LogEventAsync(user.Username, "AddApplication", $"Added application: {application.Name}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding application {AppName}", application.Name);
                return false;
            }
        }

        public async Task<bool> UpdateApplicationAsync(Application application, User user)
        {
            try
            {
                if (!_authorizationService.CanManageApplications(user))
                {
                    _logger.LogWarning("User {Username} attempted to update application without permissions", user.Username);
                    await _auditService.LogAccessDeniedAsync(user.Username, "Update Application", "Insufficient permissions");
                    return false;
                }

                // Для ChromeApp с URL - предварительно извлекаем и кэшируем title
                if (application.Type == ApplicationType.ChromeApp)
                {
                    await PreCacheWebTitleForChromeAppAsync(application);
                }

                application.ModifiedDate = DateTime.Now;

                await _applicationRepository.UpdateAsync(application);
                await _applicationRepository.SaveChangesAsync();

                _logger.LogInformation("User {Username} updated application {AppName}", user.Username, application.Name);
                await _auditService.LogEventAsync(user.Username, "UpdateApplication", $"Updated application: {application.Name}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating application {AppName}", application.Name);
                return false;
            }
        }

        public async Task<bool> DeleteApplicationAsync(int applicationId, User user)
        {
            try
            {
                if (!_authorizationService.CanManageApplications(user))
                {
                    _logger.LogWarning("User {Username} attempted to delete application without permissions", user.Username);
                    await _auditService.LogAccessDeniedAsync(user.Username, "Delete Application", "Insufficient permissions");
                    return false;
                }

                var application = await _applicationRepository.GetByIdAsync(applicationId);
                if (application == null)
                {
                    _logger.LogWarning("Application with ID {ApplicationId} not found for deletion", applicationId);
                    return false;
                }

                await _applicationRepository.DeleteAsync(applicationId);
                await _applicationRepository.SaveChangesAsync();

                _logger.LogInformation("User {Username} deleted application {AppName}", user.Username, application.Name);
                await _auditService.LogEventAsync(user.Username, "DeleteApplication", $"Deleted application: {application.Name}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting application with ID {ApplicationId}", applicationId);
                return false;
            }
        }

        public async Task<List<int>> GetRunningProcessesAsync()
        {
            try
            {
                // Используем новую архитектуру ApplicationLifecycleService
                var runningInstances = await _lifecycleService.GetRunningAsync();
                return runningInstances.Select(instance => instance.ProcessId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting running process IDs");
                return new List<int>();
            }
        }

        #endregion

        #region Legacy Helper Methods

        /// <summary>
        /// Pre-cache web title for Chrome App (сохранен для операций добавления/обновления приложений)
        /// </summary>
        private async Task PreCacheWebTitleForChromeAppAsync(Application application)
        {
            try
            {
                // Извлекаем URL из аргументов --app
                var allArgs = $"{application.ExecutablePath ?? ""} {application.Arguments ?? ""}";
                _logger.LogDebug("Pre-caching: Looking for --app URL in args: '{AllArgs}'", allArgs);
                
                var appArgMatch = Regex.Match(
                    allArgs, @"--app=((?:file:///|https?://)[^\s""]+)", 
                    RegexOptions.IgnoreCase);
                
                if (appArgMatch.Success)
                {
                    var fullUrl = appArgMatch.Groups[1].Value; // Полный URL после --app=
                    
                    _logger.LogDebug("Found Chrome App URL in arguments: {FullUrl}", fullUrl);
                    
                    // Для HTTP/HTTPS URL - кэшируем title с веб-страницы
                    if (fullUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        fullUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Pre-caching web title for Chrome App {AppName}: {FullUrl}", 
                            application.Name, fullUrl);
                        
                        var webTitle = await ExtractWebTitleAsync(fullUrl);
                        if (!string.IsNullOrEmpty(webTitle))
                        {
                            application.Description = $"[CACHED_TITLE]{webTitle}";
                            _logger.LogInformation("Cached web title '{Title}' for Chrome App {AppName}", 
                                webTitle, application.Name);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Chrome App {AppName} uses file or non-HTTP URL, no title caching needed", 
                            application.Name);
                    }
                }
                else
                {
                    _logger.LogDebug("Pre-caching: No --app URL found in args for Chrome App {AppName}", application.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error pre-caching web title for Chrome App {AppName}", application.Name);
                // Не прерываем создание приложения из-за ошибки кэширования
            }
        }

        /// <summary>
        /// Извлечь title из веб-страницы
        /// </summary>
        private async Task<string> ExtractWebTitleAsync(string url)
        {
            try
            {
                _logger.LogDebug("Extracting web title from URL: {Url}", url);
                
                var response = await _httpClient.GetStringAsync(url);
                
                // Ищем <title> тег
                var titleMatch = Regex.Match(response, @"<title[^>]*>(.*?)</title>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (titleMatch.Success)
                {
                    var title = titleMatch.Groups[1].Value.Trim();
                    
                    // Декодируем HTML entities
                    title = System.Net.WebUtility.HtmlDecode(title);
                    
                    _logger.LogDebug("Successfully extracted web title: '{Title}'", title);
                    return title;
                }
                
                _logger.LogDebug("No title tag found in HTML response from {Url}", url);
                return string.Empty;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogWarning(httpEx, "HTTP error extracting web title from {Url}: {Message}", url, httpEx.Message);
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout extracting web title from {Url}", url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting web title from {Url}: {Message}", url, ex.Message);
                return string.Empty;
            }
        }

        #endregion
    }
}
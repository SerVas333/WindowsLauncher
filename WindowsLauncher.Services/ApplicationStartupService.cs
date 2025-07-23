using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Services;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Services.Authentication;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Реализация сервиса управления запуском приложения
    /// </summary>
    public class ApplicationStartupService : IApplicationStartupService
    {
        private readonly ILogger<ApplicationStartupService> _logger;
        private readonly IVersionService _versionService;
        private readonly IDatabaseVersionService _databaseVersionService;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly IAuthenticationConfigurationService _authConfigService;
        
        public ApplicationStartupService(
            ILogger<ApplicationStartupService> logger,
            IVersionService versionService,
            IDatabaseVersionService databaseVersionService,
            IDatabaseConfigurationService dbConfigService,
            IAuthenticationConfigurationService authConfigService)
        {
            _logger = logger;
            _versionService = versionService;
            _databaseVersionService = databaseVersionService;
            _dbConfigService = dbConfigService;
            _authConfigService = authConfigService;
        }
        
        public async Task<StartupAction> DetermineStartupActionAsync()
        {
            _logger.LogInformation("=== DETERMINING STARTUP ACTION ===");
            
            var status = await GetApplicationStatusAsync();
            
            // Логируем текущее состояние
            _logger.LogInformation("Application Status:");
            _logger.LogInformation("- Configuration exists: {ConfigExists}", status.ConfigurationExists);
            _logger.LogInformation("- Database configured: {DbConfigured}", status.DatabaseConfigured);
            _logger.LogInformation("- Database accessible: {DbAccessible}", status.DatabaseAccessible);
            _logger.LogInformation("- Database version current: {DbVersionCurrent}", status.DatabaseVersionCurrent);
            _logger.LogInformation("- Authentication configured: {AuthConfigured}", status.AuthenticationConfigured);
            
            if (status.Issues.Count > 0)
            {
                _logger.LogWarning("Issues found: {Issues}", string.Join(", ", status.Issues));
            }
            
            // Принимаем решение
            if (!status.ConfigurationExists || !status.AuthenticationConfigured)
            {
                _logger.LogInformation("Setup required - missing configuration or authentication");
                return StartupAction.ShowSetup;
            }
            
            if (!status.DatabaseAccessible || !status.DatabaseVersionCurrent)
            {
                _logger.LogInformation("Database migration required");
                return StartupAction.PerformMigrations;
            }
            
            _logger.LogInformation("Application ready - showing login");
            return StartupAction.ShowLogin;
        }
        
        public async Task<bool> IsApplicationReadyAsync()
        {
            var status = await GetApplicationStatusAsync();
            return status.ConfigurationExists && 
                   status.DatabaseConfigured && 
                   status.DatabaseAccessible && 
                   status.DatabaseVersionCurrent && 
                   status.AuthenticationConfigured;
        }
        
        public async Task PrepareApplicationAsync()
        {
            _logger.LogInformation("=== PREPARING APPLICATION ===");
            
            var status = await GetApplicationStatusAsync();
            
            if (!status.DatabaseAccessible)
            {
                _logger.LogInformation("Database not accessible - needs initialization");
                await _databaseVersionService.SetDatabaseVersionAsync(_versionService.GetVersionString());
            }
            
            if (!status.DatabaseVersionCurrent)
            {
                _logger.LogInformation("Updating database from {Current} to {Required}", 
                    status.CurrentDatabaseVersion, status.RequiredDatabaseVersion);
                await _databaseVersionService.SetDatabaseVersionAsync(_versionService.GetVersionString());
            }
            
            _logger.LogInformation("Application preparation completed");
        }
        
        private async Task<ApplicationStatus> GetApplicationStatusAsync()
        {
            var status = new ApplicationStatus();
            var appVersion = _versionService.GetVersionString();
            status.RequiredDatabaseVersion = appVersion;
            
            try
            {
                // Проверяем конфигурацию аутентификации
                var authConfig = _authConfigService.GetConfiguration();
                status.AuthenticationConfigured = authConfig.ServiceAdmin.IsPasswordSet;
                status.ConfigurationExists = true; // Если дошли до сюда, конфиг есть
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Authentication configuration check failed");
                status.ConfigurationExists = false;
                status.AuthenticationConfigured = false;
                status.Issues.Add("Authentication configuration missing or invalid");
            }
            
            try
            {
                // Проверяем конфигурацию БД
                var dbConfig = await _dbConfigService.GetConfigurationAsync();
                status.DatabaseConfigured = true;
                
                // Проверяем доступность БД
                var dbVersion = await _databaseVersionService.GetCurrentDatabaseVersionAsync();
                status.CurrentDatabaseVersion = dbVersion;
                status.DatabaseAccessible = !string.IsNullOrEmpty(dbVersion);
                
                // Сравниваем версии
                if (Version.TryParse(dbVersion, out var dbVer) && Version.TryParse(appVersion, out var appVer))
                {
                    // БД актуальна если версии совпадают или версия БД новее
                    status.DatabaseVersionCurrent = dbVer >= appVer;
                }
                else
                {
                    status.DatabaseVersionCurrent = false;
                    status.Issues.Add($"Version comparison failed: DB={dbVersion}, App={appVersion}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database configuration check failed");
                status.DatabaseConfigured = false;
                status.DatabaseAccessible = false;
                status.DatabaseVersionCurrent = false;
                status.Issues.Add("Database configuration or access failed");
            }
            
            return status;
        }
    }
}
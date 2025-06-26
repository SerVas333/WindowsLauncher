// WindowsLauncher.Services/Applications/ApplicationService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Services.Applications
{
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditService _auditService;
        private readonly ILogger<ApplicationService> _logger;

        public event EventHandler<Application>? ApplicationLaunched;

        public ApplicationService(
            IApplicationRepository applicationRepository,
            IAuthorizationService authorizationService,
            IAuditService auditService,
            ILogger<ApplicationService> logger)
        {
            _applicationRepository = applicationRepository;
            _authorizationService = authorizationService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<LaunchResult> LaunchApplicationAsync(Application application, User user)
        {
            try
            {
                // Проверяем права доступа
                var canAccess = await _authorizationService.CanAccessApplicationAsync(user, application);
                if (!canAccess)
                {
                    var errorMsg = $"Access denied to application {application.Name} for user {user.Username}";
                    _logger.LogWarning(errorMsg);
                    await _auditService.LogAccessDeniedAsync(user.Username, application.Name, "Insufficient permissions");
                    return LaunchResult.Failure(errorMsg);
                }

                // Запускаем приложение в зависимости от типа
                var result = application.Type switch
                {
                    ApplicationType.Desktop => await LaunchDesktopApplicationAsync(application),
                    ApplicationType.Web => await LaunchWebApplicationAsync(application),
                    ApplicationType.Folder => await LaunchFolderAsync(application),
                    _ => LaunchResult.Failure("Unsupported application type")
                };

                // Логируем результат
                await _auditService.LogApplicationLaunchAsync(
                    application.Id, application.Name, user.Username, result.IsSuccess, result.ErrorMessage);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("User {Username} successfully launched {AppName}",
                        user.Username, application.Name);
                    ApplicationLaunched?.Invoke(this, application);
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

                var app = await _applicationRepository.GetByIdAsync(applicationId);
                if (app == null)
                {
                    _logger.LogWarning("Application with ID {AppId} not found", applicationId);
                    return false;
                }

                await _applicationRepository.DeleteAsync(applicationId);
                await _applicationRepository.SaveChangesAsync();

                _logger.LogInformation("User {Username} deleted application {AppName}", user.Username, app.Name);
                await _auditService.LogEventAsync(user.Username, "DeleteApplication", $"Deleted application: {app.Name}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting application {AppId}", applicationId);
                return false;
            }
        }

        public async Task<List<int>> GetRunningProcessesAsync()
        {
            try
            {
                var processes = Process.GetProcesses();
                return processes.Select(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting running processes");
                return new List<int>();
            }
        }

        private async Task<LaunchResult> LaunchDesktopApplicationAsync(Application app)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    Arguments = app.Arguments ?? "",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(app.ExecutablePath) ?? ""
                };

                // Проверяем существование файла
                if (!File.Exists(app.ExecutablePath) && !app.ExecutablePath.EndsWith(".exe"))
                {
                    // Возможно это системная команда (calc, notepad и т.д.)
                    _logger.LogDebug("File {Path} not found, trying as system command", app.ExecutablePath);
                }

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchResult.Failure("Failed to start process");
                }

                _logger.LogDebug("Desktop application {AppName} launched with PID {ProcessId}",
                    app.Name, process.Id);

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching desktop application {AppName}", app.Name);
                return LaunchResult.Failure(ex.Message);
            }
        }

        private async Task<LaunchResult> LaunchWebApplicationAsync(Application app)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath, // URL
                    UseShellExecute = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchResult.Failure("Failed to open URL in browser");
                }

                _logger.LogDebug("Web application {AppName} opened URL {Url}", app.Name, app.ExecutablePath);

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching web application {AppName}", app.Name);
                return LaunchResult.Failure(ex.Message);
            }
        }

        private async Task<LaunchResult> LaunchFolderAsync(Application app)
        {
            try
            {
                if (!Directory.Exists(app.ExecutablePath))
                {
                    return LaunchResult.Failure($"Folder not found: {app.ExecutablePath}");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{app.ExecutablePath}\"",
                    UseShellExecute = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchResult.Failure("Failed to open folder in explorer");
                }

                _logger.LogDebug("Folder {AppName} opened path {Path}", app.Name, app.ExecutablePath);

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching folder {AppName}", app.Name);
                return LaunchResult.Failure(ex.Message);
            }
        }
    }
}
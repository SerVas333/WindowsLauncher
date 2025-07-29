// WindowsLauncher.Services/Applications/ApplicationService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Configuration;
using WindowsLauncher.Services.Utilities;

namespace WindowsLauncher.Services.Applications
{
    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditService _auditService;
        private readonly IRunningApplicationsService _runningApplicationsService;
        private readonly ILogger<ApplicationService> _logger;
        private readonly ChromeWindowSearchOptions _chromeWindowSearchOptions;
        private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(3) };

        public event EventHandler<Application>? ApplicationLaunched;

        public ApplicationService(
            IApplicationRepository applicationRepository,
            IAuthorizationService authorizationService,
            IAuditService auditService,
            IRunningApplicationsService runningApplicationsService,
            ILogger<ApplicationService> logger,
            IOptions<ChromeWindowSearchOptions>? chromeWindowSearchOptions = null)
        {
            _applicationRepository = applicationRepository;
            _authorizationService = authorizationService;
            _auditService = auditService;
            _runningApplicationsService = runningApplicationsService;
            _logger = logger;
            
            // Инициализируем настройки Chrome window search
            _chromeWindowSearchOptions = chromeWindowSearchOptions?.Value ?? new ChromeWindowSearchOptions();
            _chromeWindowSearchOptions.Validate();
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
                    ApplicationType.Desktop => await LaunchDesktopApplicationAsync(application, user.Username),
                    ApplicationType.Web => await LaunchWebApplicationAsync(application, user.Username),
                    ApplicationType.Folder => await LaunchFolderAsync(application, user.Username),
                    ApplicationType.ChromeApp => await LaunchChromeAppAsync(application, user.Username),
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

        private async Task<LaunchResult> LaunchDesktopApplicationAsync(Application app, string launchedBy)
        {
            try
            {
                // Парсим ExecutablePath для разделения пути к файлу и аргументов
                var (executablePath, executableArgs) = ParseExecutablePath(app.ExecutablePath);
                
                // Объединяем аргументы из ExecutablePath и из поля Arguments
                var combinedArguments = string.IsNullOrEmpty(executableArgs) 
                    ? (app.Arguments ?? "")
                    : string.IsNullOrEmpty(app.Arguments) 
                        ? executableArgs
                        : $"{executableArgs} {app.Arguments}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = combinedArguments,
                    UseShellExecute = true
                };

                _logger.LogDebug("Parsed executable: '{Path}' with arguments: '{Args}'", executablePath, combinedArguments);

                // Определяем рабочую директорию
                if (File.Exists(executablePath))
                {
                    // Если файл существует, используем его директорию
                    startInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "";
                    _logger.LogDebug("Launching existing file {Path}", executablePath);
                }
                else
                {
                    // Для системных команд (calc, notepad и т.д.) не устанавливаем WorkingDirectory
                    _logger.LogDebug("File {Path} not found, trying as system command", executablePath);
                }

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchResult.Failure($"Failed to start process: {app.ExecutablePath}");
                }

                _logger.LogDebug("Desktop application {AppName} launched with PID {ProcessId}",
                    app.Name, process.Id);

                // Регистрируем запущенное приложение в сервисе управления
                try
                {
                    // Для Desktop приложений регистрируем обычным способом
                    await _runningApplicationsService.RegisterApplicationAsync(app, process, launchedBy);
                    _logger.LogDebug("Registered running application {AppName} (PID: {ProcessId}) for user {User}",
                        app.Name, process.Id, launchedBy);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register running application {AppName} (PID: {ProcessId})",
                        app.Name, process.Id);
                    // Не прерываем выполнение если регистрация не удалась
                }

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error launching desktop application '{app.Name}' ({app.ExecutablePath}): {ex.Message}";
                _logger.LogError(ex, "Error launching desktop application {AppName} at path {Path}", app.Name, app.ExecutablePath);
                return LaunchResult.Failure(errorMsg);
            }
        }

        private async Task<LaunchResult> LaunchWebApplicationAsync(Application app, string launchedBy)
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

                // Для Web приложений регистрируем с особой пометкой
                try
                {
                    await _runningApplicationsService.RegisterApplicationAsync(app, process, launchedBy);
                    _logger.LogDebug("Registered web application {AppName} (PID: {ProcessId}) for user {User}",
                        app.Name, process.Id, launchedBy);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register web application {AppName} (PID: {ProcessId})",
                        app.Name, process.Id);
                    // Для web приложений ошибка регистрации не критична
                }

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error launching web application '{app.Name}' ({app.ExecutablePath}): {ex.Message}";
                _logger.LogError(ex, "Error launching web application {AppName} at URL {Url}", app.Name, app.ExecutablePath);
                return LaunchResult.Failure(errorMsg);
            }
        }

        private async Task<LaunchResult> LaunchFolderAsync(Application app, string launchedBy)
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

                // Для Folder приложений регистрируем процесс explorer.exe
                try
                {
                    await _runningApplicationsService.RegisterApplicationAsync(app, process, launchedBy);
                    _logger.LogDebug("Registered folder application {AppName} (PID: {ProcessId}) for user {User}",
                        app.Name, process.Id, launchedBy);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register folder application {AppName} (PID: {ProcessId})",
                        app.Name, process.Id);
                    // Для folder приложений ошибка регистрации не критична
                }

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error launching folder '{app.Name}' ({app.ExecutablePath}): {ex.Message}";
                _logger.LogError(ex, "Error launching folder {AppName} at path {Path}", app.Name, app.ExecutablePath);
                return LaunchResult.Failure(errorMsg);
            }
        }

        /// <summary>
        /// Парсит ExecutablePath для разделения пути к исполняемому файлу и аргументов
        /// </summary>
        private (string executablePath, string arguments) ParseExecutablePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return (string.Empty, string.Empty);
            }

            fullPath = fullPath.Trim();

            // Случай 1: Путь в кавычках, за которыми следуют аргументы
            if (fullPath.StartsWith("\""))
            {
                var endQuoteIndex = fullPath.IndexOf("\"", 1);
                if (endQuoteIndex > 0)
                {
                    var execPath = fullPath.Substring(1, endQuoteIndex - 1);
                    var args = fullPath.Length > endQuoteIndex + 1 
                        ? fullPath.Substring(endQuoteIndex + 1).Trim()
                        : string.Empty;
                    
                    _logger.LogDebug("Parsed quoted path: '{ExecPath}' args: '{Args}'", execPath, args);
                    return (execPath, args);
                }
            }

            // Случай 2: Путь без кавычек - ищем первый аргумент, начинающийся с -
            var parts = fullPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 1)
            {
                // Только путь, без аргументов
                return (fullPath, string.Empty);
            }

            // Находим первый аргумент (начинается с - или /)
            int firstArgIndex = -1;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("-") || parts[i].StartsWith("/"))
                {
                    firstArgIndex = i;
                    break;
                }
            }

            if (firstArgIndex > 0)
            {
                // Есть аргументы
                var execPath = string.Join(" ", parts.Take(firstArgIndex));
                var args = string.Join(" ", parts.Skip(firstArgIndex));
                
                _logger.LogDebug("Parsed unquoted path with args: '{ExecPath}' args: '{Args}'", execPath, args);
                return (execPath, args);
            }

            // Случай 3: Проверяем, существует ли полный путь как файл
            if (File.Exists(fullPath))
            {
                return (fullPath, string.Empty);
            }

            // Случай 4: Пытаемся найти существующий файл, идя от конца
            var pathBuilder = new StringBuilder();
            bool foundFile = false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (pathBuilder.Length > 0) pathBuilder.Append(" ");
                pathBuilder.Append(parts[i]);

                var testPath = pathBuilder.ToString();
                if (File.Exists(testPath))
                {
                    // Нашли существующий файл
                    var remainingArgs = string.Join(" ", parts.Skip(i + 1));
                    _logger.LogDebug("Found existing file: '{TestPath}' remaining args: '{Args}'", testPath, remainingArgs);
                    return (testPath, remainingArgs);
                }
            }

            // Случай 5: Не удалось разделить - возвращаем как есть
            _logger.LogDebug("Could not parse path, returning as-is: '{FullPath}'", fullPath);
            return (fullPath, string.Empty);
        }

        /// <summary>
        /// Запуск Chrome App приложения с отслеживанием через title и предотвращением дублирования
        /// </summary>
        private async Task<LaunchResult> LaunchChromeAppAsync(Application app, string launchedBy)
        {
            try
            {
                _logger.LogInformation("Launching Chrome App: {Name}", app.Name);

                // ✅ ИСПРАВЛЕНИЕ: Для Chrome Apps разрешаем запуск нескольких экземпляров
                // Убираем поиск существующих процессов, так как это приводит к неправильному переключению
                // между разными Chrome Apps с похожими заголовками
                _logger.LogInformation("Launching new Chrome App instance: {Name} (multiple instances allowed)", app.Name);

                // Парсим путь к Chrome и аргументы
                var (executablePath, executableArgs) = ParseExecutablePath(app.ExecutablePath);
                
                // Объединяем аргументы, обеспечивая наличие --app или --kiosk
                var combinedArguments = string.IsNullOrEmpty(executableArgs) 
                    ? (app.Arguments ?? "")
                    : string.IsNullOrEmpty(app.Arguments) 
                        ? executableArgs
                        : $"{executableArgs} {app.Arguments}";

                // Проверяем наличие --app или --kiosk аргументов
                if (!combinedArguments.ToLowerInvariant().Contains("--app") && 
                    !combinedArguments.ToLowerInvariant().Contains("--kiosk"))
                {
                    return LaunchResult.Failure("Chrome App must have --app or --kiosk argument");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = combinedArguments,
                    UseShellExecute = true
                };

                // Устанавливаем рабочую директорию если Chrome существует
                if (File.Exists(executablePath))
                {
                    startInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "";
                }

                _logger.LogDebug("Launching Chrome App: '{Path}' with arguments: '{Args}'", executablePath, combinedArguments);

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return LaunchResult.Failure($"Failed to start Chrome App: {app.ExecutablePath}");
                }

                _logger.LogDebug("Chrome App {AppName} launcher process started with PID {ProcessId}", app.Name, process.Id);

                // Активно ищем Chrome App процесс с retry логикой
                var chromeAppProcessId = await FindChromeAppProcessWithRetryAsync(app, maxRetries: 5, delayMs: 1000);
                
                if (chromeAppProcessId.HasValue)
                {
                    _logger.LogInformation("🔍 FOUND Chrome App process {ProcessId} for {AppName}", chromeAppProcessId.Value, app.Name);
                    
                    // Регистрируем найденный Chrome App процесс
                    try
                    {
                        var chromeAppProcess = Process.GetProcessById(chromeAppProcessId.Value);
                        _logger.LogInformation("📝 REGISTERING Chrome App {AppName} (PID: {ProcessId}) with RunningApplicationsService...", 
                            app.Name, chromeAppProcess.Id);
                        
                        await _runningApplicationsService.RegisterApplicationAsync(app, chromeAppProcess, launchedBy);
                        
                        _logger.LogInformation("✅ SUCCESSFULLY registered Chrome App {AppName} (PID: {ProcessId}) for user {User}",
                            app.Name, chromeAppProcess.Id, launchedBy);
                            
                        return LaunchResult.Success(chromeAppProcess.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ FAILED to register Chrome App {AppName} (PID: {ProcessId})",
                            app.Name, chromeAppProcessId.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("❌ Could not find Chrome App process for {AppName} by title after retries", app.Name);
                }

                // Fallback: регистрируем с launcher процессом, но с предупреждением
                try
                {
                    await _runningApplicationsService.RegisterApplicationAsync(app, process, launchedBy);
                    _logger.LogWarning("Registered Chrome App launcher {AppName} (PID: {ProcessId}) as fallback - may not appear correctly in AppSwitcher",
                        app.Name, process.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register Chrome App launcher {AppName} (PID: {ProcessId})",
                        app.Name, process.Id);
                }

                return LaunchResult.Success(process.Id);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error launching Chrome App '{app.Name}' ({app.ExecutablePath}): {ex.Message}";
                _logger.LogError(ex, "Error launching Chrome App {AppName} at path {Path}", app.Name, app.ExecutablePath);
                return LaunchResult.Failure(errorMsg);
            }
        }

        /// <summary>
        /// Поиск существующего Chrome App процесса для предотвращения дублирования
        /// </summary>
        private async Task<int?> FindExistingChromeAppAsync(Application app)
        {
            try
            {
                // Для ChromeApp всегда ищем по title среди всех Chrome процессов
                // Это надежнее, чем полагаться на зарегистрированные PID
                _logger.LogDebug("Searching for existing Chrome App {Name} by window title", app.Name);
                
                var processId = await FindChromeAppProcessByTitleAsync(app);
                if (processId.HasValue)
                {
                    _logger.LogInformation("Found existing Chrome App {Name} with PID {ProcessId} by title", 
                        app.Name, processId.Value);
                    return processId.Value;
                }

                _logger.LogDebug("No existing Chrome App found for {Name}", app.Name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing Chrome App for {AppName}", app.Name);
                return null;
            }
        }

        /// <summary>
        /// Поиск Chrome App процесса с retry логикой для ожидания создания окна
        /// </summary>
        private async Task<int?> FindChromeAppProcessWithRetryAsync(Application app, int maxRetries = 5, int delayMs = 1000)
        {
            _logger.LogInformation("=== CHROME APP PROCESS SEARCH WITH RETRY START ===\n" +
                                 "App Name: {AppName}\n" +
                                 "Max Retries: {MaxRetries}\n" +
                                 "Delay: {DelayMs}ms", app.Name, maxRetries, delayMs);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                _logger.LogInformation("🔍 ATTEMPT {Attempt}/{MaxRetries}: Searching for Chrome App process", attempt, maxRetries);
                
                var processId = await FindChromeAppProcessByTitleAsync(app);
                if (processId.HasValue)
                {
                    _logger.LogInformation("✅ FOUND Chrome App process on attempt {Attempt}: PID {ProcessId}\n" +
                                         "=== CHROME APP PROCESS SEARCH END - SUCCESS ===", 
                        attempt, processId.Value);
                    return processId;
                }
                
                if (attempt < maxRetries)
                {
                    _logger.LogWarning("❌ Chrome App process not found on attempt {Attempt}, waiting {DelayMs}ms before retry", 
                        attempt, delayMs);
                    await Task.Delay(delayMs);
                }
                else
                {
                    _logger.LogWarning("❌ Chrome App process not found on final attempt {Attempt}", attempt);
                }
            }
            
            _logger.LogError("❌ FAILED to find Chrome App process for {AppName} after {MaxRetries} attempts\n" +
                           "=== CHROME APP PROCESS SEARCH END - FAILED ===", 
                app.Name, maxRetries);
            return null;
        }

        /// <summary>
        /// Поиск Chrome App процесса по title с использованием Windows API (решает проблему с Process.MainWindowTitle)
        /// </summary>
        private async Task<int?> FindChromeAppProcessByTitleAsync(Application app)
        {
            await Task.CompletedTask;
            
            try
            {
                // Извлекаем ожидаемый title из аргументов
                var expectedTitle = ExtractExpectedTitleFromChromeApp(app);
                if (string.IsNullOrEmpty(expectedTitle))
                {
                    _logger.LogWarning("Could not extract expected title for Chrome App {AppName}", app.Name);
                    return null;
                }

                _logger.LogDebug("Looking for Chrome App process with title: '{ExpectedTitle}' using Windows API", expectedTitle);

                // Используем новую утилиту для поиска Chrome окон через Windows API с настройками из конфигурации
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(_chromeWindowSearchOptions.SearchTimeoutSeconds));
                ChromeWindowHelper.ChromeWindowInfo? chromeWindow = null;
                
                try
                {
                    chromeWindow = await Task.Run(() => 
                        ChromeWindowHelper.FindChromeWindowByTitle(0, expectedTitle, _logger, _chromeWindowSearchOptions), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Chrome window search timed out after {Timeout} seconds for {AppName}", 
                        _chromeWindowSearchOptions.SearchTimeoutSeconds, app.Name);
                    return null;
                }
                
                if (chromeWindow != null)
                {
                    _logger.LogInformation("Found Chrome App process {ProcessId} with title '{WindowTitle}' using Windows API", 
                        chromeWindow.ProcessId, chromeWindow.WindowTitle);
                    return (int)chromeWindow.ProcessId;
                }

                _logger.LogWarning("No Chrome App process found with title containing: '{ExpectedTitle}' using Windows API", expectedTitle);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding Chrome App process for {AppName} using Windows API", app.Name);
                return null;
            }
        }

        /// <summary>
        /// Извлечение ожидаемого title для Chrome App
        /// </summary>
        private string ExtractExpectedTitleFromChromeApp(Application app)
        {
            try
            {
                _logger.LogDebug("ExtractExpectedTitle: Processing Chrome App {AppName}, Description: '{Description}'", 
                    app.Name, app.Description ?? "null");
                
                // Сначала проверяем кэшированный title в Description
                if (!string.IsNullOrEmpty(app.Description))
                {
                    if (app.Description.StartsWith("[CACHED_TITLE]"))
                    {
                        var cachedTitle = app.Description.Substring("[CACHED_TITLE]".Length);
                        _logger.LogDebug("Using cached title: '{Title}' for Chrome App {AppName}", cachedTitle, app.Name);
                        return cachedTitle;
                    }
                    
                    // Если Description короткий и не кэшированный, возможно это и есть ожидаемый title
                    if (app.Description.Length < 100)
                    {
                        _logger.LogDebug("Using short description as title: '{Title}' for Chrome App {AppName}", 
                            app.Description.Trim(), app.Name);
                        return app.Description.Trim();
                    }
                }

                // Извлекаем из аргументов --app
                var allArgs = $"{app.ExecutablePath ?? ""} {app.Arguments ?? ""}";
                _logger.LogDebug("ExtractExpectedTitle: Looking in args: '{AllArgs}'", allArgs);
                
                // Ищем --app=file:/// или --app=http(s)://
                var appArgMatch = System.Text.RegularExpressions.Regex.Match(
                    allArgs, @"--app=((?:file:///|https?://)[^\s""]+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (appArgMatch.Success)
                {
                    var url = appArgMatch.Groups[1].Value;
                    
                    // Для PDF файлов Chrome использует имя файла с расширением как title
                    if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        // Декодируем URL-encoded имя файла
                        var decodedUrl = System.Net.WebUtility.UrlDecode(url);
                        var fileName = Path.GetFileName(decodedUrl); // С расширением для PDF!
                        _logger.LogDebug("Extracted PDF title from '{Url}': '{FileName}'", url, fileName);
                        return fileName;
                    }
                    
                    // Для HTML файлов извлекаем имя файла без расширения (title из <title> тега)
                    if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || 
                        url.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(url);
                        _logger.LogDebug("Extracted HTML title from filename: '{FileName}'", fileName);
                        return fileName;
                    }
                    
                    // Для других файлов используем полное имя файла
                    if (url.Contains('.'))
                    {
                        var fileName = Path.GetFileName(url);
                        _logger.LogDebug("Extracted file title: '{FileName}'", fileName);
                        return fileName;
                    }
                    
                    // Для URL используем кэшированный title или fallback
                    // (кэширование происходит при создании/обновлении приложения)
                    
                    // Fallback для URL: домен или последняя часть пути
                    var urlParts = url.Split('/');
                    var title = urlParts.Length > 1 ? urlParts.Last() : urlParts[0];
                    _logger.LogDebug("Fallback URL title: '{Title}'", title);
                    return title;
                }
                
                // Fallback на имя приложения
                _logger.LogDebug("ExtractExpectedTitle: Using app name as fallback: '{Name}'", app.Name);
                return app.Name;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting title from Chrome App {AppName}", app.Name);
                return app.Name;
            }
        }

        /// <summary>
        /// Извлекает реальное содержимое тега &lt;title&gt; с веб-страницы
        /// </summary>
        private async Task<string> ExtractWebPageTitleAsync(string url)
        {
            try
            {
                _logger.LogDebug("Attempting to extract web page title from: {Url}", url);
                
                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("HTTP request failed for {Url}: {StatusCode}", url, response.StatusCode);
                    return string.Empty;
                }

                var html = await response.Content.ReadAsStringAsync();
                
                // Извлекаем содержимое тега <title>
                var titleMatch = System.Text.RegularExpressions.Regex.Match(
                    html, @"<title[^>]*>(.*?)</title>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                
                if (titleMatch.Success && titleMatch.Groups.Count > 1)
                {
                    var title = titleMatch.Groups[1].Value.Trim();
                    
                    // Декодируем HTML entities
                    title = System.Net.WebUtility.HtmlDecode(title);
                    
                    // Ограничиваем длину title (Chrome обрезает длинные заголовки в window title)
                    // Используем первые 50 символов для более точного поиска
                    if (title.Length > 50)
                    {
                        title = title.Substring(0, 50);
                    }
                    
                    _logger.LogDebug("Successfully extracted web title: '{Title}' from {Url}", title, url);
                    return title;
                }
                
                _logger.LogDebug("No <title> tag found in HTML from {Url}", url);
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Timeout while extracting title from {Url}", url);
                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogDebug(ex, "HTTP error while extracting title from {Url}", url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting web page title from {Url}", url);
                return string.Empty;
            }
        }

        /// <summary>
        /// Извлекает title из локального HTML файла
        /// </summary>
        private async Task<string> ExtractLocalHTMLTitleAsync(string filePath)
        {
            try
            {
                _logger.LogDebug("Attempting to extract title from local HTML file: {FilePath}", filePath);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Local HTML file not found: {FilePath}", filePath);
                    return string.Empty;
                }

                var htmlContent = await File.ReadAllTextAsync(filePath);
                
                // Извлекаем содержимое тега <title>
                var titleMatch = System.Text.RegularExpressions.Regex.Match(
                    htmlContent, @"<title[^>]*>(.*?)</title>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                
                if (titleMatch.Success && titleMatch.Groups.Count > 1)
                {
                    var title = titleMatch.Groups[1].Value.Trim();
                    
                    // Декодируем HTML entities
                    title = System.Net.WebUtility.HtmlDecode(title);
                    
                    // Ограничиваем длину title (Chrome обрезает длинные заголовки в window title)
                    if (title.Length > 50)
                    {
                        title = title.Substring(0, 50);
                    }
                    
                    _logger.LogDebug("Successfully extracted local HTML title: '{Title}' from {FilePath}", title, filePath);
                    return title;
                }
                
                _logger.LogDebug("No <title> tag found in local HTML file {FilePath}", filePath);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting title from local HTML file {FilePath}", filePath);
                return string.Empty;
            }
        }

        /// <summary>
        /// Предварительное кэширование web title для Chrome App при создании/обновлении
        /// </summary>
        private async Task PreCacheWebTitleForChromeAppAsync(Application application)
        {
            try
            {
                // Извлекаем URL из аргументов --app
                var allArgs = $"{application.ExecutablePath ?? ""} {application.Arguments ?? ""}";
                _logger.LogDebug("Pre-caching: Looking for --app URL in args: '{AllArgs}'", allArgs);
                
                var appArgMatch = System.Text.RegularExpressions.Regex.Match(
                    allArgs, @"--app=((?:file:///|https?://)[^\s""]+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (appArgMatch.Success)
                {
                    var fullUrl = appArgMatch.Groups[1].Value; // Полный URL после --app=
                    
                    _logger.LogDebug("Found Chrome App URL in arguments: {FullUrl}", fullUrl);
                    
                    // Для HTTP/HTTPS URL - кэшируем title с веб-страницы
                    if (fullUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        fullUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Pre-caching web title for Chrome App {AppName} from {Url}", 
                            application.Name, fullUrl);
                        
                        var webTitle = await ExtractWebPageTitleAsync(fullUrl);
                        if (!string.IsNullOrEmpty(webTitle))
                        {
                            application.Description = $"[CACHED_TITLE]{webTitle}";
                            _logger.LogInformation("Cached web title '{Title}' for Chrome App {AppName}", 
                                webTitle, application.Name);
                        }
                    }
                    // Для file:/// HTML файлов - извлекаем title из файла
                    else if (fullUrl.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) &&
                             (fullUrl.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || 
                              fullUrl.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Конвертируем file:///C:/path в C:\path
                        var filePath = fullUrl.Substring("file:///".Length).Replace('/', '\\');
                        
                        _logger.LogInformation("Pre-caching title from local HTML file for Chrome App {AppName}: {FilePath}", 
                            application.Name, filePath);
                        
                        var fileTitle = await ExtractLocalHTMLTitleAsync(filePath);
                        if (!string.IsNullOrEmpty(fileTitle))
                        {
                            application.Description = $"[CACHED_TITLE]{fileTitle}";
                            _logger.LogInformation("Cached local HTML title '{Title}' for Chrome App {AppName}", 
                                fileTitle, application.Name);
                        }
                        else
                        {
                            _logger.LogDebug("Could not extract title from local HTML file, using filename fallback");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Chrome App {AppName} uses file or non-HTML URL, no title caching needed", 
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
    }
}
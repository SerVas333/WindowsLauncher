using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WindowsLauncher.Core.Infrastructure.Extensions;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Services.Android;

namespace WindowsLauncher.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Лаунчер для запуска Android APK приложений через Windows Subsystem for Android
    /// Реализует полный интерфейс IApplicationLauncher для интеграции с системой управления приложениями
    /// </summary>
    public class WSAApplicationLauncher : IApplicationLauncher
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IAndroidSubsystemService _androidSubsystemService;
        private readonly IWindowManager _windowManager;
        private readonly ILogger<WSAApplicationLauncher> _logger;
        private readonly AndroidArgumentsParser _argumentsParser;
        private readonly ConcurrentDictionary<string, ApplicationInstance> _runningInstances;
        private readonly Timer _cleanupTimer;
        private readonly Timer _windowMonitorTimer;

        public ApplicationType SupportedType => ApplicationType.Android;
        public int Priority => 25; // Выше DesktopApplicationLauncher (10) но ниже TextEditor (30)

        /// <summary>
        /// Событие активации окна приложения (для интеграции с AppSwitcher)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowActivated;

        /// <summary>  
        /// Событие закрытия окна приложения (для управления жизненным циклом)
        /// </summary>
        public event EventHandler<ApplicationInstance>? WindowClosed;


        public WSAApplicationLauncher(
            IServiceScopeFactory serviceScopeFactory,
            IAndroidSubsystemService androidSubsystemService,
            IWindowManager windowManager,
            ILogger<WSAApplicationLauncher> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _androidSubsystemService = androidSubsystemService ?? throw new ArgumentNullException(nameof(androidSubsystemService));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _argumentsParser = new AndroidArgumentsParser(logger);
            
            _runningInstances = new ConcurrentDictionary<string, ApplicationInstance>();

            // Таймер для очистки завершенных экземпляров
            _cleanupTimer = new Timer(CleanupCompletedInstances, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            
            // НОВОЕ: Таймер для мониторинга WSA окон
            _windowMonitorTimer = new Timer(MonitorWSAWindows, null,
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)); // Проверяем каждые 5 секунд после 10-секундной задержки
        }

        public bool CanLaunch(Application application)
        {
            if (application == null)
            {
                return false;
            }

            // Проверяем тип приложения
            if (application.Type != ApplicationType.Android)
            {
                return false;
            }

            // Проверяем ExecutablePath
            if (string.IsNullOrWhiteSpace(application.ExecutablePath))
            {
                return false;
            }

            var executablePath = application.ExecutablePath.ToLowerInvariant();

            // Поддерживаем APK и XAPK файлы, а также package names
            return executablePath.EndsWith(".apk") || 
                   executablePath.EndsWith(".xapk") || 
                   IsValidPackageName(executablePath);
        }

        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (application == null)
            {
                return LaunchResult.Failure("Application is null");
            }

            if (!CanLaunch(application))
            {
                return LaunchResult.Failure($"Cannot launch application: {application.Name}");
            }

            // Проверяем существующий экземпляр перед запуском нового
            var existingInstance = await FindExistingInstanceAsync(application);
            if (existingInstance != null && existingInstance.IsActiveInstance())
            {
                var switchSuccess = await SwitchToAsync(existingInstance.InstanceId);
                return LaunchResult.AlreadyRunning(existingInstance, switchSuccess);
            }

            var instanceId = GenerateInstanceId(application);
            var startTime = DateTime.Now;
            
            _logger.LogInformation("Launching Android application: {ApplicationName} (ID: {ApplicationId}, Instance: {InstanceId})",
                application.Name, application.Id, instanceId);

            // Обновляем статус WSA перед запуском приложения
            await _androidSubsystemService.RefreshWSAStatusAsync();

            try
            {
                string packageName;
                
                // Определяем package name из ExecutablePath
                if (application.ExecutablePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                    application.ExecutablePath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                {
                    // Это APK или XAPK файл - извлекаем package name из метаданных
                    packageName = await ExtractPackageNameFromApk(application.ExecutablePath);
                    if (string.IsNullOrEmpty(packageName))
                    {
                        return LaunchResult.Failure($"Failed to extract metadata from APK/XAPK: {application.ExecutablePath}");
                    }
                }
                else
                {
                    // Предполагаем что это уже package name
                    packageName = application.ExecutablePath;
                }

                _logger.LogDebug("Launching Android package: {PackageName}", packageName);

                // Запускаем приложение через AndroidApplicationManager
                var androidManager = _serviceScopeFactory.CreateScopedService<IAndroidApplicationManager>();
                var launchResult = await androidManager.LaunchAndroidAppAsync(packageName);

                if (launchResult.Success)
                {
                    var instance = await CreateApplicationInstanceAsync(application, instanceId, packageName, launchResult, launchedBy);
                    
                    // Добавляем в список запущенных экземпляров
                    _runningInstances.TryAdd(instanceId, instance);

                    _logger.LogInformation("Successfully launched Android application: {ApplicationName} (Package: {PackageName}, PID: {ProcessId})",
                        application.Name, packageName, launchResult.ProcessId);

                    var launchDuration = DateTime.Now - startTime;
                    return LaunchResult.Success(instance, launchDuration);
                }
                else
                {
                    var errorMessage = $"Failed to launch Android application: {launchResult.ErrorMessage}";
                    _logger.LogError(errorMessage);
                    
                    // Создаем failed экземпляр для отслеживания
                    var failedInstance = CreateFailedInstance(application, instanceId, packageName, launchResult.ErrorMessage, launchedBy);
                    _runningInstances.TryAdd(instanceId, failedInstance);

                    return LaunchResult.Failure(errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Exception launching Android application {application.Name}: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                
                // Создаем failed экземпляр для отслеживания
                var failedInstance = CreateFailedInstance(application, instanceId, 
                    application.ExecutablePath ?? "", ex.Message, launchedBy);
                _runningInstances.TryAdd(instanceId, failedInstance);

                return LaunchResult.Failure(ex, errorMessage);
            }
        }

        public async Task<bool> SwitchToAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            _logger.LogDebug("Attempting to switch to Android application instance: {InstanceId}", instanceId);

            try
            {
                if (_runningInstances.TryGetValue(instanceId, out var instance))
                {
                    if (instance.State == ApplicationState.Running)
                    {
                        // НОВОЕ: Сначала пытаемся переключиться на реальное WSA окно
                        if (instance.MainWindow != null && instance.MainWindow.Handle != IntPtr.Zero && !instance.IsVirtual)
                        {
                            _logger.LogDebug("Attempting to activate real WSA window for instance {InstanceId}: Handle={Handle:X}", 
                                instanceId, (long)instance.MainWindow.Handle);
                            
                            var activationResult = await _windowManager.BringWindowToFrontAsync(instance.MainWindow.Handle);
                            if (activationResult)
                            {
                                // Обновляем время последней активности
                                instance.LastUpdate = DateTime.Now;
                                instance.IsActive = true;

                                // Уведомляем о активации окна
                                WindowActivated?.Invoke(this, instance);
                                
                                _logger.LogInformation("Successfully activated WSA window for instance {InstanceId}: {Title}", 
                                    instanceId, instance.MainWindow.Title);
                                return true;
                            }
                            
                            _logger.LogWarning("Failed to activate WSA window, falling back to Android app launch");
                        }
                        
                        // Fallback: запуск Android активности (старый способ)
                        var packageName = ExtractPackageNameFromExecutablePath(instance.Application.ExecutablePath ?? "");
                        
                        if (!string.IsNullOrEmpty(packageName))
                        {
                            var androidManager = _serviceScopeFactory.CreateScopedService<IAndroidApplicationManager>();
                            var launchResult = await androidManager.LaunchAndroidAppAsync(packageName);
                            
                            if (launchResult.Success)
                            {
                                // Обновляем время последней активности
                                instance.LastUpdate = DateTime.Now;
                                instance.IsActive = true;

                                _logger.LogInformation("Successfully switched to Android application via re-launch: {InstanceId}", instanceId);
                                return true;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Cannot switch to Android application instance {InstanceId} - state is {State}",
                            instanceId, instance.State);
                    }
                }
                else
                {
                    _logger.LogWarning("Android application instance not found: {InstanceId}", instanceId);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception switching to Android application instance: {InstanceId}", instanceId);
                return false;
            }
        }

        public async Task<ApplicationInstance?> FindExistingInstanceAsync(Application application)
        {
            if (application == null)
            {
                return null;
            }

            try
            {
                // Ищем экземпляр того же приложения
                var existingInstance = _runningInstances.Values
                    .FirstOrDefault(instance => instance.Application.Id == application.Id);

                if (existingInstance != null)
                {
                    _logger.LogDebug("Found existing Android application instance: {InstanceId} for application {ApplicationId}",
                        existingInstance.InstanceId, application.Id);

                    return existingInstance;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception finding existing Android application instance for: {ApplicationId}", application.Id);
                return null;
            }
        }

        #region Private Helper Methods

        private async Task<string> ExtractPackageNameFromApk(string apkPath)
        {
            try
            {
                var androidManager = _serviceScopeFactory.CreateScopedService<IAndroidApplicationManager>();
                var metadata = await androidManager.ExtractApkMetadataAsync(apkPath);
                return metadata?.PackageName ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract package name from APK: {ApkPath}", apkPath);
                return "";
            }
        }

        private static string ExtractPackageNameFromExecutablePath(string executablePath)
        {
            if (executablePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                executablePath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
            {
                // Для APK/XAPK файлов нужно извлекать package name из метаданных
                // В данном контексте мы уже должны иметь package name в ExecutablePath
                // после первого запуска, но это запасной вариант
                return Path.GetFileNameWithoutExtension(executablePath);
            }
            
            return executablePath; // Предполагаем что это уже package name
        }

        private static bool IsValidPackageName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            // Базовая проверка на соответствие формату Android package name
            // Пример: com.example.app, org.mozilla.firefox
            return packageName.Contains('.') && 
                   packageName.Length >= 3 && 
                   !packageName.StartsWith('.') && 
                   !packageName.EndsWith('.') &&
                   packageName.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_');
        }

        private string GenerateInstanceId(Application application)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var random = new Random().Next(1000, 9999);
            return $"android_{application.Id}_{timestamp:X}_{random:X}";
        }

        private async Task<ApplicationInstance> CreateApplicationInstanceAsync(
            Application application, 
            string instanceId, 
            string packageName, 
            Core.Models.Android.AppLaunchResult launchResult,
            string launchedBy)
        {
            var instance = new ApplicationInstance
            {
                InstanceId = instanceId,
                Application = application,
                LaunchedBy = launchedBy,
                ProcessId = 0, // Будет установлен позже на основе реального WSA окна
                ProcessName = $"android_{packageName}",
                State = ApplicationState.Running,
                StartTime = DateTime.Now,
                LastUpdate = DateTime.Now,
                IsActive = true
            };

            // НОВОЕ: Парсим Android-специфические аргументы
            var androidArgs = _argumentsParser.Parse(application.Arguments);
            _logger.LogDebug("Parsed Android arguments for {ApplicationName}: {Arguments}", 
                application.Name, androidArgs.ToString());

            // Добавляем Android-специфичные метаданные
            instance.Metadata["AndroidPackageName"] = packageName;
            instance.Metadata["AndroidActivity"] = launchResult.ActivityName ?? androidArgs.ActivityName ?? "MainActivity";
            instance.Metadata["LauncherType"] = "WSA";
            
            // Добавляем извлеченные аргументы в метаданные
            if (!string.IsNullOrEmpty(androidArgs.WindowName))
                instance.Metadata["AndroidWindowName"] = androidArgs.WindowName;
            if (androidArgs.LaunchTimeout.HasValue)
                instance.Metadata["AndroidLaunchTimeout"] = androidArgs.LaunchTimeout.Value.TotalSeconds.ToString();

            // ОБНОВЛЕННОЕ: Пытаемся найти реальное WSA окно с учетом window_name из аргументов
            var realWindow = await FindWSAWindowForPackageAsync(packageName, launchResult.ActivityName, androidArgs.WindowName);
            
            if (realWindow != null)
            {
                // Найдено реальное WSA окно!
                instance.MainWindow = realWindow;
                instance.ProcessId = (int)realWindow.ProcessId; // Используем ProcessId реального WSA окна
                instance.IsVirtual = false;
                instance.Metadata["WindowDetectionMethod"] = !string.IsNullOrEmpty(androidArgs.WindowName) ? 
                    "WindowNameArgument" : "WSAWindowSearch";
                
                _logger.LogInformation("Found real WSA window for {PackageName}: Handle={Handle:X}, Title='{Title}', PID={ProcessId} - window monitoring enabled", 
                    packageName, (long)realWindow.Handle, realWindow.Title, realWindow.ProcessId);
                
                // Уведомляем о активации окна
                WindowActivated?.Invoke(this, instance);
            }
            else
            {
                // Проверяем, разрешен ли virtual fallback (по умолчанию разрешен для обратной совместимости)
                bool allowVirtualFallback = androidArgs.VirtualFallback ?? true;
                
                if (allowVirtualFallback)
                {
                    // Fallback к виртуальному окну
                    instance.MainWindow = WindowInfo.Create(
                        handle: IntPtr.Zero,
                        title: $"{application.Name} (Android)",
                        processId: 0 // Для виртуальных окон не используем ProcessId
                    );
                    instance.ProcessId = -1; // Специальный ProcessId для виртуальных WSA приложений
                    instance.IsVirtual = true;
                    instance.Metadata["WindowDetectionMethod"] = "VirtualFallback";
                    
                    _logger.LogWarning("Could not find real WSA window for {PackageName}, using virtual window fallback with PID={ProcessId}", 
                        packageName, instance.ProcessId);
                }
                else
                {
                    // Virtual fallback отключен - помечаем экземпляр как ошибочный
                    instance.State = ApplicationState.Error;
                    instance.ErrorMessage = "WSA window not found and virtual fallback is disabled";
                    instance.IsVirtual = true;
                    instance.ProcessId = -1;
                    instance.Metadata["WindowDetectionMethod"] = "Failed_NoVirtualFallback";
                    
                    _logger.LogError("WSA window not found for {PackageName} and virtual fallback is disabled", packageName);
                }
            }
            
            return instance;
        }

        private ApplicationInstance CreateFailedInstance(
            Application application, 
            string instanceId, 
            string executablePath, 
            string errorMessage,
            string launchedBy)
        {
            return new ApplicationInstance
            {
                InstanceId = instanceId,
                Application = application,
                LaunchedBy = launchedBy,
                ProcessId = 0,
                ProcessName = "android_failed",
                State = ApplicationState.Error,
                StartTime = DateTime.Now,
                LastUpdate = DateTime.Now,
                IsActive = false,
                ErrorMessage = errorMessage,
                IsVirtual = true
            };
        }


        private void CleanupCompletedInstances(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
                var instancesToRemove = new List<string>();

                foreach (var kvp in _runningInstances)
                {
                    var instance = kvp.Value;
                    
                    // Удаляем старые failed экземпляры
                    if (instance.State == ApplicationState.Error && 
                        instance.StartTime < cutoffTime)
                    {
                        instancesToRemove.Add(kvp.Key);
                    }
                    // Удаляем старые terminated экземпляры
                    else if (instance.State == ApplicationState.Terminated && 
                             instance.LastUpdate < cutoffTime)
                    {
                        instancesToRemove.Add(kvp.Key);
                    }
                }

                foreach (var instanceId in instancesToRemove)
                {
                    if (_runningInstances.TryRemove(instanceId, out var removedInstance))
                    {
                        _logger.LogDebug("Cleaned up Android application instance: {InstanceId} (State: {State})",
                            instanceId, removedInstance.State);
                        
                    }
                }

                if (instancesToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} Android application instances", instancesToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Android application instances cleanup");
            }
        }

        /// <summary>
        /// Мониторинг WSA окон и автоматическое управление жизненным циклом
        /// Проверяет состояние реальных WSA окон и отправляет события WindowClosed при их закрытии
        /// </summary>
        private async void MonitorWSAWindows(object? state)
        {
            try
            {
                // Получаем снимок текущих экземпляров для безопасной итерации
                var instances = _runningInstances.Values.ToList();
                var checkedInstances = 0;
                var closedInstances = 0;
                
                foreach (var instance in instances)
                {
                    try
                    {
                        // Пропускаем виртуальные экземпляры или экземпляры без окон
                        if (instance.IsVirtual || instance.MainWindow == null || instance.MainWindow.Handle == IntPtr.Zero)
                            continue;
                        
                        // Пропускаем уже завершенные или ошибочные экземпляры
                        if (instance.State == ApplicationState.Terminated || instance.State == ApplicationState.Error)
                            continue;
                        
                        checkedInstances++;
                        
                        // Проверяем существование окна
                        var windowExists = await _windowManager.IsWindowValidAsync(instance.MainWindow.Handle);
                        
                        if (!windowExists)
                        {
                            // Окно закрыто - обновляем состояние экземпляра
                            instance.State = ApplicationState.Terminated;
                            instance.EndTime = DateTime.Now;
                            instance.IsActive = false;
                            instance.LastUpdate = DateTime.Now;
                            
                            closedInstances++;
                            
                            _logger.LogInformation("WSA window closed for Android app {AppName} (Instance: {InstanceId}, Handle: {Handle:X})", 
                                instance.Application.Name, instance.InstanceId, (long)instance.MainWindow.Handle);
                            
                            // Отправляем событие WindowClosed для интеграции с ApplicationLifecycleService
                            try
                            {
                                WindowClosed?.Invoke(this, instance);
                            }
                            catch (Exception eventEx)
                            {
                                _logger.LogError(eventEx, "Error invoking WindowClosed event for instance {InstanceId}", 
                                    instance.InstanceId);
                            }
                            
                            // Помечаем для удаления из коллекции (будет удален в CleanupCompletedInstances)
                            _logger.LogDebug("Marked WSA instance {InstanceId} for cleanup after window closure", 
                                instance.InstanceId);
                        }
                        else
                        {
                            // Окно существует - обновляем время последней проверки
                            instance.LastUpdate = DateTime.Now;
                            
                            // Дополнительно проверяем видимость окна для обновления IsActive
                            var isVisible = await _windowManager.IsWindowVisibleAsync(instance.MainWindow.Handle);
                            var isActive = await _windowManager.IsWindowActiveAsync(instance.MainWindow.Handle);
                            
                            // Обновляем статус активности
                            bool wasActive = instance.IsActive;
                            instance.IsActive = isVisible && !await _windowManager.IsWindowMinimizedAsync(instance.MainWindow.Handle);
                            
                            // Если статус активности изменился, логируем это
                            if (wasActive != instance.IsActive)
                            {
                                _logger.LogTrace("WSA window activity changed for {AppName} (Instance: {InstanceId}): Active={IsActive}", 
                                    instance.Application.Name, instance.InstanceId, instance.IsActive);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error monitoring WSA window for instance {InstanceId}", 
                            instance.InstanceId);
                    }
                }
                
                // Логируем статистику мониторинга если есть активность
                if (checkedInstances > 0)
                {
                    _logger.LogTrace("WSA window monitoring completed: checked {CheckedCount} instances, found {ClosedCount} closed windows", 
                        checkedInstances, closedInstances);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during WSA window monitoring");
            }
        }

        /// <summary>
        /// Найти главное окно для запущенного процесса приложения
        /// ОБНОВЛЕНО: теперь пытается найти реальное WSA окно перед созданием виртуального
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="application">Модель приложения</param>
        /// <returns>Информация о реальном или виртуальном окне Android приложения</returns>
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                _logger.LogDebug("Finding main window for Android application process: {ProcessId}", processId);

                // НОВОЕ: Сначала пытаемся найти реальное WSA окно
                string packageName = ExtractPackageNameFromExecutablePath(application.ExecutablePath ?? "");
                if (!string.IsNullOrEmpty(packageName))
                {
                    var realWindow = await FindWSAWindowForPackageAsync(packageName);
                    if (realWindow != null)
                    {
                        _logger.LogInformation("Found real WSA window for Android app {AppName}: Handle={Handle:X}, Title='{Title}'", 
                            application.Name, (long)realWindow.Handle, realWindow.Title);
                        return realWindow;
                    }
                }

                // Fallback к виртуальному окну (старое поведение)
                var virtualWindow = WindowInfo.Create(
                    handle: IntPtr.Zero,
                    title: $"{application.Name} (Android)",
                    processId: (uint)processId
                );

                // Устанавливаем дополнительную информацию
                virtualWindow.ClassName = "AndroidWindowClass";
                virtualWindow.IsVisible = true;
                virtualWindow.IsResponding = true;
                virtualWindow.AdditionalInfo = $"Virtual window for Android package {packageName}";

                _logger.LogDebug("Created virtual window fallback for Android application: {Title}", virtualWindow.Title);
                return virtualWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception finding main window for Android process: {ProcessId}", processId);
                return null;
            }
        }

        /// <summary>
        /// Получить время ожидания инициализации окна для Android приложений
        /// Android приложения могут требовать больше времени на инициализацию
        /// </summary>
        /// <param name="application">Приложение</param>
        /// <returns>Время ожидания в миллисекундах</returns>
        public int GetWindowInitializationTimeoutMs(Application application)
        {
            // Android приложения могут требовать больше времени на инициализацию
            // особенно при первом запуске или установке через APK
            return 15000; // 15 секунд для Android приложений
        }

        /// <summary>
        /// Выполнить очистку ресурсов для Android экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            if (instance == null)
                return;

            try
            {
                _logger.LogDebug("Cleaning up Android application instance: {InstanceId}", instance.InstanceId);

                // Извлекаем package name из метаданных
                if (instance.Metadata.TryGetValue("AndroidPackageName", out var packageNameObj) &&
                    packageNameObj is string packageName)
                {
                    // Пытаемся корректно завершить Android приложение
                    try
                    {
                        var androidManager = _serviceScopeFactory.CreateScopedService<IAndroidApplicationManager>();
                        var stopResult = await androidManager.StopAndroidAppAsync(packageName);
                        if (stopResult)
                        {
                            _logger.LogInformation("Successfully stopped Android application: {PackageName}", packageName);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to stop Android application {PackageName}", packageName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception stopping Android application: {PackageName}", packageName);
                    }
                }

                // Обновляем состояние экземпляра
                instance.State = ApplicationState.Terminated;
                instance.EndTime = DateTime.Now;
                instance.IsActive = false;

                // Удаляем из списка запущенных экземпляров
                _runningInstances.TryRemove(instance.InstanceId, out _);

                _logger.LogDebug("Completed cleanup for Android application instance: {InstanceId}", instance.InstanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during cleanup of Android application instance: {InstanceId}", 
                    instance.InstanceId);
            }
        }

        /// <summary>
        /// Завершить указанный экземпляр Android приложения
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <param name="force">Принудительное завершение</param>
        /// <returns>true если завершение успешно</returns>
        public async Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            if (string.IsNullOrEmpty(instanceId))
                return false;

            try
            {
                _logger.LogDebug("Terminating Android application instance: {InstanceId} (Force: {Force})", 
                    instanceId, force);

                if (!_runningInstances.TryGetValue(instanceId, out var instance))
                {
                    _logger.LogWarning("Android application instance not found for termination: {InstanceId}", instanceId);
                    return false;
                }

                // Используем CleanupAsync для корректного завершения
                await CleanupAsync(instance);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception terminating Android application instance: {InstanceId}", instanceId);
                return false;
            }
        }

        /// <summary>
        /// Получить все активные экземпляры Android приложений
        /// </summary>
        /// <returns>Список активных экземпляров</returns>
        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            try
            {
                await Task.Delay(10); // Минимальная задержка для async контракта

                var activeInstances = _runningInstances.Values
                    .Where(instance => instance.IsActiveInstance())
                    .ToList();

                _logger.LogDebug("Retrieved {Count} active Android application instances", activeInstances.Count);
                return activeInstances.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving active Android application instances");
                return new List<ApplicationInstance>().AsReadOnly();
            }
        }

        /// <summary>
        /// Поиск реального WSA окна для Android приложения
        /// </summary>
        /// <param name="packageName">Имя Android пакета</param>
        /// <param name="activityName">Имя активности (опционально)</param>
        /// <param name="windowName">Точное имя окна из аргументов --window_name (приоритетный поиск)</param>
        /// <returns>Информация о найденном WSA окне или null</returns>
        private async Task<WindowInfo?> FindWSAWindowForPackageAsync(string packageName, string? activityName = null, string? windowName = null)
        {
            try
            {
                _logger.LogDebug("Searching for WSA window for package: {PackageName}, activity: {ActivityName}, window_name: {WindowName}", 
                    packageName, activityName ?? "any", windowName ?? "none");

                // НОВЫЙ УЛУЧШЕННЫЙ ПОИСК: Используем новый метод WindowManager с правильными P/Invoke
                var wsaWindow = await _windowManager.FindWSAApplicationWindowAsync(packageName, activityName, windowName);
                
                if (wsaWindow != null)
                {
                    _logger.LogInformation("Successfully found WSA window using improved detection for {PackageName}: Handle={Handle:X}, Title='{Title}', PID={ProcessId}", 
                        packageName, (long)wsaWindow.Handle, wsaWindow.Title, wsaWindow.ProcessId);
                    return wsaWindow;
                }

                _logger.LogWarning("No WSA window found using improved detection for package {PackageName}", packageName);
                
                // FALLBACK: Старый метод на случай проблем с новым подходом
                _logger.LogDebug("Falling back to legacy WSA window search for package {PackageName}", packageName);
                var legacyWindow = await _windowManager.FindWSAWindowAsync(packageName, activityName ?? "");
                
                if (legacyWindow != null)
                {
                    _logger.LogInformation("Found WSA window via legacy search for {PackageName}: Handle={Handle:X}, Title='{Title}'", 
                        packageName, (long)legacyWindow.Handle, legacyWindow.Title);
                    return legacyWindow;
                }

                // ДОПОЛНИТЕЛЬНЫЙ FALLBACK: ищем среди всех WSA окон по package name
                _logger.LogDebug("Legacy search also failed, trying final fallback among all WSA windows");
                var allWindows = await _windowManager.GetWSAWindowsAsync();
                
                if (allWindows.Count == 0)
                {
                    _logger.LogWarning("No WSA windows found in system at all for package {PackageName}", packageName);
                    return null;
                }

                // Пытаемся найти окно по совпадению названия приложения
                foreach (var window in allWindows)
                {
                    if (DoesWindowMatchPackage(window, packageName))
                    {
                        _logger.LogInformation("Found WSA window via fallback search for {PackageName}: Handle={Handle:X}, Title='{Title}'", 
                            packageName, (long)window.Handle, window.Title);
                        return window;
                    }
                }

                _logger.LogDebug("No matching WSA window found for package {PackageName} among {Count} available windows", 
                    packageName, allWindows.Count);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for WSA window for package {PackageName}", packageName);
                return null;
            }
        }

        /// <summary>
        /// Проверяет точное совпадение заголовка окна с указанным именем (для --window_name)
        /// </summary>
        /// <param name="window">Информация об окне</param>
        /// <param name="windowName">Точное имя окна для поиска</param>
        /// <returns>true если найдено точное совпадение</returns>
        private bool DoesWindowMatchExactName(WindowInfo window, string windowName)
        {
            try
            {
                if (window == null || string.IsNullOrEmpty(window.Title) || string.IsNullOrEmpty(windowName))
                    return false;

                var windowTitle = window.Title.Trim();
                var targetName = windowName.Trim();

                // Точное совпадение (case-sensitive)
                if (windowTitle.Equals(targetName, StringComparison.Ordinal))
                {
                    _logger.LogTrace("Exact match found: '{WindowTitle}' == '{TargetName}'", windowTitle, targetName);
                    return true;
                }

                // Точное совпадение без учета регистра
                if (windowTitle.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogTrace("Case-insensitive match found: '{WindowTitle}' == '{TargetName}'", windowTitle, targetName);
                    return true;
                }

                // Проверка содержания (только если точное совпадение не найдено)
                if (windowTitle.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogTrace("Partial match found: '{WindowTitle}' contains '{TargetName}'", windowTitle, targetName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error matching window title to exact name {WindowName}", windowName);
                return false;
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли WSA окно указанному Android пакету
        /// </summary>
        /// <param name="window">Информация об окне</param>
        /// <param name="packageName">Имя Android пакета</param>
        /// <returns>true если окно соответствует пакету</returns>
        private bool DoesWindowMatchPackage(WindowInfo window, string packageName)
        {
            try
            {
                if (window == null || string.IsNullOrEmpty(window.Title) || string.IsNullOrEmpty(packageName))
                    return false;

                var titleLower = window.Title.ToLowerInvariant();
                var packageLower = packageName.ToLowerInvariant();

                // Прямое совпадение с package name
                if (titleLower.Contains(packageLower))
                    return true;

                // Извлекаем простое имя приложения из package name
                var packageParts = packageName.Split('.');
                if (packageParts.Length > 0)
                {
                    var simpleAppName = packageParts[packageParts.Length - 1].ToLowerInvariant();
                    if (titleLower.Contains(simpleAppName) && simpleAppName.Length >= 3)
                        return true;
                }

                // Специальные случаи для популярных приложений
                var knownMappings = new Dictionary<string, string[]>
                {
                    ["com.whatsapp"] = new[] { "whatsapp" },
                    ["com.instagram.android"] = new[] { "instagram" },
                    ["com.facebook.katana"] = new[] { "facebook" },
                    ["com.google.android.youtube"] = new[] { "youtube" },
                    ["com.spotify.music"] = new[] { "spotify" }
                };

                if (knownMappings.TryGetValue(packageLower, out var aliases))
                {
                    return aliases.Any(alias => titleLower.Contains(alias));
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error matching window to package {PackageName}", packageName);
                return false;
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();
                    _windowMonitorTimer?.Dispose(); // НОВОЕ: освобождаем таймер мониторинга
                    
                    // Завершаем все активные экземпляры
                    foreach (var instance in _runningInstances.Values)
                    {
                        if (instance.State == ApplicationState.Running)
                        {
                            instance.State = ApplicationState.Terminated;
                            instance.EndTime = DateTime.Now;
                        }
                    }
                    
                    _runningInstances.Clear();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
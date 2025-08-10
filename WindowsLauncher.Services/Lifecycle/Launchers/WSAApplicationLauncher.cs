using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Лаунчер для запуска Android APK приложений через Windows Subsystem for Android
    /// Реализует полный интерфейс IApplicationLauncher для интеграции с системой управления приложениями
    /// </summary>
    public class WSAApplicationLauncher : IApplicationLauncher
    {
        private readonly IAndroidApplicationManager _androidManager;
        private readonly ILogger<WSAApplicationLauncher> _logger;
        private readonly ConcurrentDictionary<string, ApplicationInstance> _runningInstances;
        private readonly Timer _cleanupTimer;

        public ApplicationType SupportedType => ApplicationType.Android;
        public int Priority => 25; // Выше DesktopApplicationLauncher (10) но ниже TextEditor (30)


        public WSAApplicationLauncher(
            IAndroidApplicationManager androidManager,
            ILogger<WSAApplicationLauncher> logger)
        {
            _androidManager = androidManager ?? throw new ArgumentNullException(nameof(androidManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _runningInstances = new ConcurrentDictionary<string, ApplicationInstance>();

            // Таймер для очистки завершенных экземпляров
            _cleanupTimer = new Timer(CleanupCompletedInstances, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
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
                var launchResult = await _androidManager.LaunchAndroidAppAsync(packageName);

                if (launchResult.Success)
                {
                    var instance = CreateApplicationInstance(application, instanceId, packageName, launchResult, launchedBy);
                    
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
                        // Для Android приложений "переключение" означает повторный запуск активности
                        var packageName = ExtractPackageNameFromExecutablePath(instance.Application.ExecutablePath ?? "");
                        
                        if (!string.IsNullOrEmpty(packageName))
                        {
                            var launchResult = await _androidManager.LaunchAndroidAppAsync(packageName);
                            
                            if (launchResult.Success)
                            {
                                // Обновляем время последней активности
                                instance.LastUpdate = DateTime.Now;
                                instance.IsActive = true;

                                _logger.LogInformation("Successfully switched to Android application: {InstanceId}", instanceId);
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
                var metadata = await _androidManager.ExtractApkMetadataAsync(apkPath);
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

        private ApplicationInstance CreateApplicationInstance(
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
                ProcessId = launchResult.ProcessId ?? 0,
                ProcessName = $"android_{packageName}",
                State = ApplicationState.Running,
                StartTime = DateTime.Now,
                LastUpdate = DateTime.Now,
                IsActive = true
            };

            // Добавляем Android-специфичные метаданные
            instance.Metadata["AndroidPackageName"] = packageName;
            instance.Metadata["AndroidActivity"] = launchResult.ActivityName ?? "MainActivity";
            instance.Metadata["LauncherType"] = "WSA";

            // Создаем виртуальную информацию об окне для Android приложений
            instance.MainWindow = WindowInfo.Create(
                handle: IntPtr.Zero, // Android приложения не имеют Windows Handle
                title: $"{application.Name} (Android)",
                processId: (uint)(launchResult.ProcessId ?? 0)
            );
            
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
        /// Найти главное окно для запущенного процесса приложения
        /// Для Android приложений возвращает виртуальную информацию об окне
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="application">Модель приложения</param>
        /// <returns>Информация о виртуальном окне Android приложения</returns>
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, Application application)
        {
            try
            {
                _logger.LogDebug("Finding main window for Android application process: {ProcessId}", processId);

                // Для Android приложений создаем виртуальное окно
                // так как они не имеют реального Windows Handle
                var virtualWindow = WindowInfo.Create(
                    handle: IntPtr.Zero,
                    title: $"{application.Name} (Android)",
                    processId: (uint)processId
                );

                // Устанавливаем дополнительную информацию
                virtualWindow.ClassName = "AndroidWindowClass";
                virtualWindow.IsVisible = true;
                virtualWindow.IsResponding = true;
                virtualWindow.AdditionalInfo = $"Virtual window for Android package";

                await Task.Delay(10); // Минимальная задержка для async контракта

                _logger.LogDebug("Created virtual window for Android application: {Title}", virtualWindow.Title);
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
                        var stopResult = await _androidManager.StopAndroidAppAsync(packageName);
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
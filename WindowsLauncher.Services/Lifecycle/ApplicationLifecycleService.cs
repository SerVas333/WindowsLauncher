using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;
using Timer = System.Timers.Timer;

namespace WindowsLauncher.Services.Lifecycle
{
    /// <summary>
    /// Главный сервис управления жизненным циклом приложений
    /// Координирует работу всех компонентов системы управления приложениями
    /// </summary>
    public class ApplicationLifecycleService : IApplicationLifecycleService, IDisposable
    {
        private readonly ILogger<ApplicationLifecycleService> _logger;
        private readonly IApplicationInstanceManager _instanceManager;
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        private readonly IEnumerable<IApplicationLauncher> _launchers;
        private readonly IAuditService _auditService;
        
        private readonly Timer _monitoringTimer;
        private readonly SemaphoreSlim _monitoringSemaphore;
        private bool _isMonitoring;
        private bool _disposed;
        
        // События
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceStarted;
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceStopped;
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceStateChanged;
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceActivated;
        public event EventHandler<ApplicationInstanceEventArgs>? InstanceUpdated;
        
        public ApplicationLifecycleService(
            ILogger<ApplicationLifecycleService> logger,
            IApplicationInstanceManager instanceManager,
            IWindowManager windowManager,
            IProcessMonitor processMonitor,
            IEnumerable<IApplicationLauncher> launchers,
            IAuditService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _processMonitor = processMonitor ?? throw new ArgumentNullException(nameof(processMonitor));
            _launchers = launchers ?? throw new ArgumentNullException(nameof(launchers));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            _monitoringSemaphore = new SemaphoreSlim(1, 1);
            
            // Настраиваем таймер мониторинга (каждые 5 секунд)
            _monitoringTimer = new Timer(5000);
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.AutoReset = true;
            
            // Подписываемся на события менеджера экземпляров
            _instanceManager.InstanceAdded += OnInstanceAdded;
            _instanceManager.InstanceRemoved += OnInstanceRemoved;
            _instanceManager.InstanceUpdated += OnInstanceUpdated;
            
            // Подписываемся на события монитора процессов
            _processMonitor.ProcessExited += OnProcessExited;
            _processMonitor.ProcessNotResponding += OnProcessNotResponding;
            
            // Подписываемся на события лаунчеров (например, WebView2ApplicationLauncher)
            SubscribeToLauncherEvents();
            
            _logger.LogInformation("ApplicationLifecycleService initialized with {LauncherCount} launchers", 
                _launchers.Count());
        }
        
        #region Запуск приложений
        
        public async Task<LaunchResult> LaunchAsync(Application application, string launchedBy)
        {
            if (application == null)
            {
                var error = "Application parameter is null";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            if (string.IsNullOrEmpty(launchedBy))
            {
                var error = "LaunchedBy parameter is null or empty";
                _logger.LogError(error);
                return LaunchResult.Failure(error);
            }
            
            var startTime = DateTime.Now;
            _logger.LogInformation("Launching application: {AppName} (Type: {Type}) for user {User}", 
                application.Name, application.Type, launchedBy);
            
            try
            {
                // Находим подходящий лаунчер
                var launcher = FindLauncherForApplication(application);
                if (launcher == null)
                {
                    var error = $"No launcher found for application type: {application.Type}";
                    _logger.LogError(error);
                    await _auditService.LogApplicationLaunchAsync(
                        application.Id, application.Name, launchedBy, false, error);
                    return LaunchResult.Failure(error);
                }
                
                _logger.LogDebug("Using launcher: {LauncherType} for application {AppName}", 
                    launcher.GetType().Name, application.Name);
                
                // Проверяем, не запущено ли приложение уже (для некоторых типов)
                var existingInstance = await launcher.FindExistingInstanceAsync(application);
                if (existingInstance != null && !AllowMultipleInstances(application))
                {
                    _logger.LogInformation("Application {AppName} is already running (Instance: {InstanceId})", 
                        application.Name, existingInstance.InstanceId);
                    
                    // Пытаемся активировать существующий экземпляр
                    bool activated = await SwitchToAsync(existingInstance.InstanceId);
                    
                    var launchDuration = DateTime.Now - startTime;
                    await _auditService.LogApplicationLaunchAsync(
                        application.Id, application.Name, launchedBy, true, "Activated existing instance");
                    
                    return LaunchResult.AlreadyRunning(existingInstance, activated);
                }
                
                // Запускаем приложение
                var launchResult = await launcher.LaunchAsync(application, launchedBy);
                
                if (!launchResult.IsSuccess)
                {
                    _logger.LogError("Failed to launch application {AppName}: {Error}", 
                        application.Name, launchResult.ErrorMessage);
                    
                    await _auditService.LogApplicationLaunchAsync(
                        application.Id, application.Name, launchedBy, false, launchResult.ErrorMessage);
                    
                    return launchResult;
                }
                
                // Регистрируем экземпляр
                if (launchResult.Instance != null)
                {
                    bool registered = await _instanceManager.AddInstanceAsync(launchResult.Instance);
                    
                    if (!registered)
                    {
                        _logger.LogWarning("Failed to register launched instance {InstanceId}", 
                            launchResult.Instance.InstanceId);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully launched and registered {AppName} (Instance: {InstanceId}, PID: {ProcessId})", 
                            application.Name, launchResult.Instance.InstanceId, launchResult.Instance.ProcessId);
                    }
                }
                
                // Запускаем мониторинг если еще не запущен
                if (!_isMonitoring)
                {
                    await StartMonitoringAsync();
                }
                
                var finalLaunchDuration = DateTime.Now - startTime;
                launchResult.LaunchDuration = finalLaunchDuration;
                
                await _auditService.LogApplicationLaunchAsync(
                    application.Id, application.Name, launchedBy, true, $"Launched in {finalLaunchDuration.TotalMilliseconds:F0}ms");
                
                return launchResult;
            }
            catch (Exception ex)
            {
                var error = $"Unexpected error launching application: {ex.Message}";
                _logger.LogError(ex, "Error launching application {AppName} for user {User}", 
                    application.Name, launchedBy);
                
                await _auditService.LogApplicationLaunchAsync(
                    application.Id, application.Name, launchedBy, false, error);
                
                return LaunchResult.Failure(ex, error);
            }
        }
        
        public async Task<ApplicationInstance?> RegisterExistingAsync(Application application, int processId, string launchedBy)
        {
            if (application == null || processId <= 0 || string.IsNullOrEmpty(launchedBy))
            {
                _logger.LogError("Invalid parameters for RegisterExistingAsync");
                return null;
            }
            
            try
            {
                _logger.LogInformation("Registering existing process {ProcessId} as application {AppName}", 
                    processId, application.Name);
                
                // Проверяем что процесс существует
                bool processExists = await _processMonitor.IsProcessAliveAsync(processId);
                if (!processExists)
                {
                    _logger.LogWarning("Cannot register non-existent process {ProcessId}", processId);
                    return null;
                }
                
                // Получаем Process объект
                var process = await _processMonitor.GetProcessSafelyAsync(processId);
                if (process == null)
                {
                    _logger.LogWarning("Cannot get Process object for PID {ProcessId}", processId);
                    return null;
                }
                
                // Создаем экземпляр приложения
                var instance = ApplicationInstance.CreateFromProcess(application, process, launchedBy);
                instance.State = ApplicationState.Running; // Считаем что уже запущено
                
                // Пытаемся найти главное окно
                var launcher = FindLauncherForApplication(application);
                if (launcher != null)
                {
                    var windowInfo = await launcher.FindMainWindowAsync(processId, application);
                    if (windowInfo != null)
                    {
                        instance.MainWindow = windowInfo;
                        instance.State = ApplicationState.Running;
                    }
                }
                
                // Регистрируем экземпляр
                bool registered = await _instanceManager.AddInstanceAsync(instance);
                
                if (registered)
                {
                    _logger.LogInformation("Successfully registered existing process {ProcessId} as {AppName} (Instance: {InstanceId})", 
                        processId, application.Name, instance.InstanceId);
                    
                    // Запускаем мониторинг если еще не запущен
                    if (!_isMonitoring)
                    {
                        await StartMonitoringAsync();
                    }
                    
                    return instance;
                }
                else
                {
                    _logger.LogError("Failed to register existing process {ProcessId} as {AppName}", 
                        processId, application.Name);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering existing process {ProcessId} as {AppName}", 
                    processId, application.Name);
                return null;
            }
        }
        
        private IApplicationLauncher? FindLauncherForApplication(Application application)
        {
            // Ищем лаунчеры, которые могут обработать данное приложение
            var suitableLaunchers = _launchers
                .Where(launcher => launcher.SupportedType == application.Type && launcher.CanLaunch(application))
                .OrderByDescending(launcher => launcher.Priority) // Сортируем по приоритету
                .ToList();
            
            if (suitableLaunchers.Count == 0)
            {
                _logger.LogError("No suitable launcher found for application {AppName} (Type: {Type})", 
                    application.Name, application.Type);
                return null;
            }
            
            if (suitableLaunchers.Count > 1)
            {
                _logger.LogDebug("Multiple launchers found for {AppName}, using highest priority: {LauncherType}", 
                    application.Name, suitableLaunchers[0].GetType().Name);
            }
            
            return suitableLaunchers[0];
        }
        
        private bool AllowMultipleInstances(Application application)
        {
            // Папки могут открываться несколько раз
            if (application.Type == ApplicationType.Folder)
                return true;
            
            // Для всех остальных типов (Desktop, ChromeApp, Web) - 
            // только один экземпляр каждого конкретного приложения
            return false;
        }
        
        #endregion
        
        #region Управление экземплярами
        
        public async Task<bool> SwitchToAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                _logger.LogWarning("SwitchToAsync called with empty instanceId");
                return false;
            }
            
            try
            {
                var instance = await _instanceManager.GetInstanceAsync(instanceId);
                if (instance == null)
                {
                    _logger.LogWarning("Cannot switch to non-existent instance {InstanceId}", instanceId);
                    return false;
                }
                
                if (!instance.State.CanSwitchTo())
                {
                    _logger.LogWarning("Cannot switch to instance {InstanceId} in state {State}", 
                        instanceId, instance.State);
                    return false;
                }
                
                _logger.LogDebug("Switching to instance {InstanceId} ({AppName})", 
                    instanceId, instance.Application?.Name);
                
                // Проверяем что процесс еще жив
                bool processAlive = await _processMonitor.IsProcessAliveAsync(instance.ProcessId);
                if (!processAlive)
                {
                    _logger.LogWarning("Cannot switch to instance {InstanceId} - process {ProcessId} is not alive", 
                        instanceId, instance.ProcessId);
                    
                    // Обновляем состояние экземпляра
                    var previousState = instance.State;
                    instance.State = ApplicationState.Terminated;
                    instance.EndTime = DateTime.Now;
                    await _instanceManager.UpdateInstanceAsync(instance);
                    
                    // Генерируем событие изменения состояния
                    RaiseInstanceStateChanged(instance, previousState, ApplicationState.Terminated, "Process not alive");
                    
                    return false;
                }
                
                // Получаем handle окна
                IntPtr windowHandle = IntPtr.Zero;
                
                if (instance.MainWindow != null && instance.MainWindow.IsValidHandle())
                {
                    // Проверяем что окно еще валидно
                    bool windowValid = await _windowManager.IsWindowValidAsync(instance.MainWindow.Handle);
                    if (windowValid)
                    {
                        windowHandle = instance.MainWindow.Handle;
                    }
                }
                
                // Если окно недоступно, пытаемся найти заново
                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogDebug("Main window handle not available for instance {InstanceId}, searching...", instanceId);
                    
                    var launcher = FindLauncherForApplication(instance.Application);
                    if (launcher != null)
                    {
                        var windowInfo = await launcher.FindMainWindowAsync(instance.ProcessId, instance.Application);
                        if (windowInfo != null)
                        {
                            windowHandle = windowInfo.Handle;
                            instance.MainWindow = windowInfo;
                            await _instanceManager.UpdateInstanceAsync(instance);
                        }
                    }
                }
                
                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogWarning("No window handle found for instance {InstanceId}", instanceId);
                    return false;
                }
                
                // Переключаемся на окно
                bool switched = await _windowManager.SwitchToWindowAsync(windowHandle);
                
                if (switched)
                {
                    var previousState = instance.State;
                    instance.State = ApplicationState.Active;
                    instance.IsActive = true;
                    instance.IsMinimized = false;
                    instance.LastUpdate = DateTime.Now;
                    
                    await _instanceManager.UpdateInstanceAsync(instance);
                    
                    _logger.LogInformation("Successfully switched to instance {InstanceId} ({AppName})", 
                        instanceId, instance.Application?.Name);
                    
                    // Генерируем события
                    RaiseInstanceActivated(instance);
                    
                    if (previousState != ApplicationState.Active)
                    {
                        RaiseInstanceStateChanged(instance, previousState, ApplicationState.Active, "Switched to window");
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to switch to window for instance {InstanceId}", instanceId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to instance {InstanceId}", instanceId);
                return false;
            }
        }
        
        public async Task<bool> MinimizeAsync(string instanceId)
        {
            return await PerformWindowOperation(instanceId, "minimize", 
                (windowHandle) => _windowManager.MinimizeWindowAsync(windowHandle),
                ApplicationState.Minimized);
        }
        
        public async Task<bool> RestoreAsync(string instanceId)
        {
            return await PerformWindowOperation(instanceId, "restore", 
                (windowHandle) => _windowManager.RestoreWindowAsync(windowHandle),
                ApplicationState.Running);
        }
        
        public async Task<bool> CloseAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                _logger.LogWarning("CloseAsync called with empty instanceId");
                return false;
            }
            
            try
            {
                var instance = await _instanceManager.GetInstanceAsync(instanceId);
                if (instance == null)
                {
                    _logger.LogWarning("Cannot close non-existent instance {InstanceId}", instanceId);
                    return false;
                }
                
                _logger.LogInformation("Closing instance {InstanceId} ({AppName}) gracefully", 
                    instanceId, instance.Application?.Name);
                
                // Обновляем состояние
                var previousState = instance.State;
                instance.State = ApplicationState.Closing;
                await _instanceManager.UpdateInstanceAsync(instance);
                RaiseInstanceStateChanged(instance, previousState, ApplicationState.Closing, "Close requested");
                
                // Пытаемся закрыть корректно
                bool closed = await _processMonitor.CloseProcessGracefullyAsync(instance.ProcessId, 5000);
                
                if (closed)
                {
                    _logger.LogInformation("Instance {InstanceId} closed gracefully", instanceId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to close instance {InstanceId} gracefully", instanceId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing instance {InstanceId}", instanceId);
                return false;
            }
        }
        
        public async Task<bool> KillAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                _logger.LogWarning("KillAsync called with empty instanceId");
                return false;
            }
            
            try
            {
                var instance = await _instanceManager.GetInstanceAsync(instanceId);
                if (instance == null)
                {
                    _logger.LogWarning("Cannot kill non-existent instance {InstanceId}", instanceId);
                    return false;
                }
                
                _logger.LogWarning("Force killing instance {InstanceId} ({AppName})", 
                    instanceId, instance.Application?.Name);
                
                // Обновляем состояние
                var previousState = instance.State;
                instance.State = ApplicationState.Closing;
                await _instanceManager.UpdateInstanceAsync(instance);
                RaiseInstanceStateChanged(instance, previousState, ApplicationState.Closing, "Force kill requested");
                
                // Принудительно завершаем
                bool killed = await _processMonitor.KillProcessAsync(instance.ProcessId, 3000);
                
                if (killed)
                {
                    _logger.LogInformation("Instance {InstanceId} killed successfully", instanceId);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to kill instance {InstanceId}", instanceId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error killing instance {InstanceId}", instanceId);
                return false;
            }
        }
        
        private async Task<bool> PerformWindowOperation(
            string instanceId, 
            string operationName, 
            Func<IntPtr, Task<bool>> operation, 
            ApplicationState newState)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                _logger.LogWarning("{Operation} called with empty instanceId", operationName);
                return false;
            }
            
            try
            {
                var instance = await _instanceManager.GetInstanceAsync(instanceId);
                if (instance == null)
                {
                    _logger.LogWarning("Cannot {Operation} non-existent instance {InstanceId}", operationName, instanceId);
                    return false;
                }
                
                if (instance.MainWindow?.Handle == IntPtr.Zero)
                {
                    _logger.LogWarning("Cannot {Operation} instance {InstanceId} - no window handle", operationName, instanceId);
                    return false;
                }
                
                bool success = await operation(instance.MainWindow.Handle);
                
                if (success)
                {
                    var previousState = instance.State;
                    instance.State = newState;
                    instance.IsMinimized = (newState == ApplicationState.Minimized);
                    instance.IsActive = (newState == ApplicationState.Active);
                    instance.LastUpdate = DateTime.Now;
                    
                    await _instanceManager.UpdateInstanceAsync(instance);
                    
                    _logger.LogDebug("Successfully performed {Operation} on instance {InstanceId}", operationName, instanceId);
                    
                    if (previousState != newState)
                    {
                        RaiseInstanceStateChanged(instance, previousState, newState, $"{operationName} operation");
                    }
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing {Operation} on instance {InstanceId}", operationName, instanceId);
                return false;
            }
        }
        
        #endregion
        
        #region Получение информации
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetRunningAsync()
        {
            return await _instanceManager.GetAllInstancesAsync();
        }
        
        public async Task<ApplicationInstance?> GetByIdAsync(string instanceId)
        {
            return await _instanceManager.GetInstanceAsync(instanceId);
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetByApplicationIdAsync(int applicationId)
        {
            return await _instanceManager.GetInstancesByApplicationIdAsync(applicationId);
        }
        
        public async Task<IReadOnlyList<ApplicationInstance>> GetByUserAsync(string username)
        {
            return await _instanceManager.GetInstancesByUserAsync(username);
        }
        
        public async Task<int> GetCountAsync()
        {
            return await _instanceManager.GetTotalCountAsync();
        }
        
        public async Task<long> GetTotalMemoryUsageAsync()
        {
            return await _instanceManager.GetTotalMemoryUsageAsync();
        }
        
        public async Task<bool> IsApplicationRunningAsync(int applicationId)
        {
            var instances = await _instanceManager.GetInstancesByApplicationIdAsync(applicationId);
            return instances.Any(instance => instance.IsActiveInstance());
        }
        
        #endregion
        
        #region Мониторинг и жизненный цикл сервиса
        
        public async Task StartMonitoringAsync()
        {
            await _monitoringSemaphore.WaitAsync();
            try
            {
                if (!_isMonitoring)
                {
                    _isMonitoring = true;
                    _monitoringTimer.Start();
                    
                    _logger.LogInformation("Application lifecycle monitoring started");
                }
            }
            finally
            {
                _monitoringSemaphore.Release();
            }
        }
        
        public async Task StopMonitoringAsync()
        {
            await _monitoringSemaphore.WaitAsync();
            try
            {
                if (_isMonitoring)
                {
                    _isMonitoring = false;
                    _monitoringTimer.Stop();
                    
                    _logger.LogInformation("Application lifecycle monitoring stopped");
                }
            }
            finally
            {
                _monitoringSemaphore.Release();
            }
        }
        
        public async Task RefreshAllAsync()
        {
            try
            {
                _logger.LogDebug("Refreshing all application instances");
                
                var allInstances = await _instanceManager.GetAllInstancesAsync();
                var activeTasks = new List<Task>();
                
                foreach (var instance in allInstances.Where(i => i.IsActiveInstance()))
                {
                    activeTasks.Add(RefreshInstanceAsync(instance));
                }
                
                await Task.WhenAll(activeTasks);
                
                _logger.LogDebug("Refreshed {Count} active instances", activeTasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing all instances");
            }
        }
        
        public async Task CleanupAsync()
        {
            try
            {
                int removedCount = await _instanceManager.CleanupTerminatedInstancesAsync();
                
                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {RemovedCount} terminated instances", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }
        
        private async void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!_isMonitoring) return;
            
            try
            {
                await RefreshAllAsync();
                await CleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monitoring timer elapsed");
            }
        }
        
        private async Task RefreshInstanceAsync(ApplicationInstance instance)
        {
            try
            {
                // Проверяем что процесс еще жив
                bool processAlive = await _processMonitor.IsProcessAliveAsync(instance.ProcessId);
                
                if (!processAlive)
                {
                    // Процесс завершился
                    var previousState = instance.State;
                    instance.State = ApplicationState.Terminated;
                    instance.EndTime = DateTime.Now;
                    instance.LastUpdate = DateTime.Now;
                    
                    await _instanceManager.UpdateInstanceAsync(instance);
                    
                    _logger.LogDebug("Instance {InstanceId} process {ProcessId} has terminated", 
                        instance.InstanceId, instance.ProcessId);
                    
                    if (previousState.IsActive())
                    {
                        RaiseInstanceStateChanged(instance, previousState, ApplicationState.Terminated, "Process exited");
                    }
                    
                    return;
                }
                
                // Получаем актуальную информацию о процессе
                var processInfo = await _processMonitor.GetProcessInfoAsync(instance.ProcessId);
                if (processInfo != null)
                {
                    // Обновляем информацию об экземпляре
                    var previousState = instance.State;
                    
                    instance.MemoryUsageMB = (long)processInfo.GetMemoryUsageMB();
                    instance.IsResponding = processInfo.IsResponding;
                    instance.LastUpdate = DateTime.Now;
                    
                    // Определяем новое состояние
                    ApplicationState newState = DetermineInstanceState(instance, processInfo);
                    
                    if (newState != previousState)
                    {
                        instance.State = newState;
                        await _instanceManager.UpdateInstanceAsync(instance);
                        
                        RaiseInstanceStateChanged(instance, previousState, newState, "State updated from monitoring");
                    }
                    else
                    {
                        await _instanceManager.UpdateInstanceAsync(instance);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing instance {InstanceId}", instance.InstanceId);
            }
        }
        
        private ApplicationState DetermineInstanceState(ApplicationInstance instance, ProcessInfo processInfo)
        {
            if (!processInfo.IsAlive || processInfo.HasExited)
                return ApplicationState.Terminated;
            
            if (!processInfo.IsResponding)
                return ApplicationState.NotResponding;
            
            // Проверяем состояние окна если доступно
            if (instance.MainWindow != null && instance.MainWindow.IsValidHandle())
            {
                try
                {
                    bool isMinimized = _windowManager.IsWindowMinimizedAsync(instance.MainWindow.Handle).Result;
                    bool isActive = _windowManager.IsWindowActiveAsync(instance.MainWindow.Handle).Result;
                    
                    instance.IsMinimized = isMinimized;
                    instance.IsActive = isActive;
                    
                    if (isActive)
                        return ApplicationState.Active;
                    
                    if (isMinimized)
                        return ApplicationState.Minimized;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error checking window state for instance {InstanceId}", instance.InstanceId);
                }
            }
            
            return ApplicationState.Running;
        }
        
        #endregion
        
        #region Обработчики событий
        
        private void OnInstanceAdded(object? sender, ApplicationInstanceEventArgs e)
        {
            RaiseInstanceStarted(e.Instance);
        }
        
        private void OnInstanceRemoved(object? sender, ApplicationInstanceEventArgs e)
        {
            RaiseInstanceStopped(e.Instance);
        }
        
        private void OnInstanceUpdated(object? sender, ApplicationInstanceEventArgs e)
        {
            RaiseInstanceUpdated(e.Instance);
        }
        
        private void OnProcessExited(object? sender, ProcessExitedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Находим экземпляры с данным PID
                    var instances = await _instanceManager.GetInstancesByProcessIdAsync(e.ProcessId);
                    
                    foreach (var instance in instances)
                    {
                        var previousState = instance.State;
                        instance.State = ApplicationState.Terminated;
                        instance.EndTime = e.ExitTime;
                        instance.LastUpdate = DateTime.Now;
                        
                        await _instanceManager.UpdateInstanceAsync(instance);
                        
                        _logger.LogInformation("Instance {InstanceId} terminated due to process exit (PID: {ProcessId})", 
                            instance.InstanceId, e.ProcessId);
                        
                        if (previousState.IsActive())
                        {
                            RaiseInstanceStateChanged(instance, previousState, ApplicationState.Terminated, 
                                e.IsExpected ? "Expected process exit" : "Unexpected process exit");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling process exited event for PID {ProcessId}", e.ProcessId);
                }
            });
        }
        
        private void OnProcessNotResponding(object? sender, ProcessNotRespondingEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Находим экземпляры с данным PID
                    var instances = await _instanceManager.GetInstancesByProcessIdAsync(e.ProcessId);
                    
                    foreach (var instance in instances)
                    {
                        if (instance.State != ApplicationState.NotResponding)
                        {
                            var previousState = instance.State;
                            instance.State = ApplicationState.NotResponding;
                            instance.IsResponding = false;
                            instance.LastUpdate = DateTime.Now;
                            
                            await _instanceManager.UpdateInstanceAsync(instance);
                            
                            _logger.LogWarning("Instance {InstanceId} marked as not responding (PID: {ProcessId})", 
                                instance.InstanceId, e.ProcessId);
                            
                            RaiseInstanceStateChanged(instance, previousState, ApplicationState.NotResponding, 
                                $"Process not responding for {e.Duration}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling process not responding event for PID {ProcessId}", e.ProcessId);
                }
            });
        }
        
        #endregion
        
        #region Обработчики событий лаунчеров
        
        /// <summary>
        /// Подписывается на события лаунчеров для интеграции с жизненным циклом
        /// </summary>
        private void SubscribeToLauncherEvents()
        {
            foreach (var launcher in _launchers)
            {
                var launcherType = launcher.GetType();
                
                // Проверяем является ли лаунчер WebView2ApplicationLauncher или TextEditorApplicationLauncher через рефлексию
                if (launcherType.Name == "WebView2ApplicationLauncher" || launcherType.Name == "TextEditorApplicationLauncher")
                {
                    try
                    {
                        // Подписываемся на события через рефлексию
                        var windowActivatedEvent = launcherType.GetEvent("WindowActivated");
                        var windowDeactivatedEvent = launcherType.GetEvent("WindowDeactivated");
                        var windowClosedEvent = launcherType.GetEvent("WindowClosed");
                        var windowStateChangedEvent = launcherType.GetEvent("WindowStateChanged");

                        if (windowActivatedEvent != null)
                        {
                            var handler = new EventHandler<ApplicationInstance>(OnLauncherWindowActivated);
                            windowActivatedEvent.AddEventHandler(launcher, handler);
                        }
                        
                        if (windowDeactivatedEvent != null)
                        {
                            var handler = new EventHandler<ApplicationInstance>(OnLauncherWindowDeactivated);
                            windowDeactivatedEvent.AddEventHandler(launcher, handler);
                        }
                        
                        if (windowClosedEvent != null)
                        {
                            var handler = new EventHandler<ApplicationInstance>(OnLauncherWindowClosed);
                            windowClosedEvent.AddEventHandler(launcher, handler);
                        }
                        
                        if (windowStateChangedEvent != null)
                        {
                            var handler = new EventHandler<ApplicationInstance>(OnLauncherWindowStateChanged);
                            windowStateChangedEvent.AddEventHandler(launcher, handler);
                        }
                        
                        _logger.LogDebug("Subscribed to {LauncherType} events via reflection", launcherType.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to subscribe to {LauncherType} events", launcherType.Name);
                    }
                }
            }
        }
        
        /// <summary>
        /// Обработчик активации окна лаунчера
        /// </summary>
        private void OnLauncherWindowActivated(object? sender, ApplicationInstance instance)
        {
            try
            {
                _logger.LogTrace("Launcher window activated for {AppName} (Instance: {InstanceId})", 
                    instance.Application?.Name, instance.InstanceId);
                
                // Обновляем состояние экземпляра
                instance.IsActive = true;
                instance.LastUpdate = DateTime.Now;
                
                // Регистрируем экземпляр в менеджере если его еще нет
                Task.Run(async () =>
                {
                    try
                    {
                        var existingInstance = await _instanceManager.GetInstanceAsync(instance.InstanceId);
                        if (existingInstance == null)
                        {
                            await _instanceManager.AddInstanceAsync(instance);
                            _logger.LogDebug("Registered new WebView2 instance {InstanceId} in manager", instance.InstanceId);
                        }
                        else
                        {
                            // Обновляем существующий экземпляр
                            existingInstance.IsActive = true;
                            existingInstance.LastUpdate = DateTime.Now;
                            await _instanceManager.UpdateInstanceAsync(existingInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error registering/updating WebView2 instance {InstanceId}", instance.InstanceId);
                    }
                });
                
                // Уведомляем через событие
                RaiseInstanceActivated(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling launcher window activation for {InstanceId}", instance.InstanceId);
            }
        }
        
        /// <summary>
        /// Обработчик деактивации окна лаунчера
        /// </summary>
        private void OnLauncherWindowDeactivated(object? sender, ApplicationInstance instance)
        {
            try
            {
                _logger.LogTrace("Launcher window deactivated for {AppName} (Instance: {InstanceId})", 
                    instance.Application?.Name, instance.InstanceId);
                
                // Обновляем состояние экземпляра
                instance.IsActive = false;
                instance.LastUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling launcher window deactivation for {InstanceId}", instance.InstanceId);
            }
        }
        
        /// <summary>
        /// Обработчик закрытия окна лаунчера
        /// </summary>
        private void OnLauncherWindowClosed(object? sender, ApplicationInstance instance)
        {
            try
            {
                _logger.LogDebug("Launcher window closed for {AppName} (Instance: {InstanceId})", 
                    instance.Application?.Name, instance.InstanceId);
                
                // Удаляем экземпляр из менеджера
                Task.Run(async () =>
                {
                    try
                    {
                        await _instanceManager.RemoveInstanceAsync(instance.InstanceId);
                        _logger.LogDebug("Removed instance {InstanceId} after window closure", instance.InstanceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing instance {InstanceId} after window closure", instance.InstanceId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling launcher window closure for {InstanceId}", instance.InstanceId);
            }
        }
        
        /// <summary>
        /// Обработчик изменения состояния окна лаунчера
        /// </summary>
        private void OnLauncherWindowStateChanged(object? sender, ApplicationInstance instance)
        {
            try
            {
                _logger.LogTrace("Launcher window state changed for {AppName} (Instance: {InstanceId})", 
                    instance.Application?.Name, instance.InstanceId);
                
                // Обновляем состояние экземпляра
                instance.LastUpdate = DateTime.Now;
                
                // Уведомляем об обновлении
                RaiseInstanceUpdated(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling launcher window state change for {InstanceId}", instance.InstanceId);
            }
        }
        
        #endregion
        
        #region Генерация событий
        
        private void RaiseInstanceStarted(ApplicationInstance instance)
        {
            try
            {
                var args = ApplicationInstanceEventArgs.Started(instance, "ApplicationLifecycleService");
                InstanceStarted?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceStarted event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceStopped(ApplicationInstance instance)
        {
            try
            {
                var args = ApplicationInstanceEventArgs.Stopped(instance, "Instance stopped", "ApplicationLifecycleService");
                InstanceStopped?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceStopped event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceStateChanged(ApplicationInstance instance, ApplicationState previousState, ApplicationState newState, string reason)
        {
            try
            {
                var args = ApplicationInstanceEventArgs.StateChanged(instance, previousState, newState, reason, "ApplicationLifecycleService");
                InstanceStateChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceStateChanged event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceActivated(ApplicationInstance instance)
        {
            try
            {
                var args = ApplicationInstanceEventArgs.Activated(instance, "ApplicationLifecycleService");
                InstanceActivated?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceActivated event for {InstanceId}", instance.InstanceId);
            }
        }
        
        private void RaiseInstanceUpdated(ApplicationInstance instance)
        {
            try
            {
                var args = ApplicationInstanceEventArgs.Updated(instance, "Instance data updated", "ApplicationLifecycleService");
                InstanceUpdated?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising InstanceUpdated event for {InstanceId}", instance.InstanceId);
            }
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                // Останавливаем мониторинг
                StopMonitoringAsync().Wait(1000);
                
                // Освобождаем ресурсы
                _monitoringTimer?.Dispose();
                _monitoringSemaphore?.Dispose();
                
                // Отписываемся от событий
                if (_instanceManager != null)
                {
                    _instanceManager.InstanceAdded -= OnInstanceAdded;
                    _instanceManager.InstanceRemoved -= OnInstanceRemoved;
                    _instanceManager.InstanceUpdated -= OnInstanceUpdated;
                }
                
                if (_processMonitor != null)
                {
                    _processMonitor.ProcessExited -= OnProcessExited;
                    _processMonitor.ProcessNotResponding -= OnProcessNotResponding;
                }
                
                // Очищаем события
                InstanceStarted = null;
                InstanceStopped = null;
                InstanceStateChanged = null;
                InstanceActivated = null;
                InstanceUpdated = null;
                
                _logger.LogInformation("ApplicationLifecycleService disposed");
            }
        }
        
        #endregion
    }
}
# Technical Specification

This is the technical specification for the spec detailed in @.agent-os/specs/2025-08-12-android-window-integration/spec.md

## Technical Requirements

### 1. Расширение интерфейса IApplicationLauncher

**Цель:** Добавить событийную модель для интеграции лаунчеров с AppSwitcher и системой управления жизненным циклом

**Новые события:**
```csharp
public interface IApplicationLauncher
{
    // Существующие методы остаются без изменений...
    
    /// <summary>
    /// Событие активации окна приложения (для интеграции с AppSwitcher)
    /// </summary>
    event EventHandler<ApplicationInstance>? WindowActivated;
    
    /// <summary>
    /// Событие закрытия окна приложения (для управления жизненным циклом)
    /// </summary>
    event EventHandler<ApplicationInstance>? WindowClosed;
}
```

**Backward Compatibility:** Все существующие лаунчеры продолжат работать без изменений, так как события опциональные

### 2. Расширение интерфейса IWindowManager

**Цель:** Добавить специализированные методы для работы с WSA-окнами

**Новые методы:**
```csharp
public interface IWindowManager
{
    // Существующие методы остаются без изменений...
    
    /// <summary>
    /// Найти WSA-окно по package name и activity name
    /// </summary>
    Task<WindowInfo?> FindWSAWindowAsync(string packageName, string activityName = "");
    
    /// <summary>
    /// Получить все WSA-окна в системе
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> GetWSAWindowsAsync();
    
    /// <summary>
    /// Проверить, является ли окно WSA-окном
    /// </summary>
    Task<bool> IsWSAWindowAsync(IntPtr windowHandle);
}
```

### 3. Алгоритм обнаружения WSA-окон

**Проблема:** WSA-приложения создают окна с классом "ApplicationFrameWindow", но нет прямой связи между Android package name и Windows handle

**Решение - Корреляционный алгоритм:**
1. **По времени запуска:** Искать окна, созданные в течение 30 секунд после запуска Android-приложения
2. **По заголовку окна:** WSA-окна содержат название Android-приложения в title
3. **По классу окна:** Фильтровать только окна с классом "ApplicationFrameWindow"
4. **По процессу:** Проверять, что окно принадлежит процессу WSA

**Алгоритм поиска:**
```csharp
private async Task<WindowInfo?> FindWSAWindowForPackage(string packageName, string appName, DateTime launchTime)
{
    // 1. Получить все окна класса ApplicationFrameWindow
    var candidateWindows = await _windowManager.FindWindowsByClassAsync("ApplicationFrameWindow");
    
    // 2. Фильтровать по времени создания (30 секунд после запуска)
    var timeFilteredWindows = candidateWindows
        .Where(w => w.CreatedAt >= launchTime.AddSeconds(-30) && 
                   w.CreatedAt <= launchTime.AddSeconds(30));
    
    // 3. Искать по заголовку (содержит название приложения)
    var titleMatch = timeFilteredWindows
        .FirstOrDefault(w => w.Title.Contains(appName, StringComparison.OrdinalIgnoreCase));
    
    if (titleMatch != null)
        return titleMatch;
    
    // 4. Fallback: первое найденное окно в временном диапазоне
    return timeFilteredWindows.FirstOrDefault();
}
```

### 4. Реализация в WSAApplicationLauncher

**Изменения в CreateApplicationInstance:**
```csharp
private async Task<ApplicationInstance> CreateApplicationInstance(
    Application application, 
    string instanceId, 
    string packageName, 
    AppLaunchResult launchResult,
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

    // НОВОЕ: Попытка найти реальное WSA-окно
    try 
    {
        var wsaWindow = await FindWSAWindowForPackage(packageName, application.Name, instance.StartTime);
        if (wsaWindow != null)
        {
            // Используем реальное окно вместо виртуального
            instance.MainWindow = wsaWindow;
            _logger.LogInformation("Found real WSA window for {PackageName}: {WindowTitle}", 
                packageName, wsaWindow.Title);
        }
        else
        {
            // Fallback на виртуальное окно (как сейчас)
            instance.MainWindow = WindowInfo.Create(
                handle: IntPtr.Zero,
                title: $"{application.Name} (Android)",
                processId: (uint)(launchResult.ProcessId ?? 0)
            );
            _logger.LogWarning("Could not find real WSA window for {PackageName}, using virtual window", packageName);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error finding WSA window for {PackageName}, using virtual window", packageName);
        // Fallback на виртуальное окно
        instance.MainWindow = WindowInfo.Create(IntPtr.Zero, $"{application.Name} (Android)", 0);
    }

    // Добавляем Android-специфичные метаданные
    instance.Metadata["AndroidPackageName"] = packageName;
    instance.Metadata["AndroidActivity"] = launchResult.ActivityName ?? "MainActivity";
    instance.Metadata["LauncherType"] = "WSA";
    
    return instance;
}
```

### 5. Мониторинг жизненного цикла окон

**Проблема:** Нужно отслеживать закрытие Windows-окон Android-приложений для автоматического завершения Android-процессов

**Решение - Polling с оптимизацией:**
```csharp
private readonly Timer _windowMonitorTimer;

// В конструкторе WSAApplicationLauncher
_windowMonitorTimer = new Timer(MonitorWindowStates, null, 
    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

private async void MonitorWindowStates(object? state)
{
    try
    {
        var activeInstances = _runningInstances.Values
            .Where(i => i.State == ApplicationState.Running && i.MainWindow?.Handle != IntPtr.Zero)
            .ToList();

        foreach (var instance in activeInstances)
        {
            // Проверяем, существует ли еще окно
            bool windowExists = await _windowManager.IsWindowValidAsync(instance.MainWindow.Handle);
            
            if (!windowExists)
            {
                _logger.LogInformation("WSA window closed for {AppName}, terminating Android app", 
                    instance.Application.Name);
                
                // Завершаем Android-приложение
                if (instance.Metadata.TryGetValue("AndroidPackageName", out var packageNameObj) &&
                    packageNameObj is string packageName)
                {
                    await _androidManager.StopAndroidAppAsync(packageName);
                }
                
                // Обновляем состояние экземпляра
                instance.State = ApplicationState.Terminated;
                instance.EndTime = DateTime.Now;
                instance.IsActive = false;
                
                // Генерируем событие закрытия окна
                WindowClosed?.Invoke(this, instance);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error monitoring WSA window states");
    }
}
```

### 6. Интеграция с ApplicationLifecycleService

**Автоматическая подписка на события лаунчеров:**
```csharp
// В ApplicationLifecycleService.SubscribeToLauncherEvents()
private void SubscribeToLauncherEvents()
{
    foreach (var launcher in _launchers)
    {
        // Подписываемся на новые события, если они поддерживаются
        if (launcher.GetType().GetEvent("WindowActivated") != null)
        {
            launcher.GetType().GetEvent("WindowActivated")!
                .AddEventHandler(launcher, new EventHandler<ApplicationInstance>(OnLauncherWindowActivated));
        }
        
        if (launcher.GetType().GetEvent("WindowClosed") != null)
        {
            launcher.GetType().GetEvent("WindowClosed")!
                .AddEventHandler(launcher, new EventHandler<ApplicationInstance>(OnLauncherWindowClosed));
        }
    }
}

private void OnLauncherWindowActivated(object? sender, ApplicationInstance instance)
{
    _logger.LogDebug("Window activated for {AppName} via {LauncherType}", 
        instance.Application.Name, sender?.GetType().Name);
    
    // Обновляем состояние и уведомляем AppSwitcher
    InstanceActivated?.Invoke(this, new ApplicationInstanceEventArgs(instance, "Window activated"));
}

private void OnLauncherWindowClosed(object? sender, ApplicationInstance instance)
{
    _logger.LogInformation("Window closed for {AppName} via {LauncherType}", 
        instance.Application.Name, sender?.GetType().Name);
    
    // Удаляем из активных экземпляров
    RemoveInstance(instance.InstanceId);
    
    // Уведомляем подписчиков
    InstanceStopped?.Invoke(this, new ApplicationInstanceEventArgs(instance, "Window closed"));
}
```

### 7. Улучшение SwitchToAsync в WSAApplicationLauncher

**Текущая проблема:** Метод пытается повторно запустить активность вместо переключения на окно

**Новая реализация:**
```csharp
public async Task<bool> SwitchToAsync(string instanceId)
{
    if (!_runningInstances.TryGetValue(instanceId, out var instance))
    {
        return false;
    }

    try
    {
        // Если есть реальное окно - используем WindowManager
        if (instance.MainWindow?.Handle != IntPtr.Zero)
        {
            _logger.LogDebug("Switching to real WSA window for {InstanceId}", instanceId);
            
            bool success = await _windowManager.SwitchToWindowAsync(instance.MainWindow.Handle);
            if (success)
            {
                instance.LastUpdate = DateTime.Now;
                instance.IsActive = true;
                
                // Генерируем событие активации
                WindowActivated?.Invoke(this, instance);
                return true;
            }
        }
        
        // Fallback: повторный запуск активности (как сейчас)
        var packageName = ExtractPackageNameFromExecutablePath(instance.Application.ExecutablePath ?? "");
        if (!string.IsNullOrEmpty(packageName))
        {
            _logger.LogDebug("Fallback to activity launch for {InstanceId}", instanceId);
            var launchResult = await _androidManager.LaunchAndroidAppAsync(packageName);
            return launchResult.Success;
        }
        
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error switching to Android application instance: {InstanceId}", instanceId);
        return false;
    }
}
```

## Performance Considerations

**1. Кэширование WSA-окон:**
- Кэш найденных WSA-окон на 60 секунд для избежания повторных поисков
- Инвалидация кэша при изменении состояния окон

**2. Оптимизация мониторинга:**
- Polling каждые 5 секунд вместо real-time мониторинга
- Мониторинг только активных экземпляров с реальными окнами

**3. Graceful Fallbacks:**
- При невозможности найти реальное окно - использовать виртуальное
- При ошибках переключения на реальное окно - fallback на повторный запуск активности

## External Dependencies

**Новые зависимости не требуются** - используем существующие:
- WindowsLauncher.Core.Interfaces.Lifecycle.IWindowManager
- WindowsLauncher.Core.Interfaces.Android.IAndroidApplicationManager  
- Microsoft.Extensions.Logging
- System.Runtime.InteropServices (для Windows API)
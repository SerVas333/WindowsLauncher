using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Components.TextEditor;
using System.Windows.Interop;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Лаунчер для запуска встроенного текстового редактора
    /// Заменяет DesktopApplicationLauncher для текстовых файлов и предоставляет полный контроль жизненного цикла
    /// </summary>
    public class TextEditorApplicationLauncher : IApplicationLauncher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TextEditorApplicationLauncher> _logger;
        private readonly Dictionary<string, TextEditorWindow> _activeWindows;

        // События для интеграции с ApplicationLifecycleService
        public event EventHandler<ApplicationInstance>? WindowActivated;
        public event EventHandler<ApplicationInstance>? WindowDeactivated;
        public event EventHandler<ApplicationInstance>? WindowClosed;
        public event EventHandler<ApplicationInstance>? WindowStateChanged;

        public TextEditorApplicationLauncher(
            IServiceProvider serviceProvider,
            ILogger<TextEditorApplicationLauncher> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeWindows = new Dictionary<string, TextEditorWindow>();
        }

        #region IApplicationLauncher Properties

        /// <summary>
        /// Тип приложений, которые поддерживает данный лаунчер
        /// </summary>
        public ApplicationType SupportedType => ApplicationType.Desktop;

        /// <summary>
        /// Приоритет лаунчера (высший приоритет для текстовых редакторов)
        /// </summary>
        public int Priority => 30; // Высший приоритет - заменяет DesktopApplicationLauncher для текстовых файлов

        #endregion

        /// <summary>
        /// Проверяет, может ли этот лаунчер запустить указанное приложение
        /// </summary>
        public bool CanLaunch(CoreApplication application)
        {
            if (application == null)
                return false;

            // Поддерживаем только Desktop приложения
            if (application.Type != ApplicationType.Desktop)
                return false;

            // Проверяем по пути к исполняемому файлу (основной критерий)
            var executablePath = application.ExecutablePath?.ToLowerInvariant() ?? "";
            
            // Список поддерживаемых текстовых редакторов по пути
            bool isTextEditor = 
                executablePath.Contains("notepad.exe") ||           // Стандартный блокнот Windows
                executablePath.Contains("notepad++.exe") ||         // Notepad++
                executablePath.Contains("wordpad.exe") ||           // WordPad
                executablePath.Contains("sublimetext.exe") ||       // Sublime Text
                executablePath.Contains("code.exe") ||              // VS Code
                executablePath.Contains("atom.exe") ||              // Atom
                executablePath.Contains("texteditor.exe") ||        // Общие названия
                executablePath.Contains("editor.exe") ||
                executablePath.Contains("edit.exe");

            // Дополнительная проверка по названию приложения (fallback)
            if (!isTextEditor)
            {
                var appName = application.Name?.ToLowerInvariant() ?? "";
                isTextEditor = 
                    appName.Contains("notepad") ||
                    appName.Contains("блокнот") ||
                    appName.Contains("текстовый редактор") ||
                    appName.Contains("text editor") ||
                    (appName.Contains("редактор") && !appName.Contains("изображений") && !appName.Contains("видео"));
            }

            if (isTextEditor)
            {
                _logger.LogTrace("TextEditorApplicationLauncher can launch {AppName} (Path: {Path})", 
                    application.Name, application.ExecutablePath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Запускает приложение через встроенный текстовый редактор
        /// </summary>
        public async Task<LaunchResult> LaunchAsync(CoreApplication application, string launchedBy)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));
            
            if (string.IsNullOrWhiteSpace(launchedBy))
                throw new ArgumentException("LaunchedBy cannot be null or empty", nameof(launchedBy));

            if (!CanLaunch(application))
            {
                var error = $"TextEditorApplicationLauncher cannot launch application {application.Name} (Type: {application.Type})";
                _logger.LogWarning(error);
                return LaunchResult.Failure(error);
            }

            var startTime = DateTime.Now;
            var instanceId = GenerateInstanceId(application);

            try
            {
                _logger.LogInformation("Launching TextEditor application {AppName} (Instance: {InstanceId}) by user {LaunchedBy}", 
                    application.Name, instanceId, launchedBy);

                // Извлекаем путь к файлу из аргументов приложения
                var filePath = ExtractFilePathFromArguments(application);

                // Создаем окно текстового редактора
                var window = await CreateTextEditorWindowAsync(application, filePath, launchedBy, instanceId);
                
                if (window == null)
                {
                    var error = $"Failed to create TextEditor window for {application.Name}";
                    _logger.LogError(error);
                    return LaunchResult.Failure(error);
                }

                // Регистрируем окно
                _activeWindows[instanceId] = window;
                
                // Подписываемся на события окна
                SubscribeToWindowEvents(window);

                // Показываем окно
                window.Show();
                window.Activate();

                var launchDuration = DateTime.Now - startTime;
                
                // Создаем ApplicationInstance для возврата результата
                var instance = new ApplicationInstance
                {
                    InstanceId = instanceId,
                    Application = application,
                    ProcessId = Environment.ProcessId, // TextEditor работает в текущем процессе
                    StartTime = startTime,
                    State = ApplicationState.Running,
                    LaunchedBy = launchedBy,
                    IsVirtual = false,
                    IsActive = true,
                    LastUpdate = DateTime.Now
                };
                
                // НЕ вызываем WindowActivated здесь - оно автоматически сработает в OnWindowActivated при window.Activate()
                
                _logger.LogInformation("Successfully launched TextEditor application {AppName} (Instance: {InstanceId}) in {Duration}ms", 
                    application.Name, instanceId, launchDuration.TotalMilliseconds);

                return LaunchResult.Success(instance, launchDuration);
            }
            catch (Exception ex)
            {
                var error = $"Error launching TextEditor application {application.Name}: {ex.Message}";
                _logger.LogError(ex, "Failed to launch TextEditor application {AppName} (Instance: {InstanceId})", 
                    application.Name, instanceId);

                // Очищаем регистрацию при ошибке
                _activeWindows.Remove(instanceId);

                return LaunchResult.Failure(error);
            }
        }

        /// <summary>
        /// Переключиться на указанный экземпляр приложения
        /// </summary>
        public async Task<bool> SwitchToAsync(string instanceId)
        {
            try
            {
                if (_activeWindows.TryGetValue(instanceId, out var window))
                {
                    if (!window.IsClosed)
                    {
                        // Восстанавливаем окно если свернуто
                        if (window.WindowState == System.Windows.WindowState.Minimized)
                        {
                            window.WindowState = System.Windows.WindowState.Normal;
                        }
                        
                        // Активируем и выводим на передний план
                        window.Activate();
                        window.Topmost = true;
                        window.Topmost = false; // Снимаем Topmost после активации
                        window.Focus();

                        _logger.LogDebug("Successfully switched to TextEditor instance {InstanceId}", instanceId);
                        return true;
                    }
                    else
                    {
                        // Окно закрыто, удаляем из коллекции
                        _activeWindows.Remove(instanceId);
                        _logger.LogWarning("TextEditor window {InstanceId} was closed, removed from active windows", instanceId);
                    }
                }

                _logger.LogWarning("TextEditor window not found for instance {InstanceId}", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to TextEditor instance {InstanceId}", instanceId);
                return false;
            }
        }

        /// <summary>
        /// Закрыть указанный экземпляр приложения
        /// </summary>
        public async Task<bool> TerminateAsync(string instanceId, bool force = false)
        {
            try
            {
                if (_activeWindows.TryGetValue(instanceId, out var window))
                {
                    _logger.LogInformation("Terminating TextEditor application instance {InstanceId} (Force: {Force})", 
                        instanceId, force);

                    // Закрываем окно
                    if (force)
                    {
                        // Принудительное закрытие без сохранения
                        window.ForceClose();
                    }
                    else
                    {
                        // Обычное закрытие с проверкой сохранения
                        window.Close();
                    }

                    // Удаляем из коллекции
                    _activeWindows.Remove(instanceId);

                    return true;
                }

                _logger.LogWarning("TextEditor window not found for termination: {InstanceId}", instanceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating TextEditor instance {InstanceId}", instanceId);
                return false;
            }
        }

        /// <summary>
        /// Получить все активные экземпляры
        /// </summary>
        public async Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync()
        {
            await Task.CompletedTask;

            var instances = new List<ApplicationInstance>();

            foreach (var kvp in _activeWindows.ToList())
            {
                try
                {
                    var window = kvp.Value;
                    if (window.IsClosed)
                    {
                        // Удаляем закрытые окна из коллекции
                        _activeWindows.Remove(kvp.Key);
                        continue;
                    }

                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId, // TextEditor работает в текущем процессе
                        StartTime = DateTime.Now, // TODO: Добавить StartTime в TextEditorWindow
                        State = ApplicationState.Running,
                        LaunchedBy = "system", // TODO: Сохранить LaunchedBy в TextEditorWindow
                        IsVirtual = false,
                        IsActive = !window.IsClosed,
                        LastUpdate = DateTime.Now
                    };

                    instances.Add(instance);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting instance info for {InstanceId}", kvp.Key);
                }
            }

            return instances;
        }

        /// <summary>
        /// Найти главное окно для запущенного процесса приложения
        /// Для TextEditor это не применимо, так как мы управляем WPF окнами напрямую
        /// </summary>
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, CoreApplication application)
        {
            try
            {
                // Для TextEditor приложений мы управляем окнами напрямую
                // Ищем среди активных окон
                foreach (var kvp in _activeWindows)
                {
                    var window = kvp.Value;
                    if (window.Application.Id == application.Id && !window.IsClosed)
                    {
                        // Создаем WindowInfo из WPF окна
                        var windowHandle = window.GetWindowHandle();
                        var windowInfo = new WindowInfo
                        {
                            Handle = windowHandle,
                            ProcessId = (uint)Environment.ProcessId, // TextEditor работает в текущем процессе
                            Title = window.Title,
                            IsVisible = window.IsVisible,
                            ClassName = "TextEditorApplicationWindow"
                        };
                    
                        _logger.LogDebug("Found TextEditor main window for {AppName}: '{Title}'", 
                            application.Name, windowInfo.Title);
                        return windowInfo;
                    }
                }

                _logger.LogDebug("No TextEditor window found for {AppName} with process ID {ProcessId}", 
                    application.Name, processId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for TextEditor app {AppName} (PID: {ProcessId})", 
                    application.Name, processId);
                return null;
            }
        }

        /// <summary>
        /// Найти существующий экземпляр приложения
        /// </summary>
        public async Task<ApplicationInstance?> FindExistingInstanceAsync(CoreApplication application)
        {
            try
            {
                if (!CanLaunch(application))
                    return null;

                // Ищем среди активных окон
                foreach (var kvp in _activeWindows)
                {
                    var window = kvp.Value;
                    if (window.Application.Id == application.Id && !window.IsClosed)
                    {
                        // Создаем ApplicationInstance из активного окна
                        var instance = new ApplicationInstance
                        {
                            InstanceId = window.InstanceId,
                            Application = application,
                            ProcessId = Environment.ProcessId, // TextEditor работает в текущем процессе
                            StartTime = DateTime.Now, // TODO: Добавить StartTime в TextEditorWindow
                            State = ApplicationState.Running,
                            LaunchedBy = "system", // TODO: Сохранить LaunchedBy в TextEditorWindow
                            IsVirtual = false
                        };

                        _logger.LogDebug("Found existing TextEditor instance for {AppName} (Instance: {InstanceId})", 
                            application.Name, instance.InstanceId);
                        return instance;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing TextEditor instance for {AppName}", application.Name);
                return null;
            }
        }

        /// <summary>
        /// Получить время ожидания инициализации окна для данного типа приложений
        /// </summary>
        public int GetWindowInitializationTimeoutMs(CoreApplication application)
        {
            // TextEditor приложения инициализируются быстро
            return 5000; // 5 секунд
        }

        /// <summary>
        /// Выполнить специфичную для TextEditor очистку ресурсов
        /// </summary>
        public async Task CleanupAsync(ApplicationInstance instance)
        {
            try
            {
                if (instance == null)
                    return;

                _logger.LogDebug("Cleaning up TextEditor application {AppName} (Instance: {InstanceId})", 
                    instance.Application.Name, instance.InstanceId);

                // Ищем и закрываем соответствующее окно
                if (_activeWindows.TryGetValue(instance.InstanceId, out var window))
                {
                    UnsubscribeFromWindowEvents(window);
                    window.ForceClose(); // Закрываем без запроса сохранения
                    _activeWindows.Remove(instance.InstanceId);
                    
                    _logger.LogDebug("TextEditor window closed and cleaned up for {AppName}", 
                        instance.Application.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of TextEditor application {AppName}", 
                    instance?.Application.Name);
            }
        }

        #region Вспомогательные методы

        /// <summary>
        /// Создает окно текстового редактора для приложения
        /// </summary>
        private async Task<TextEditorWindow?> CreateTextEditorWindowAsync(
            CoreApplication application, string filePath, string launchedBy, string instanceId = null)
        {
            try
            {
                // Создаем окно редактора с переданным instanceId
                var window = new TextEditorWindow(application, filePath, launchedBy, instanceId);

                _logger.LogDebug("Created TextEditor window for {AppName} (File: {FilePath})", 
                    application.Name, filePath ?? "новый документ");

                return window;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating TextEditor window for {AppName}", application.Name);
                return null;
            }
        }

        /// <summary>
        /// Подписывается на события окна для интеграции с жизненным циклом
        /// </summary>
        private void SubscribeToWindowEvents(TextEditorWindow window)
        {
            window.Closed += OnWindowClosed;
            window.Activated += OnWindowActivated;
            window.Deactivated += OnWindowDeactivated;
            window.StateChanged += OnWindowStateChanged;
        }

        /// <summary>
        /// Отписывается от событий окна
        /// </summary>
        private void UnsubscribeFromWindowEvents(TextEditorWindow window)
        {
            window.Closed -= OnWindowClosed;
            window.Activated -= OnWindowActivated;
            window.Deactivated -= OnWindowDeactivated;
            window.StateChanged -= OnWindowStateChanged;
        }

        /// <summary>
        /// Обработчик закрытия окна
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            try
            {
                if (sender is TextEditorWindow window)
                {
                    _logger.LogDebug("TextEditor window closed for {AppName} (Instance: {InstanceId})", 
                        window.Application?.Name, window.InstanceId);

                    // Удаляем из коллекции активных окон
                    if (_activeWindows.TryGetValue(window.InstanceId, out var activeWindow))
                    {
                        UnsubscribeFromWindowEvents(activeWindow);
                        _activeWindows.Remove(window.InstanceId);
                    }

                    // Создаем ApplicationInstance для уведомления
                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId,
                        State = ApplicationState.Terminated,
                        LastUpdate = DateTime.Now
                    };

                    // Уведомляем ApplicationLifecycleService о закрытии через событие
                    WindowClosed?.Invoke(this, instance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window closed event");
            }
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            try
            {
                if (sender is TextEditorWindow window)
                {
                    _logger.LogTrace("TextEditor window activated for {AppName} (Instance: {InstanceId})", 
                        window.Application?.Name, window.InstanceId);
                    
                    // Создаем ApplicationInstance для уведомления
                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId,
                        State = ApplicationState.Running,
                        IsActive = true,
                        LastUpdate = DateTime.Now
                    };
                    
                    // Уведомляем ApplicationLifecycleService об активации
                    WindowActivated?.Invoke(this, instance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window activated event");
            }
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            try
            {
                if (sender is TextEditorWindow window)
                {
                    _logger.LogTrace("TextEditor window deactivated for {AppName} (Instance: {InstanceId})", 
                        window.Application?.Name, window.InstanceId);
                    
                    // Создаем ApplicationInstance для уведомления
                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId,
                        State = ApplicationState.Running,
                        IsActive = false,
                        LastUpdate = DateTime.Now
                    };
                    
                    // Уведомляем ApplicationLifecycleService о деактивации
                    WindowDeactivated?.Invoke(this, instance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window deactivated event");
            }
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            try
            {
                if (sender is TextEditorWindow window)
                {
                    _logger.LogTrace("TextEditor window state changed for {AppName} (Instance: {InstanceId})", 
                        window.Application?.Name, window.InstanceId);
                    
                    // Создаем ApplicationInstance для уведомления
                    var instance = new ApplicationInstance
                    {
                        InstanceId = window.InstanceId,
                        Application = window.Application,
                        ProcessId = Environment.ProcessId,
                        State = ApplicationState.Running,
                        LastUpdate = DateTime.Now
                    };
                    
                    // Уведомляем ApplicationLifecycleService об изменении состояния
                    WindowStateChanged?.Invoke(this, instance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window state changed event");
            }
        }

        /// <summary>
        /// Извлекает путь к файлу из аргументов приложения
        /// </summary>
        private string ExtractFilePathFromArguments(CoreApplication application)
        {
            try
            {
                var arguments = application.Arguments ?? "";
                
                // Парсим аргументы с помощью TextEditorArguments
                var parsedArgs = TextEditorArguments.Parse(arguments);
                
                if (!string.IsNullOrEmpty(parsedArgs.InitialFilePath))
                {
                    _logger.LogDebug("Extracted file path from arguments: {FilePath}", parsedArgs.InitialFilePath);
                    return parsedArgs.InitialFilePath;
                }
                
                // Если нет файла в аргументах, возвращаем пустую строку (новый документ)
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting file path from arguments: {Arguments}", application.Arguments);
                return string.Empty;
            }
        }

        /// <summary>
        /// Генерирует уникальный ID экземпляра
        /// </summary>
        private string GenerateInstanceId(CoreApplication application)
        {
            return $"texteditor_{application.Id}_{Guid.NewGuid():N}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _logger.LogDebug("Disposing TextEditorApplicationLauncher with {Count} active windows", 
                    _activeWindows.Count);

                // Закрываем все активные окна
                foreach (var kvp in _activeWindows.ToList())
                {
                    try
                    {
                        var window = kvp.Value;
                        UnsubscribeFromWindowEvents(window);
                        window.ForceClose(); // Принудительное закрытие без сохранения при завершении
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing TextEditor window {InstanceId} during disposal", kvp.Key);
                    }
                }

                _activeWindows.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing TextEditorApplicationLauncher");
            }
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Services.Lifecycle.Windows
{
    /// <summary>
    /// Менеджер окон для управления окнами приложений через Windows API
    /// Обеспечивает безопасную работу с нативными функциями Windows
    /// </summary>
    public class WindowManager : IWindowManager
    {
        private readonly ILogger<WindowManager> _logger;
        
        // НОВОЕ: Кэширование WSA окон для оптимизации производительности
        private readonly Dictionary<string, (WindowInfo Window, DateTime CachedAt)> _wsaWindowCache 
            = new Dictionary<string, (WindowInfo, DateTime)>();
        private readonly TimeSpan _wsaCacheTimeout = TimeSpan.FromSeconds(60); // TTL 60 секунд
        private readonly object _cacheLocker = new object();
        
        #region Windows API Declarations
        
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        
        [DllImport("user32.dll")]
        private static extern uint GetCurrentThreadId();
        
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);
        
        // Process-related APIs
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);
        
        
        [DllImport("psapi.dll", CharSet = CharSet.Auto)]
        private static extern uint GetProcessImageFileName(IntPtr hProcess, StringBuilder lpImageFileName, uint nSize);

        // Новые P/Invoke декларации для WSA window detection
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        // Constants
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_MAXIMIZE = 3;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINNOACTIVE = 7;
        private const int SW_SHOWNA = 8;
        private const int SW_RESTORE = 9;
        
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_RESTORE = 0xF120;
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        
        // Process access rights
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        
        private const byte VK_MENU = 0x12; // Alt key
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        #endregion
        
        public WindowManager(ILogger<WindowManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region Поиск окон
        
        public async Task<WindowInfo?> FindMainWindowAsync(int processId, string? expectedTitle = null, string? expectedClassName = null)
        {
            await Task.CompletedTask;
            
            try
            {
                _logger.LogDebug("Finding main window for process {ProcessId}, expected title: '{ExpectedTitle}', class: '{ExpectedClassName}'",
                    processId, expectedTitle, expectedClassName);
                
                var windows = GetAllWindowsForProcess(processId);
                
                if (windows.Length == 0)
                {
                    _logger.LogDebug("No windows found for process {ProcessId}", processId);
                    return null;
                }
                
                // Сначала ищем точное совпадение по заголовку
                if (!string.IsNullOrEmpty(expectedTitle))
                {
                    var exactMatch = windows.FirstOrDefault(w => 
                        string.Equals(w.Title, expectedTitle, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        _logger.LogDebug("Found exact title match: '{Title}'", exactMatch.Title);
                        return exactMatch;
                    }
                    
                    // Затем частичное совпадение
                    var partialMatch = windows.FirstOrDefault(w => 
                        w.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase));
                    if (partialMatch != null)
                    {
                        _logger.LogDebug("Found partial title match: '{Title}' contains '{Expected}'", 
                            partialMatch.Title, expectedTitle);
                        return partialMatch;
                    }
                }
                
                // Ищем по классу окна
                if (!string.IsNullOrEmpty(expectedClassName))
                {
                    var classMatch = windows.FirstOrDefault(w => 
                        string.Equals(w.ClassName, expectedClassName, StringComparison.OrdinalIgnoreCase));
                    if (classMatch != null)
                    {
                        _logger.LogDebug("Found class match: '{ClassName}'", classMatch.ClassName);
                        return classMatch;
                    }
                }
                
                // Приоритет: видимые окна с заголовком
                var visibleWithTitle = windows
                    .Where(w => w.IsVisible && !string.IsNullOrEmpty(w.Title))
                    .OrderByDescending(w => w.Title.Length) // Предпочитаем окна с более длинными заголовками
                    .FirstOrDefault();
                
                if (visibleWithTitle != null)
                {
                    _logger.LogDebug("Found visible window with title: '{Title}'", visibleWithTitle.Title);
                    return visibleWithTitle;
                }
                
                // Любое видимое окно
                var visibleWindow = windows.FirstOrDefault(w => w.IsVisible);
                if (visibleWindow != null)
                {
                    _logger.LogDebug("Found visible window: '{Title}'", visibleWindow.Title);
                    return visibleWindow;
                }
                
                // В крайнем случае, первое найденное окно
                var firstWindow = windows.First();
                _logger.LogDebug("Using first available window: '{Title}'", firstWindow.Title);
                return firstWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding main window for process {ProcessId}", processId);
                return null;
            }
        }
        
        public async Task<WindowInfo[]> GetAllWindowsForProcessAsync(int processId)
        {
            await Task.CompletedTask;
            return GetAllWindowsForProcess(processId);
        }
        
        private WindowInfo[] GetAllWindowsForProcess(int processId)
        {
            var windows = new List<WindowInfo>();
            var enumCount = 0;
            var maxEnumCount = 1000; // Защита от зависания
            
            try
            {
                var startTime = DateTime.Now;
                
                EnumWindows((hWnd, lParam) =>
                {
                    enumCount++;
                    
                    // Защита от зависания
                    if (enumCount > maxEnumCount || (DateTime.Now - startTime).TotalSeconds > 5)
                    {
                        _logger.LogWarning("EnumWindows stopped due to safety limits for process {ProcessId}", processId);
                        return false;
                    }
                    
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                        
                        if (windowProcessId == processId)
                        {
                            var windowInfo = CreateWindowInfoFromHandle(hWnd, windowProcessId);
                            if (windowInfo != null)
                            {
                                windows.Add(windowInfo);
                                
                                // Дополнительное логирование для отладки
                                _logger.LogDebug("Found window for process {ProcessId}: Handle={Handle:X}, Title='{Title}', Class='{Class}', Visible={Visible}", 
                                    processId, (long)hWnd, windowInfo.Title, windowInfo.ClassName, windowInfo.IsVisible);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error processing window handle {Handle:X}", (long)hWnd);
                    }
                    
                    return true;
                }, IntPtr.Zero);
                
                _logger.LogDebug("Found {Count} windows for process {ProcessId} (enumerated {EnumCount} total windows)", 
                    windows.Count, processId, enumCount);
                
                // Если ничего не найдено, попробуем дополнительные способы поиска
                if (windows.Count == 0)
                {
                    _logger.LogDebug("No windows found via EnumWindows for process {ProcessId}, trying alternative methods", processId);
                    
                    // Дополнительный поиск по всем окнам с проверкой PID
                    var alternativeWindows = FindWindowsByProcessIdAlternative(processId);
                    windows.AddRange(alternativeWindows);
                    
                    _logger.LogDebug("Alternative search found {Count} additional windows for process {ProcessId}", 
                        alternativeWindows.Length, processId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating windows for process {ProcessId}", processId);
            }
            
            return windows.ToArray();
        }
        
        private WindowInfo? CreateWindowInfoFromHandle(IntPtr hWnd, uint processId)
        {
            try
            {
                var title = GetWindowTitle(hWnd);
                var className = GetWindowClass(hWnd);
                var isVisible = IsWindowVisible(hWnd);
                var isMinimized = IsIconic(hWnd);
                var isMaximized = IsZoomed(hWnd);
                var isEnabled = IsWindowEnabled(hWnd);
                
                var windowInfo = WindowInfo.CreateDetailed(
                    hWnd, title, className, processId, 0, isVisible);
                
                windowInfo.IsMinimized = isMinimized;
                windowInfo.IsMaximized = isMaximized;
                windowInfo.IsEnabled = isEnabled;
                windowInfo.IsActive = GetForegroundWindow() == hWnd;
                
                // Получаем геометрию окна
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    windowInfo.UpdateGeometry(rect.Left, rect.Top, 
                        rect.Right - rect.Left, rect.Bottom - rect.Top);
                }
                
                return windowInfo;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error creating WindowInfo for handle {Handle:X}", (long)hWnd);
                return null;
            }
        }
        
        public async Task<WindowInfo?> FindWindowByTitleAsync(string windowTitle, bool exactMatch = false)
        {
            await Task.CompletedTask;
            
            WindowInfo? foundWindow = null;
            var enumCount = 0;
            var maxEnumCount = 2000;
            
            try
            {
                var startTime = DateTime.Now;
                
                EnumWindows((hWnd, lParam) =>
                {
                    enumCount++;
                    
                    if (enumCount > maxEnumCount || (DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        _logger.LogWarning("EnumWindows stopped due to safety limits while searching for title '{Title}'", windowTitle);
                        return false;
                    }
                    
                    try
                    {
                        var title = GetWindowTitle(hWnd);
                        bool titleMatches = exactMatch 
                            ? string.Equals(title, windowTitle, StringComparison.OrdinalIgnoreCase)
                            : title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase);
                        
                        if (titleMatches && IsWindowVisible(hWnd))
                        {
                            GetWindowThreadProcessId(hWnd, out uint processId);
                            foundWindow = CreateWindowInfoFromHandle(hWnd, processId);
                            return false; // Останавливаем поиск
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error processing window handle {Handle:X} during title search", (long)hWnd);
                    }
                    
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for window by title '{Title}'", windowTitle);
            }
            
            return foundWindow;
        }
        
        public async Task<IReadOnlyList<WindowInfo>> FindWindowsByClassAsync(string className)
        {
            await Task.CompletedTask;
            
            var windows = new List<WindowInfo>();
            var enumCount = 0;
            var maxEnumCount = 2000;
            
            try
            {
                var startTime = DateTime.Now;
                
                EnumWindows((hWnd, lParam) =>
                {
                    enumCount++;
                    
                    if (enumCount > maxEnumCount || (DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        _logger.LogWarning("EnumWindows stopped due to safety limits while searching for class '{ClassName}'", className);
                        return false;
                    }
                    
                    try
                    {
                        var windowClass = GetWindowClass(hWnd);
                        if (string.Equals(windowClass, className, StringComparison.OrdinalIgnoreCase))
                        {
                            GetWindowThreadProcessId(hWnd, out uint processId);
                            var windowInfo = CreateWindowInfoFromHandle(hWnd, processId);
                            if (windowInfo != null)
                            {
                                windows.Add(windowInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error processing window handle {Handle:X} during class search", (long)hWnd);
                    }
                    
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for windows by class '{ClassName}'", className);
            }
            
            return windows.AsReadOnly();
        }
        
        #endregion
        
        #region Управление состоянием окон
        
        public async Task<bool> SwitchToWindowAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogWarning("Invalid window handle {Handle:X}", (long)windowHandle);
                    return false;
                }
                
                _logger.LogDebug("Switching to window {Handle:X}", (long)windowHandle);
                
                // Используем расширенную логику переключения
                return await ForceToForegroundAsync(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to window {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> MinimizeWindowAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogWarning("Invalid window handle {Handle:X}", (long)windowHandle);
                    return false;
                }
                
                bool result = ShowWindow(windowHandle, SW_MINIMIZE);
                _logger.LogDebug("Minimized window {Handle:X}: {Result}", (long)windowHandle, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error minimizing window {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> RestoreWindowAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogWarning("Invalid window handle {Handle:X}", (long)windowHandle);
                    return false;
                }
                
                bool result = ShowWindow(windowHandle, SW_RESTORE);
                _logger.LogDebug("Restored window {Handle:X}: {Result}", (long)windowHandle, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> MaximizeWindowAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogWarning("Invalid window handle {Handle:X}", (long)windowHandle);
                    return false;
                }
                
                bool result = ShowWindow(windowHandle, SW_MAXIMIZE);
                _logger.LogDebug("Maximized window {Handle:X}: {Result}", (long)windowHandle, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error maximizing window {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> CloseWindowAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogWarning("Invalid window handle {Handle:X}", (long)windowHandle);
                    return false;
                }
                
                bool result = PostMessage(windowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                _logger.LogDebug("Sent close message to window {Handle:X}: {Result}", (long)windowHandle, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing window {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        #endregion
        
        #region Получение информации об окне
        
        public async Task<WindowInfo?> RefreshWindowInfoAsync(WindowInfo windowInfo)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowInfo.Handle))
                {
                    _logger.LogDebug("Window handle {Handle:X} is no longer valid", (long)windowInfo.Handle);
                    return null;
                }
                
                var refreshed = CreateWindowInfoFromHandle(windowInfo.Handle, windowInfo.ProcessId);
                if (refreshed != null)
                {
                    // Сохраняем некоторые исходные данные
                    refreshed.CreatedAt = windowInfo.CreatedAt;
                    refreshed.AdditionalInfo = windowInfo.AdditionalInfo;
                }
                
                return refreshed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing window info for handle {Handle:X}", (long)windowInfo.Handle);
                return null;
            }
        }
        
        public async Task<bool> IsWindowValidAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                return IsWindow(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking window validity {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> IsWindowVisibleAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                return IsWindow(windowHandle) && IsWindowVisible(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking window visibility {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> IsWindowMinimizedAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                bool isValid = IsWindow(windowHandle);
                if (!isValid)
                {
                    _logger.LogDebug("Window {Handle:X} is not valid when checking minimized state", (long)windowHandle);
                    return false;
                }
                
                bool isMinimized = IsIconic(windowHandle);
                _logger.LogTrace("Window {Handle:X} minimized state: {IsMinimized}", (long)windowHandle, isMinimized);
                return isMinimized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if window is minimized {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<bool> IsWindowActiveAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                return IsWindow(windowHandle) && GetForegroundWindow() == windowHandle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if window is active {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<string> GetWindowTitleAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                return GetWindowTitle(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting window title {Handle:X}", (long)windowHandle);
                return string.Empty;
            }
        }
        
        public async Task<string> GetWindowClassAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                return GetWindowClass(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting window class {Handle:X}", (long)windowHandle);
                return string.Empty;
            }
        }
        
        #endregion
        
        #region Специальные операции
        
        public async Task<bool> ForceToForegroundAsync(IntPtr windowHandle)
        {
            await Task.CompletedTask;
            
            try
            {
                if (!IsWindow(windowHandle))
                {
                    return false;
                }
                
                _logger.LogDebug("Attempting to force window {Handle:X} to foreground", (long)windowHandle);
                
                // Проверяем текущее состояние окна ДО переключения
                bool isMinimizedBeforeSwitch = IsIconic(windowHandle);
                bool isVisibleBeforeSwitch = IsWindowVisible(windowHandle);
                _logger.LogInformation("Window {Handle:X} state before switch - IsMinimized: {IsMinimized}, IsVisible: {IsVisible}", 
                    (long)windowHandle, isMinimizedBeforeSwitch, isVisibleBeforeSwitch);
                
                // ВАЖНО: Если окно свернуто, СНАЧАЛА восстанавливаем его
                if (isMinimizedBeforeSwitch)
                {
                    _logger.LogInformation("Window {Handle:X} is minimized, restoring before SetForegroundWindow", (long)windowHandle);
                    ShowWindow(windowHandle, SW_RESTORE);
                    await Task.Delay(150); // Даем время на завершение анимации восстановления
                }
                
                // Метод 1: Стандартный SetForegroundWindow
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogInformation("Window {Handle:X} brought to foreground via SetForegroundWindow (was minimized: {WasMinimized})", 
                        (long)windowHandle, isMinimizedBeforeSwitch);
                    return true;
                }
                
                // Метод 2: Восстановить если свернуто, затем SetForegroundWindow
                if (IsIconic(windowHandle))
                {
                    _logger.LogInformation("Window {Handle:X} is minimized, attempting to restore", (long)windowHandle);
                    ShowWindow(windowHandle, SW_RESTORE);
                    await Task.Delay(100); // Небольшая задержка для завершения анимации
                    
                    if (SetForegroundWindow(windowHandle))
                    {
                        _logger.LogInformation("Successfully restored and brought minimized window {Handle:X} to foreground", (long)windowHandle);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to bring restored window {Handle:X} to foreground", (long)windowHandle);
                    }
                }
                
                // Метод 3: Симуляция Alt-клавиши для обхода ограничений SetForegroundWindow
                _logger.LogDebug("Using Alt key simulation technique");
                keybd_event(VK_MENU, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(VK_MENU, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                ShowWindow(windowHandle, SW_RESTORE);
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogDebug("Window brought to foreground via Alt simulation");
                    return true;
                }
                
                // Метод 4: AttachThreadInput для связывания потоков
                _logger.LogDebug("Using AttachThreadInput technique");
                uint currentThreadId = GetCurrentThreadId();
                uint windowThreadId = GetWindowThreadProcessId(windowHandle, out _);
                
                if (windowThreadId != 0 && windowThreadId != currentThreadId)
                {
                    if (AttachThreadInput(currentThreadId, windowThreadId, true))
                    {
                        ShowWindow(windowHandle, SW_RESTORE);
                        SetForegroundWindow(windowHandle);
                        BringWindowToTop(windowHandle);
                        AttachThreadInput(currentThreadId, windowThreadId, false);
                        
                        _logger.LogDebug("Window brought to foreground via AttachThreadInput");
                        return true;
                    }
                }
                
                // Метод 5: Через SetWindowPos с TOPMOST
                SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                
                if (SetForegroundWindow(windowHandle))
                {
                    _logger.LogDebug("Window brought to foreground via SetWindowPos + SetForegroundWindow");
                    return true;
                }
                
                // Метод 6: Через SendMessage с WM_SYSCOMMAND
                SendMessage(windowHandle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                ShowWindow(windowHandle, SW_RESTORE);
                
                bool finalResult = SetForegroundWindow(windowHandle);
                _logger.LogDebug("Final attempt result: {Result}", finalResult);
                
                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing window to foreground {Handle:X}", (long)windowHandle);
                return false;
            }
        }
        
        public async Task<WindowInfo?> EnumerateAndFindWindowAsync(int processId, string? titleFilter = null)
        {
            await Task.CompletedTask;
            
            try
            {
                _logger.LogDebug("Enumerating all windows to find process {ProcessId} with title filter '{TitleFilter}'", 
                    processId, titleFilter);
                
                var windows = GetAllWindowsForProcess(processId);
                
                if (!string.IsNullOrEmpty(titleFilter))
                {
                    var filtered = windows.Where(w => 
                        w.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
                    
                    if (filtered.Length > 0)
                    {
                        _logger.LogDebug("Found {Count} windows matching title filter", filtered.Length);
                        return filtered.OrderByDescending(w => w.IsVisible)
                                      .ThenByDescending(w => w.Title.Length)
                                      .First();
                    }
                }
                
                // Возвращаем лучшее найденное окно
                return windows.OrderByDescending(w => w.IsVisible)
                             .ThenByDescending(w => !string.IsNullOrEmpty(w.Title))
                             .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating windows for process {ProcessId}", processId);
                return null;
            }
        }
        
        #endregion
        
        #region Вспомогательные методы
        
        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                int titleLength = GetWindowTextLength(hWnd);
                if (titleLength <= 0 || titleLength > 1000) // Ограничение на разумную длину
                    return string.Empty;
                
                var title = new StringBuilder(titleLength + 1);
                GetWindowText(hWnd, title, title.Capacity);
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private string GetWindowClass(IntPtr hWnd)
        {
            try
            {
                var className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                return className.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private WindowInfo[] FindWindowsByProcessIdAlternative(int processId)
        {
            var windows = new List<WindowInfo>();
            
            try
            {
                // Альтернативный подход: поиск через известные классы окон для Notepad
                var notepadClasses = new[] { "Notepad", "Edit", "RICHEDIT50W", "Static" };
                
                foreach (var className in notepadClasses)
                {
                    var classWindows = FindWindowsByClassAsync(className).GetAwaiter().GetResult();
                    
                    foreach (var window in classWindows)
                    {
                        // Проверяем что окно принадлежит нашему процессу
                        GetWindowThreadProcessId(window.Handle, out uint windowProcessId);
                        
                        if (windowProcessId == processId)
                        {
                            _logger.LogDebug("Alternative method found window: PID={ProcessId}, Class='{Class}', Title='{Title}'", 
                                processId, className, window.Title);
                            windows.Add(window);
                        }
                    }
                }
                
                // Если все еще ничего не найдено, ищем любые видимые окна для процесса
                if (windows.Count == 0)
                {
                    _logger.LogDebug("Class-based search failed, trying brute force enumeration for process {ProcessId}", processId);
                    
                    // Брутфорс поиск с более детальным логированием
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                            
                            if (windowProcessId == processId)
                            {
                                var title = GetWindowTitle(hWnd);
                                var className = GetWindowClass(hWnd);
                                var isVisible = IsWindowVisible(hWnd);
                                
                                _logger.LogDebug("Brute force found potential window: PID={ProcessId}, Handle={Handle:X}, Title='{Title}', Class='{Class}', Visible={Visible}", 
                                    processId, (long)hWnd, title, className, isVisible);
                                
                                var windowInfo = CreateWindowInfoFromHandle(hWnd, windowProcessId);
                                if (windowInfo != null)
                                {
                                    windows.Add(windowInfo);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogTrace(ex, "Error in brute force window search for handle {Handle:X}", (long)hWnd);
                        }
                        
                        return true;
                    }, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alternative window search for process {ProcessId}", processId);
            }
            
            return windows.ToArray();
        }
        
        #endregion
        
        #region Дополнительные методы для совместимости с тестами
        
        /// <summary>
        /// Получить окна по ID процесса (алиас для GetAllWindowsForProcessAsync)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Список окон процесса</returns>
        public async Task<IReadOnlyList<WindowInfo>> GetWindowsByProcessIdAsync(int processId)
        {
            var windows = await GetAllWindowsForProcessAsync(processId);
            return windows.ToList().AsReadOnly();
        }
        
        /// <summary>
        /// Вынести окно на передний план (алиас для ForceToForegroundAsync)
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>true если операция успешна</returns>
        public async Task<bool> BringWindowToFrontAsync(IntPtr windowHandle)
        {
            return await ForceToForegroundAsync(windowHandle);
        }
        
        /// <summary>
        /// Установить состояние окна
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <param name="windowState">Желаемое состояние окна</param>
        /// <returns>true если операция успешна</returns>
        public async Task<bool> SetWindowStateAsync(IntPtr windowHandle, ApplicationWindowState windowState)
        {
            await Task.CompletedTask;
            
            try
            {
                return windowState switch
                {
                    ApplicationWindowState.Normal => ShowWindow(windowHandle, SW_RESTORE),
                    ApplicationWindowState.Minimized => ShowWindow(windowHandle, SW_MINIMIZE),
                    ApplicationWindowState.Maximized => ShowWindow(windowHandle, SW_MAXIMIZE),
                    ApplicationWindowState.Hidden => ShowWindow(windowHandle, SW_HIDE),
                    ApplicationWindowState.Active => SetForegroundWindow(windowHandle),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting window state for {Handle:X} to {State}", (long)windowHandle, windowState);
                return false;
            }
        }
        
        #endregion

        #region WSA (Windows Subsystem for Android) Support

        /// <summary>
        /// Найти WSA-окно по package name и activity name
        /// Использует корреляционный алгоритм поиска на основе времени запуска и заголовка
        /// </summary>
        /// <param name="packageName">Android package name (например, com.example.app)</param>
        /// <param name="activityName">Имя Android активности (опционально)</param>
        /// <returns>Информация о найденном WSA-окне или null</returns>
        public async Task<WindowInfo?> FindWSAWindowAsync(string packageName, string activityName = "")
        {
            await Task.CompletedTask; // Заглушка для async контракта
            
            try
            {
                _logger.LogDebug("Searching for WSA window with package: {PackageName}, activity: {ActivityName}", 
                    packageName, activityName);

                if (string.IsNullOrWhiteSpace(packageName))
                {
                    _logger.LogWarning("Package name is empty - cannot search for WSA window");
                    return null;
                }
                
                // НОВОЕ: Проверяем кэш перед дорогим поиском
                var cacheKey = $"{packageName}:{activityName}";
                lock (_cacheLocker)
                {
                    if (_wsaWindowCache.TryGetValue(cacheKey, out var cachedEntry))
                    {
                        var age = DateTime.Now - cachedEntry.CachedAt;
                        if (age < _wsaCacheTimeout)
                        {
                            // Проверяем что окно все еще существует
                            if (IsWindow(cachedEntry.Window.Handle))
                            {
                                _logger.LogTrace("Found WSA window in cache for {PackageName}: Handle={Handle:X} (Age: {Age})", 
                                    packageName, (long)cachedEntry.Window.Handle, age);
                                return cachedEntry.Window;
                            }
                            else
                            {
                                // Окно больше не существует, удаляем из кэша
                                _wsaWindowCache.Remove(cacheKey);
                                _logger.LogTrace("Removed stale WSA window from cache for {PackageName}", packageName);
                            }
                        }
                        else
                        {
                            // Кэш устарел, удаляем запись
                            _wsaWindowCache.Remove(cacheKey);
                            _logger.LogTrace("Removed expired WSA window from cache for {PackageName}", packageName);
                        }
                    }
                }

                // Шаг 1: Получить все WSA-окна (ApplicationFrameWindow)
                var wsaWindows = await GetWSAWindowsAsync();
                if (wsaWindows.Count == 0)
                {
                    _logger.LogDebug("No WSA windows found in system");
                    return null;
                }

                _logger.LogDebug("Found {Count} potential WSA windows to analyze", wsaWindows.Count);

                // Шаг 2: Корреляционный алгоритм поиска
                var currentTime = DateTime.Now;
                var searchTimeWindow = TimeSpan.FromMinutes(5); // Окно поиска 5 минут

                // Извлекаем простое имя приложения из package name (com.example.app -> app)
                var simpleAppName = ExtractSimpleAppName(packageName);

                foreach (var window in wsaWindows)
                {
                    // Фильтр по времени - окно должно быть создано недавно
                    var windowAge = currentTime - window.CreatedAt;
                    if (windowAge > searchTimeWindow)
                    {
                        _logger.LogTrace("Skipping window {Handle:X} - too old ({Age})", 
                            (long)window.Handle, windowAge);
                        continue;
                    }

                    // Проверка по заголовку окна (содержит имя приложения)
                    var titleMatch = CheckWindowTitleMatch(window.Title, packageName, simpleAppName);
                    if (titleMatch)
                    {
                        _logger.LogInformation("Found WSA window by title match: {Title} for package {PackageName}", 
                            window.Title, packageName);
                        
                        // НОВОЕ: Сохраняем в кэш перед возвратом
                        lock (_cacheLocker)
                        {
                            _wsaWindowCache[cacheKey] = (window, DateTime.Now);
                            _logger.LogTrace("Cached WSA window for {PackageName}: Handle={Handle:X}", 
                                packageName, (long)window.Handle);
                        }
                        
                        return window;
                    }

                    _logger.LogTrace("Window {Handle:X} title '{Title}' doesn't match package {PackageName}", 
                        (long)window.Handle, window.Title, packageName);
                }

                // Если точное совпадение не найдено, возвращаем самое свежее WSA-окно
                var newestWindow = wsaWindows
                    .Where(w => currentTime - w.CreatedAt <= searchTimeWindow)
                    .OrderByDescending(w => w.CreatedAt)
                    .FirstOrDefault();

                if (newestWindow != null)
                {
                    _logger.LogWarning("No exact match found for package {PackageName}, returning newest WSA window: {Title}", 
                        packageName, newestWindow.Title);
                    return newestWindow;
                }

                _logger.LogDebug("No suitable WSA window found for package: {PackageName}", packageName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for WSA window: {PackageName}", packageName);
                return null;
            }
        }

        /// <summary>
        /// Получить все WSA-окна в системе
        /// Использует двойной поиск: ApplicationFrameWindow + Chrome_WidgetWin_*
        /// </summary>
        /// <returns>Список всех WSA-окон</returns>
        public async Task<IReadOnlyList<WindowInfo>> GetWSAWindowsAsync()
        {
            try
            {
                _logger.LogDebug("Getting all WSA windows using dual search strategy");

                // Стратегия 1: Ищем окна класса ApplicationFrameWindow (классический WSA)
                var applicationFrameWindows = await FindWindowsByClassAsync("ApplicationFrameWindow");
                _logger.LogTrace("Found {Count} ApplicationFrameWindow instances", applicationFrameWindows.Count);

                // Стратегия 2: Ищем окна класса Chrome_WidgetWin_* (Chromium-based WSA)
                var chromeWidgetWindows = await FindWindowsByClassPatternAsync("Chrome_WidgetWin_*");
                _logger.LogTrace("Found {Count} Chrome_WidgetWin_* instances", chromeWidgetWindows.Count);

                // Объединяем результаты и убираем дубликаты по Handle
                var allCandidateWindows = new Dictionary<IntPtr, WindowInfo>();
                
                foreach (var window in applicationFrameWindows)
                {
                    allCandidateWindows[window.Handle] = window;
                }
                
                foreach (var window in chromeWidgetWindows)
                {
                    allCandidateWindows[window.Handle] = window;
                }

                _logger.LogTrace("Combined {Total} unique candidate windows from both searches", 
                    allCandidateWindows.Count);

                // Фильтруем кандидатов через WSA проверку
                var confirmedWSAWindows = new List<WindowInfo>();
                
                foreach (var window in allCandidateWindows.Values)
                {
                    // Дополнительная проверка что это именно WSA-окно
                    if (await IsWSAWindowAsync(window.Handle))
                    {
                        confirmedWSAWindows.Add(window);
                        _logger.LogTrace("Confirmed WSA window: {Handle:X}, Class: '{Class}', Title: '{Title}'", 
                            (long)window.Handle, window.ClassName, window.Title);
                    }
                    else
                    {
                        _logger.LogTrace("Window {Handle:X} rejected by WSA verification: '{Title}'", 
                            (long)window.Handle, window.Title);
                    }
                }

                _logger.LogDebug("Found {WSACount} confirmed WSA windows out of {CandidateCount} candidates " +
                    "({AppFrameCount} ApplicationFrameWindow + {ChromeCount} Chrome_WidgetWin_*)", 
                    confirmedWSAWindows.Count, allCandidateWindows.Count, 
                    applicationFrameWindows.Count, chromeWidgetWindows.Count);
                
                return confirmedWSAWindows.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WSA windows");
                return new List<WindowInfo>().AsReadOnly();
            }
        }

        /// <summary>
        /// Проверить, является ли окно WSA-окном
        /// Использует комбинированную проверку: класс окна + WSA процесс + эвристики
        /// </summary>
        /// <param name="windowHandle">Handle окна для проверки</param>
        /// <returns>true если окно создано WSA</returns>
        public async Task<bool> IsWSAWindowAsync(IntPtr windowHandle)
        {
            try
            {
                if (!IsWindow(windowHandle))
                {
                    _logger.LogTrace("Window handle {Handle:X} is not valid", (long)windowHandle);
                    return false;
                }

                // Получаем базовую информацию об окне
                var windowClass = GetWindowClass(windowHandle);
                var windowTitle = GetWindowTitle(windowHandle);
                
                GetWindowThreadProcessId(windowHandle, out uint processId);
                
                _logger.LogTrace("Checking WSA window: Handle={Handle:X}, Class='{Class}', Title='{Title}', PID={ProcessId}", 
                    (long)windowHandle, windowClass, windowTitle, processId);

                // Шаг 1: Проверка класса окна (поддерживаем оба типа WSA окон)
                bool hasWSAWindowClass = string.Equals(windowClass, "ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase) ||
                                       IsChromeWidgetWindow(windowClass);
                
                if (!hasWSAWindowClass)
                {
                    _logger.LogTrace("Window {Handle:X} rejected - unsupported class: {Class}", 
                        (long)windowHandle, windowClass);
                    return false;
                }

                // Шаг 2: Проверка WSA процесса (самый надежный критерий)
                bool isWSAProcess = await IsWSAProcessAsync((int)processId);
                
                if (isWSAProcess)
                {
                    _logger.LogDebug("Window {Handle:X} confirmed as WSA by process check: Class='{Class}', Title='{Title}'", 
                        (long)windowHandle, windowClass, windowTitle);
                    return true;
                }

                // Шаг 3: Если процесс не WSA, но класс подходящий - дополнительные эвристики
                _logger.LogTrace("Window {Handle:X} has WSA-compatible class but non-WSA process, applying heuristics", 
                    (long)windowHandle);

                // Исключаем известные Windows приложения
                var excludePatterns = new[]
                {
                    "Microsoft Store", "Settings", "Calculator", "Mail", "Calendar", "Photos",
                    "Movies & TV", "Groove Music", "Microsoft Edge", "File Explorer",
                    "Task Manager", "Control Panel", "Registry Editor"
                };

                foreach (var pattern in excludePatterns)
                {
                    if (windowTitle.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogTrace("Window {Handle:X} excluded as Windows app by pattern '{Pattern}': {Title}", 
                            (long)windowHandle, pattern, windowTitle);
                        return false;
                    }
                }

                // Пустой заголовок = системное окно
                if (string.IsNullOrWhiteSpace(windowTitle))
                {
                    _logger.LogTrace("Window {Handle:X} rejected - empty title", (long)windowHandle);
                    return false;
                }

                // Положительные индикаторы Android приложений
                var androidIndicators = new[]
                {
                    // Популярные Android приложения
                    "WhatsApp", "Telegram", "Instagram", "TikTok", "YouTube", "Facebook",
                    "Chrome", "Firefox", "VLC", "Spotify", "Netflix", "Amazon",
                    // Android package name признаки
                    "com.", "org.", "net.", "io.", "app.",
                    // Android UI элементы
                    "MainActivity", "Activity", "Fragment"
                };

                foreach (var indicator in androidIndicators)
                {
                    if (windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Window {Handle:X} identified as WSA by title indicator '{Indicator}': {Title}", 
                            (long)windowHandle, indicator, windowTitle);
                        return true;
                    }
                }

                // Для Chrome_WidgetWin_* окон, не принадлежащих WSA процессу - скорее всего НЕ WSA
                if (IsChromeWidgetWindow(windowClass))
                {
                    _logger.LogTrace("Window {Handle:X} rejected - Chrome_WidgetWin_* class but non-WSA process", 
                        (long)windowHandle);
                    return false;
                }

                // Для ApplicationFrameWindow без WSA процесса - консервативный подход
                _logger.LogDebug("Window {Handle:X} tentatively identified as WSA: class={Class}, title='{Title}' (fallback)", 
                    (long)windowHandle, windowClass, windowTitle);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if window is WSA: {Handle:X}", (long)windowHandle);
                return false;
            }
        }

        /// <summary>
        /// Извлекает простое имя приложения из Android package name
        /// Например: com.example.myapp -> myapp
        /// </summary>
        /// <param name="packageName">Android package name</param>
        /// <returns>Простое имя приложения</returns>
        private string ExtractSimpleAppName(string packageName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packageName))
                    return string.Empty;

                // Разделяем package name по точкам
                var parts = packageName.Split('.');
                if (parts.Length == 0)
                    return packageName;

                // Берем последнюю часть (обычно это имя приложения)
                var lastPart = parts[parts.Length - 1];
                
                // Если последняя часть слишком короткая или общая, берем предпоследнюю
                if (lastPart.Length <= 2 || IsCommonAppSuffix(lastPart))
                {
                    if (parts.Length >= 2)
                    {
                        lastPart = parts[parts.Length - 2];
                    }
                }

                _logger.LogTrace("Extracted simple app name '{SimpleAppName}' from package '{PackageName}'", 
                    lastPart, packageName);
                
                return lastPart;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting simple app name from package: {PackageName}", packageName);
                return packageName; // Fallback к исходному имени
            }
        }

        /// <summary>
        /// Проверяет, является ли суффикс общим для Android приложений
        /// </summary>
        /// <param name="suffix">Суффикс для проверки</param>
        /// <returns>true если суффикс общий</returns>
        private bool IsCommonAppSuffix(string suffix)
        {
            var commonSuffixes = new[] { "app", "main", "ui", "client", "mobile", "android" };
            return commonSuffixes.Contains(suffix.ToLowerInvariant());
        }

        /// <summary>
        /// Проверяет соответствие заголовка окна Android package name
        /// </summary>
        /// <param name="windowTitle">Заголовок окна</param>
        /// <param name="packageName">Android package name</param>
        /// <param name="simpleAppName">Простое имя приложения</param>
        /// <returns>true если заголовок соответствует приложению</returns>
        private bool CheckWindowTitleMatch(string windowTitle, string packageName, string simpleAppName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(windowTitle))
                    return false;

                var titleLower = windowTitle.ToLowerInvariant();
                var packageLower = packageName.ToLowerInvariant();
                var simpleAppLower = simpleAppName.ToLowerInvariant();

                // Точное совпадение с package name
                if (titleLower.Contains(packageLower))
                {
                    _logger.LogTrace("Window title contains full package name: '{Title}' contains '{PackageName}'", 
                        windowTitle, packageName);
                    return true;
                }

                // Совпадение с простым именем приложения
                if (!string.IsNullOrEmpty(simpleAppLower) && titleLower.Contains(simpleAppLower))
                {
                    _logger.LogTrace("Window title contains simple app name: '{Title}' contains '{SimpleAppName}'", 
                        windowTitle, simpleAppName);
                    return true;
                }

                // Проверка отдельных частей package name
                var packageParts = packageName.Split('.');
                foreach (var part in packageParts)
                {
                    if (part.Length >= 3 && !IsCommonAppSuffix(part) && 
                        titleLower.Contains(part.ToLowerInvariant()))
                    {
                        _logger.LogTrace("Window title contains package part: '{Title}' contains '{Part}'", 
                            windowTitle, part);
                        return true;
                    }
                }

                // Специальные случаи для известных Android приложений
                if (CheckSpecialAndroidAppCases(titleLower, packageLower))
                {
                    _logger.LogTrace("Window title matches special Android app case: '{Title}' for package '{PackageName}'", 
                        windowTitle, packageName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking window title match for package: {PackageName}", packageName);
                return false;
            }
        }

        /// <summary>
        /// Проверяет специальные случаи для известных Android приложений
        /// </summary>
        /// <param name="titleLower">Заголовок окна в нижнем регистре</param>
        /// <param name="packageLower">Package name в нижнем регистре</param>
        /// <returns>true если найдено совпадение</returns>
        private bool CheckSpecialAndroidAppCases(string titleLower, string packageLower)
        {
            // Известные mappings для популярных Android приложений
            var knownMappings = new Dictionary<string, string[]>
            {
                ["com.whatsapp"] = new[] { "whatsapp" },
                ["com.instagram.android"] = new[] { "instagram" },
                ["com.facebook.katana"] = new[] { "facebook" },
                ["com.twitter.android"] = new[] { "twitter" },
                ["com.google.android.youtube"] = new[] { "youtube" },
                ["com.spotify.music"] = new[] { "spotify" },
                ["com.netflix.mediaclient"] = new[] { "netflix" },
                ["com.microsoft.office.outlook"] = new[] { "outlook", "microsoft outlook" },
                ["com.adobe.reader"] = new[] { "adobe", "acrobat", "reader" },
                ["com.skype.raider"] = new[] { "skype" }
            };

            if (knownMappings.TryGetValue(packageLower, out var aliases))
            {
                return aliases.Any(alias => titleLower.Contains(alias));
            }

            return false;
        }

        /// <summary>
        /// Проверяет, является ли процесс WSA процессом
        /// </summary>
        /// <param name="processId">ID процесса для проверки</param>
        /// <returns>true если процесс принадлежит WSA</returns>
        private async Task<bool> IsWSAProcessAsync(int processId)
        {
            try
            {
                await Task.CompletedTask; // Заглушка для async контракта

                // Получаем имя процесса через Windows API
                var processName = GetProcessNameById(processId);
                if (string.IsNullOrEmpty(processName))
                    return false;

                var nameLower = processName.ToLowerInvariant();
                
                // Проверка известных имён WSA процессов
                if (nameLower == "wsaclient" || nameLower == "wsaservice" || 
                    nameLower == "wsa" || nameLower.Contains("subsystemforandroid"))
                {
                    _logger.LogTrace("Process {ProcessId} identified as WSA by name: {ProcessName}", 
                        processId, processName);
                    return true;
                }

                // Проверка пути процесса через Windows API
                try
                {
                    var processPath = GetProcessPathById(processId);
                    if (!string.IsNullOrEmpty(processPath))
                    {
                        var pathLower = processPath.ToLowerInvariant();
                        if (pathLower.Contains("microsoftcorporationii.windowssubsystemforandroid") ||
                            pathLower.Contains("subsystemforandroid") ||
                            pathLower.Contains("wsa"))
                        {
                            _logger.LogTrace("Process {ProcessId} identified as WSA by path: {Path}", 
                                processId, processPath);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not get process path for {ProcessId}", processId);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error checking if process {ProcessId} is WSA process", processId);
                return false;
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли класс окна паттерну Chrome_WidgetWin_*
        /// </summary>
        /// <param name="className">Класс окна для проверки</param>
        /// <returns>true если класс соответствует паттерну Chrome_WidgetWin_*</returns>
        private bool IsChromeWidgetWindow(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            // Chrome_WidgetWin_0, Chrome_WidgetWin_1, etc.
            return className.StartsWith("Chrome_WidgetWin_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Находит окна по паттерну класса (для Chrome_WidgetWin_*)
        /// </summary>
        /// <param name="classPattern">Паттерн класса для поиска</param>
        /// <returns>Список найденных окон</returns>
        private async Task<IReadOnlyList<WindowInfo>> FindWindowsByClassPatternAsync(string classPattern)
        {
            await Task.CompletedTask;
            
            var windows = new List<WindowInfo>();
            var enumCount = 0;
            var maxEnumCount = 2000;

            try
            {
                var startTime = DateTime.Now;
                
                EnumWindows((hWnd, lParam) =>
                {
                    enumCount++;
                    
                    if (enumCount > maxEnumCount || (DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        _logger.LogWarning("EnumWindows stopped due to safety limits while searching for pattern '{Pattern}'", classPattern);
                        return false;
                    }
                    
                    try
                    {
                        var className = GetWindowClass(hWnd);
                        
                        // Проверяем соответствие паттерну
                        bool matches = classPattern == "Chrome_WidgetWin_*" 
                            ? IsChromeWidgetWindow(className)
                            : string.Equals(className, classPattern, StringComparison.OrdinalIgnoreCase);
                        
                        if (matches && IsWindowVisible(hWnd))
                        {
                            GetWindowThreadProcessId(hWnd, out uint processId);
                            var windowInfo = CreateWindowInfoFromHandle(hWnd, processId);
                            
                            if (windowInfo != null)
                            {
                                windows.Add(windowInfo);
                                _logger.LogTrace("Found window with class pattern '{Pattern}': Handle={Handle:X}, Title='{Title}'", 
                                    classPattern, (long)hWnd, windowInfo.Title);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error processing window handle {Handle:X} during pattern search", (long)hWnd);
                    }
                    
                    return true;
                }, IntPtr.Zero);
                
                _logger.LogDebug("Found {Count} windows matching class pattern '{Pattern}' in {EnumCount} enumerated windows", 
                    windows.Count, classPattern, enumCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for windows by class pattern '{Pattern}'", classPattern);
            }
            
            return windows;
        }

        /// <summary>
        /// Получает имя процесса по его ID через Process.GetProcessById (безопасный метод)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Имя процесса или empty string при ошибке</returns>
        private string GetProcessNameById(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return string.Empty;
                
                return process.ProcessName ?? string.Empty;
            }
            catch (ArgumentException)
            {
                // Process not found
                return string.Empty;
            }
            catch (InvalidOperationException)
            {
                // Process has exited
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error getting process name for PID {ProcessId}", processId);
                return string.Empty;
            }
        }

        /// <summary>
        /// Получает полный путь к исполняемому файлу процесса по его ID через Process.GetProcessById (безопасный метод)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Полный путь к процессу или empty string при ошибке</returns>
        private string GetProcessPathById(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return string.Empty;
                
                // Пытаемся получить путь к исполняемому файлу
                try
                {
                    var mainModule = process.MainModule;
                    if (mainModule != null)
                    {
                        return mainModule.FileName ?? string.Empty;
                    }
                }
                catch (Win32Exception)
                {
                    // Access denied или процесс системный
                    _logger.LogTrace("Access denied getting main module for PID {ProcessId}", processId);
                }
                catch (InvalidOperationException)
                {
                    // Process has exited during access
                    return string.Empty;
                }
                
                // Fallback: используем GetProcessImageFileName через P/Invoke
                IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)processId);
                if (processHandle != IntPtr.Zero)
                {
                    try
                    {
                        var fileName = new StringBuilder(1024);
                        uint size = GetProcessImageFileName(processHandle, fileName, (uint)fileName.Capacity);
                        if (size > 0)
                        {
                            return fileName.ToString();
                        }
                    }
                    finally
                    {
                        CloseHandle(processHandle);
                    }
                }
            }
            catch (ArgumentException)
            {
                // Process not found
                return string.Empty;
            }
            catch (InvalidOperationException)
            {
                // Process has exited
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error getting process path for PID {ProcessId}", processId);
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Ищет WSA приложение по заданным критериям используя правильные Windows API
        /// </summary>
        /// <param name="packageName">Package name Android приложения</param>
        /// <param name="activityName">Activity name (может быть null)</param>
        /// <param name="windowName">Точное имя окна для поиска (из AndroidArgumentsParser)</param>
        /// <returns>Информация о найденном окне или null</returns>
        public async Task<WindowInfo?> FindWSAApplicationWindowAsync(string packageName, string? activityName = null, string? windowName = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Searching for WSA window: Package={PackageName}, Activity={ActivityName}, WindowName={WindowName}", 
                        packageName, activityName, windowName);

                    var wsaWindows = new List<(IntPtr hWnd, string title, string className, uint processId, string processName)>();
                    
                    // Перебираем все окна системы
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            // Получаем класс окна
                            var className = new StringBuilder(256);
                            int classResult = GetClassNameW(hWnd, className, className.Capacity);
                            
                            if (classResult == 0 || className.ToString() != "ApplicationFrameWindow")
                            {
                                return true; // Продолжаем поиск
                            }
                            
                            // Получаем заголовок окна
                            var windowTitle = new StringBuilder(256);
                            GetWindowTextW(hWnd, windowTitle, windowTitle.Capacity);
                            
                            // Получаем PID процесса (используем правильное имя параметра)
                            uint processId = 0;
                            GetWindowThreadProcessId(hWnd, out processId);
                            
                            if (processId == 0)
                            {
                                return true; // Продолжаем поиск
                            }
                            
                            // Получаем имя процесса
                            string processName = GetProcessNameById((int)processId);
                            
                            // Проверяем что это WSA процесс
                            if (IsWSAProcess(processName))
                            {
                                wsaWindows.Add((hWnd, windowTitle.ToString(), className.ToString(), processId, processName));
                                
                                _logger.LogTrace("Found WSA window: Handle={Handle:X}, Title='{Title}', PID={ProcessId}, Process={ProcessName}", 
                                    (long)hWnd, windowTitle.ToString(), processId, processName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogTrace(ex, "Error processing window during WSA search");
                        }
                        
                        return true; // Продолжаем поиск
                    }, IntPtr.Zero);
                    
                    _logger.LogDebug("Found {Count} WSA windows total", wsaWindows.Count);
                    
                    // Ищем наиболее подходящее окно
                    foreach (var (hWnd, title, className, processId, processName) in wsaWindows)
                    {
                        // Приоритет 1: Точное совпадение по window_name
                        if (!string.IsNullOrEmpty(windowName) && 
                            title.Equals(windowName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found WSA window by exact window_name match: '{WindowName}', PID={ProcessId}", 
                                windowName, processId);
                            
                            return CreateWindowInfoFromHandle(hWnd, title, (int)processId);
                        }
                        
                        // Приоритет 2: Содержит package name в заголовке
                        if (title.Contains(packageName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found WSA window by package name match: '{Title}' contains '{PackageName}', PID={ProcessId}", 
                                title, packageName, processId);
                            
                            return CreateWindowInfoFromHandle(hWnd, title, (int)processId);
                        }
                        
                        // Приоритет 3: Содержит activity name в заголовке
                        if (!string.IsNullOrEmpty(activityName) && 
                            title.Contains(activityName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found WSA window by activity name match: '{Title}' contains '{ActivityName}', PID={ProcessId}", 
                                title, activityName, processId);
                            
                            return CreateWindowInfoFromHandle(hWnd, title, (int)processId);
                        }
                    }
                    
                    // Если точного совпадения нет, берем первое WSA окно
                    if (wsaWindows.Count > 0)
                    {
                        var (hWnd, title, className, processId, processName) = wsaWindows[0];
                        _logger.LogInformation("Using first available WSA window: '{Title}', PID={ProcessId}", title, processId);
                        
                        return CreateWindowInfoFromHandle(hWnd, title, (int)processId);
                    }
                    
                    _logger.LogWarning("No WSA windows found for package '{PackageName}'", packageName);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching for WSA application window");
                    return null;
                }
            });
        }
        
        /// <summary>
        /// Проверяет, является ли процесс WSA процессом
        /// </summary>
        /// <param name="processName">Имя процесса</param>
        /// <returns>true если это WSA процесс</returns>
        private static bool IsWSAProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
                
            return processName.Equals("WsaClient", StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("WindowsSubsystemForAndroid", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Создает WindowInfo из Handle окна
        /// </summary>
        /// <param name="hWnd">Handle окна</param>
        /// <param name="title">Заголовок окна</param>
        /// <param name="processId">PID процесса</param>
        /// <returns>WindowInfo объект</returns>
        private WindowInfo CreateWindowInfoFromHandle(IntPtr hWnd, string title, int processId)
        {
            var windowInfo = new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessId = (uint)processId, // Приведение к uint
                ClassName = "ApplicationFrameWindow",
                IsVisible = IsWindowVisible(hWnd)
            };

            // Получаем размеры и позицию окна
            try
            {
                RECT rect;
                if (GetWindowRect(hWnd, out rect))
                {
                    windowInfo.X = rect.Left;
                    windowInfo.Y = rect.Top;
                    windowInfo.Width = rect.Right - rect.Left;
                    windowInfo.Height = rect.Bottom - rect.Top;
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error getting window rect for handle {Handle:X}", (long)hWnd);
            }

            return windowInfo;
        }

        #endregion
        
        #region WSA Window Cache Management
        
        /// <summary>
        /// Очистить кэш WSA окон
        /// Полезно при проблемах с кэшированием или для принудительного обновления
        /// </summary>
        public void ClearWSAWindowCache()
        {
            lock (_cacheLocker)
            {
                var count = _wsaWindowCache.Count;
                _wsaWindowCache.Clear();
                _logger.LogDebug("Cleared WSA window cache ({Count} entries removed)", count);
            }
        }
        
        /// <summary>
        /// Очистить устаревшие записи из кэша WSA окон
        /// Вызывается автоматически, но можно вызвать принудительно
        /// </summary>
        public void CleanupExpiredWSAWindowCache()
        {
            lock (_cacheLocker)
            {
                var expiredKeys = new List<string>();
                var currentTime = DateTime.Now;
                
                foreach (var kvp in _wsaWindowCache)
                {
                    var age = currentTime - kvp.Value.CachedAt;
                    if (age >= _wsaCacheTimeout || !IsWindow(kvp.Value.Window.Handle))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (var key in expiredKeys)
                {
                    _wsaWindowCache.Remove(key);
                }
                
                if (expiredKeys.Count > 0)
                {
                    _logger.LogTrace("Cleaned up {Count} expired WSA window cache entries", expiredKeys.Count);
                }
            }
        }
        
        /// <summary>
        /// Получить статистику кэша WSA окон для диагностики
        /// </summary>
        /// <returns>Информация о состоянии кэша</returns>
        public (int TotalEntries, int ValidEntries, int ExpiredEntries) GetWSAWindowCacheStats()
        {
            lock (_cacheLocker)
            {
                var total = _wsaWindowCache.Count;
                var valid = 0;
                var expired = 0;
                var currentTime = DateTime.Now;
                
                foreach (var kvp in _wsaWindowCache)
                {
                    var age = currentTime - kvp.Value.CachedAt;
                    if (age < _wsaCacheTimeout && IsWindow(kvp.Value.Window.Handle))
                    {
                        valid++;
                    }
                    else
                    {
                        expired++;
                    }
                }
                
                return (total, valid, expired);
            }
        }

        #endregion
    }
}
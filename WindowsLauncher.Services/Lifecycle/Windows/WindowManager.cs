using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    }
}
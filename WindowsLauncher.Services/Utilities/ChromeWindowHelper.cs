using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Configuration;

namespace WindowsLauncher.Services.Utilities
{
    /// <summary>
    /// Утилита для работы с Chrome окнами через Windows API
    /// Решает проблему с Process.MainWindowTitle для Chrome Apps
    /// </summary>
    public static class ChromeWindowHelper
    {
        #region Windows API

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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        /// <summary>
        /// Информация о Chrome окне
        /// </summary>
        public class ChromeWindowInfo
        {
            public IntPtr WindowHandle { get; set; }
            public uint ProcessId { get; set; }
            public string WindowTitle { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public bool IsVisible { get; set; }
        }

        /// <summary>
        /// Получить все видимые Chrome окна с их заголовками
        /// </summary>
        public static List<ChromeWindowInfo> GetAllChromeWindows(ILogger? logger = null, ChromeWindowSearchOptions? options = null)
        {
            // Используем переданные настройки или значения по умолчанию
            var config = options ?? new ChromeWindowSearchOptions();
            config.Validate();

            var chromeWindows = new List<ChromeWindowInfo>();
            var allWindows = new List<(IntPtr handle, string className, string title, uint processId)>();
            var enumCount = 0;

            try
            {
                var startTime = DateTime.Now;
                
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        enumCount++;
                        
                        // Защита от зависания - ограничиваем количество обработанных окон и время
                        if (enumCount > config.MaxEnumCount || (DateTime.Now - startTime).TotalSeconds > config.MaxEnumTimeSeconds)
                        {
                            logger?.LogWarning("EnumWindows stopped due to safety limits: Count={Count}, Time={Time}s (MaxCount={MaxCount}, MaxTime={MaxTime}s)", 
                                enumCount, (DateTime.Now - startTime).TotalSeconds, config.MaxEnumCount, config.MaxEnumTimeSeconds);
                            return false; // Прерываем перечисление
                        }

                        // Получаем класс окна
                        var className = new StringBuilder(256);
                        GetClassName(hWnd, className, className.Capacity);
                        string classNameStr = className.ToString();

                        // Получаем PID процесса
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        // Получаем заголовок окна
                        string titleStr = "";
                        int titleLength = GetWindowTextLength(hWnd);
                        if (titleLength > 0 && titleLength < config.MaxTitleLength) // Ограничиваем длину заголовка
                        {
                            var title = new StringBuilder(titleLength + 1);
                            GetWindowText(hWnd, title, title.Capacity);
                            titleStr = title.ToString();
                        }

                        // Собираем информацию о всех окнах для диагностики
                        if (allWindows.Count < config.MaxDiagnosticWindows)
                        {
                            allWindows.Add((hWnd, classNameStr, titleStr, processId));
                        }

                        // Проверяем, что окно видимо
                        if (!IsWindowVisible(hWnd))
                            return true;

                        // Проверяем, что это Chrome окно
                        if (!IsChromeWindow(classNameStr))
                            return true;

                        // Фильтруем системные окна Chrome
                        if (!IsSystemChromeWindow(titleStr))
                        {
                            chromeWindows.Add(new ChromeWindowInfo
                            {
                                WindowHandle = hWnd,
                                ProcessId = processId,
                                WindowTitle = titleStr,
                                ClassName = classNameStr,
                                IsVisible = true
                            });

                            logger?.LogDebug("Found Chrome window: PID {ProcessId}, Title: '{Title}', Class: {ClassName}", 
                                processId, titleStr, classNameStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Error processing window handle {Handle}", hWnd);
                    }

                    return true; // Продолжаем перечисление
                }, IntPtr.Zero);

                // Расширенная диагностическая информация
                logger?.LogInformation("=== ENUM WINDOWS SUMMARY ===\n" +
                                     "Total windows enumerated: {Count} (stopped at {EnumCount})\n" +
                                     "Found Chrome windows: {ChromeCount}", 
                                     allWindows.Count, enumCount, chromeWindows.Count);
                
                var chromeRelatedWindows = allWindows.Where(w => 
                    w.className.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                    w.title.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                    w.className.Contains("Edge", StringComparison.OrdinalIgnoreCase)
                ).ToList();
                
                logger?.LogInformation("Windows with 'Chrome'/'Edge' in class/title: {Count}", chromeRelatedWindows.Count);
                
                // Показываем все Chrome-related окна для полной диагностики
                foreach (var window in chromeRelatedWindows.Take(10))
                {
                    bool isVisible = IsWindowVisible(window.handle);
                    bool isChromeClass = IsChromeWindow(window.className);
                    bool isSystemWindow = IsSystemChromeWindow(window.title);
                    
                    logger?.LogInformation("Chrome-related: PID {ProcessId}, Class: '{ClassName}', Title: '{Title}', " +
                                         "Visible: {Visible}, IsChromeClass: {IsChromeClass}, IsSystem: {IsSystemWindow}",
                        window.processId, window.className, window.title, isVisible, isChromeClass, isSystemWindow);
                }
                
                // Показываем первые 10 окон любого типа для общей диагностики
                logger?.LogDebug("First 10 windows of any type:");
                foreach (var window in allWindows.Take(10))
                {
                    logger?.LogDebug("  PID {ProcessId}, Class: '{ClassName}', Title: '{Title}'", 
                        window.processId, window.className, window.title);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error enumerating Chrome windows");
            }

            logger?.LogDebug("Found {Count} Chrome windows total", chromeWindows.Count);
            return chromeWindows;
        }

        /// <summary>
        /// Найти Chrome окно по заголовку для конкретного процесса
        /// </summary>
        public static ChromeWindowInfo? FindChromeWindowByTitle(int processId, string expectedTitle, ILogger? logger = null, ChromeWindowSearchOptions? options = null)
        {
            logger?.LogInformation("=== CHROME WINDOW SEARCH START ===\n" +
                                 "Target ProcessId: {ProcessId}\n" +
                                 "Expected Title: '{ExpectedTitle}'", processId, expectedTitle);
            
            var allChromeWindows = GetAllChromeWindows(logger, options);
            
            logger?.LogInformation("Standard approach found {Count} Chrome windows:", allChromeWindows.Count);
            for (int i = 0; i < allChromeWindows.Count; i++)
            {
                var w = allChromeWindows[i];
                logger?.LogInformation("  [{Index}] PID: {ProcessId}, Title: '{Title}', Class: '{ClassName}', Visible: {Visible}", 
                    i + 1, w.ProcessId, w.WindowTitle, w.ClassName, w.IsVisible);
            }
            
            // Если мы не нашли Chrome окна через стандартный подход, попробуем альтернативный
            if (allChromeWindows.Count == 0)
            {
                logger?.LogWarning("No Chrome windows found via standard approach, trying alternative method");
                allChromeWindows = GetChromeWindowsAlternative(logger, options);
                
                logger?.LogInformation("Alternative approach found {Count} Chrome windows:", allChromeWindows.Count);
                for (int i = 0; i < allChromeWindows.Count; i++)
                {
                    var w = allChromeWindows[i];
                    logger?.LogInformation("  ALT[{Index}] PID: {ProcessId}, Title: '{Title}', Class: '{ClassName}', Visible: {Visible}", 
                        i + 1, w.ProcessId, w.WindowTitle, w.ClassName, w.IsVisible);
                }
            }
            
            // ПОИСК 1: Сначала ищем точное совпадение для этого процесса
            logger?.LogDebug("SEARCH 1: Looking for exact title match in target process {ProcessId}", processId);
            foreach (var window in allChromeWindows)
            {
                bool processMatch = processId > 0 && window.ProcessId == processId;
                bool titleMatch = string.Equals(window.WindowTitle, expectedTitle, StringComparison.OrdinalIgnoreCase);
                
                logger?.LogTrace("  Checking PID {WindowPid}: ProcessMatch={ProcessMatch}, TitleMatch={TitleMatch}, Title='{WindowTitle}'", 
                    window.ProcessId, processMatch, titleMatch, window.WindowTitle);
                
                if (processMatch && titleMatch)
                {
                    logger?.LogInformation("✅ FOUND exact match: PID {ProcessId}, Title: '{Title}'", processId, window.WindowTitle);
                    return window;
                }
            }

            // ПОИСК 2: Ищем частичное совпадение для этого процесса
            logger?.LogDebug("SEARCH 2: Looking for partial title match in target process {ProcessId}", processId);
            foreach (var window in allChromeWindows)
            {
                bool processMatch = processId > 0 && window.ProcessId == processId;
                bool titleContains = window.WindowTitle.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase);
                
                logger?.LogTrace("  Checking PID {WindowPid}: ProcessMatch={ProcessMatch}, TitleContains={TitleContains}, Title='{WindowTitle}'", 
                    window.ProcessId, processMatch, titleContains, window.WindowTitle);
                
                if (processMatch && titleContains)
                {
                    logger?.LogInformation("✅ FOUND partial match: PID {ProcessId}, Title: '{Title}' contains '{Expected}'", 
                        processId, window.WindowTitle, expectedTitle);
                    return window;
                }
            }

            // ПОИСК 3: Если не нашли для конкретного процесса, ищем среди всех Chrome окон
            logger?.LogDebug("SEARCH 3: Looking for exact title match in any Chrome process");
            foreach (var window in allChromeWindows)
            {
                bool titleMatch = string.Equals(window.WindowTitle, expectedTitle, StringComparison.OrdinalIgnoreCase);
                
                logger?.LogTrace("  Checking any PID {WindowPid}: TitleMatch={TitleMatch}, Title='{WindowTitle}'", 
                    window.ProcessId, titleMatch, window.WindowTitle);
                
                if (titleMatch)
                {
                    logger?.LogInformation("✅ FOUND exact match in any process: PID {ProcessId}, Title: '{Title}'", 
                        window.ProcessId, window.WindowTitle);
                    return window;
                }
            }

            // ПОИСК 4: Ищем частичное совпадение среди всех Chrome окон
            logger?.LogDebug("SEARCH 4: Looking for partial title match in any Chrome process");
            foreach (var window in allChromeWindows)
            {
                bool titleContains = window.WindowTitle.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase);
                
                logger?.LogTrace("  Checking any PID {WindowPid}: TitleContains={TitleContains}, Title='{WindowTitle}'", 
                    window.ProcessId, titleContains, window.WindowTitle);
                
                if (titleContains)
                {
                    logger?.LogInformation("✅ FOUND partial match in any process: PID {ProcessId}, Title: '{Title}' contains '{Expected}'", 
                        window.ProcessId, window.WindowTitle, expectedTitle);
                    return window;
                }
            }

            logger?.LogWarning("❌ NO CHROME WINDOW FOUND with title containing: '{ExpectedTitle}' in {WindowCount} windows", 
                expectedTitle, allChromeWindows.Count);
            logger?.LogWarning("=== CHROME WINDOW SEARCH END - FAILED ===");
            return null;
        }

        /// <summary>
        /// Альтернативный метод поиска Chrome окон - через процессы Chrome
        /// </summary>
        private static List<ChromeWindowInfo> GetChromeWindowsAlternative(ILogger? logger = null, ChromeWindowSearchOptions? options = null)
        {
            var chromeWindows = new List<ChromeWindowInfo>();

            try
            {
                // Найдем все Chrome процессы безопасно
                var allProcesses = System.Diagnostics.Process.GetProcesses();
                var chromeProcesses = new List<System.Diagnostics.Process>();
                
                // Безопасная фильтрация Chrome процессов
                foreach (var p in allProcesses)
                {
                    try
                    {
                        // Проверяем HasExited первым
                        if (p.HasExited) 
                            continue;
                            
                        // Проверяем имя процесса
                        var processName = p.ProcessName?.ToLowerInvariant() ?? "";
                        if (processName.Contains("chrome") || processName.Contains("msedge"))
                        {
                            chromeProcesses.Add(p);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс недоступен, это нормально
                        continue;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogTrace(ex, "Unexpected error checking process {ProcessId} in alternative search", p.Id);
                    }
                }

                logger?.LogDebug("Found {Count} Chrome/Edge processes for alternative search", chromeProcesses.Count);

                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        // Проверяем снова что процесс не завершился
                        if (process.HasExited)
                            continue;
                            
                        // Проверяем наличие главного окна
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            string windowTitle = "";
                            try 
                            {
                                windowTitle = process.MainWindowTitle ?? "";
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                                // MainWindowTitle недоступен, пропускаем
                                continue;
                            }
                            
                            if (!string.IsNullOrEmpty(windowTitle))
                            {
                                // Получаем класс окна для этого handle
                                var className = new StringBuilder(256);
                                GetClassName(process.MainWindowHandle, className, className.Capacity);

                                chromeWindows.Add(new ChromeWindowInfo
                                {
                                    WindowHandle = process.MainWindowHandle,
                                    ProcessId = (uint)process.Id,
                                    WindowTitle = windowTitle,
                                    ClassName = className.ToString(),
                                    IsVisible = IsWindowVisible(process.MainWindowHandle)
                                });

                                logger?.LogDebug("Alternative method found Chrome window: PID {ProcessId}, Title: '{Title}', Class: {ClassName}", 
                                    process.Id, windowTitle, className.ToString());
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Win32Exception - процесс стал недоступен во время проверки
                        continue;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogTrace(ex, "Unexpected error processing Chrome process {ProcessId} in alternative search", process.Id);
                    }
                }

                // Освобождаем ресурсы
                foreach (var process in chromeProcesses)
                {
                    try { process.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error in alternative Chrome window search");
            }

            logger?.LogDebug("Alternative method found {Count} Chrome windows", chromeWindows.Count);
            return chromeWindows;
        }

        /// <summary>
        /// Получить все Chrome окна для конкретного процесса
        /// </summary>
        public static List<ChromeWindowInfo> GetChromeWindowsForProcess(int processId, ILogger? logger = null, ChromeWindowSearchOptions? options = null)
        {
            var allChromeWindows = GetAllChromeWindows(logger, options);
            var processWindows = new List<ChromeWindowInfo>();

            foreach (var window in allChromeWindows)
            {
                if (window.ProcessId == processId)
                {
                    processWindows.Add(window);
                }
            }

            logger?.LogDebug("Found {Count} Chrome windows for process {ProcessId}", processWindows.Count, processId);
            return processWindows;
        }

        /// <summary>
        /// Проверить, является ли класс окна Chrome окном
        /// </summary>
        private static bool IsChromeWindow(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            // Строгие критерии для избежания зависания - только конкретные классы Chrome
            return className.StartsWith("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Chrome_WidgetWin_0", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Chrome_WidgetWin_2", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверить, является ли окно системным Chrome окном (исключаем из результатов)
        /// </summary>
        private static bool IsSystemChromeWindow(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return true;

            // Исключаем системные окна Chrome
            var systemTitles = new[]
            {
                "Chrome App Launcher",
                "Task Manager - Google Chrome",
                "Developer Tools",
                "Chrome Remote Desktop",
                ""
            };

            foreach (var systemTitle in systemTitles)
            {
                if (title.Equals(systemTitle, StringComparison.OrdinalIgnoreCase) ||
                    title.StartsWith(systemTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Получить реальный заголовок Chrome процесса (вместо Process.MainWindowTitle)
        /// </summary>
        public static string GetChromeProcessTitle(int processId, ILogger? logger = null, ChromeWindowSearchOptions? options = null)
        {
            try
            {
                var windows = GetChromeWindowsForProcess(processId, logger, options);
                
                // Возвращаем заголовок первого найденного окна
                if (windows.Count > 0)
                {
                    var title = windows[0].WindowTitle;
                    logger?.LogDebug("Got Chrome process {ProcessId} title: '{Title}'", processId, title);
                    return title;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error getting Chrome process {ProcessId} title", processId);
            }

            return string.Empty;
        }
    }
}
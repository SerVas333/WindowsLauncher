using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления сенсорной клавиатурой Windows
    /// </summary>
    public class VirtualKeyboardService : IVirtualKeyboardService
    {
        private readonly ILogger<VirtualKeyboardService> _logger;
        private const string TOUCH_KEYBOARD_PROCESS_NAME = "TabTip";
        private const string TOUCH_KEYBOARD_EXECUTABLE_PATH = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";
        private const string FALLBACK_OSK_PROCESS_NAME = "osk";
        private const string FALLBACK_OSK_EXECUTABLE_PATH = @"C:\Windows\System32\osk.exe";

        public event EventHandler<VirtualKeyboardStateChangedEventArgs>? StateChanged;

        #region Windows API для управления сенсорной клавиатурой

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Константы для ShowWindow
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWDEFAULT = 10;

        // Константы для SetWindowPos
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Имена классов для поиска окон клавиатуры
        private const string TOUCH_KEYBOARD_CLASS_NAME = "IPTip_Main_Window";
        private const string OSK_CLASS_NAME = "OSKMainClass";

        #endregion

        public VirtualKeyboardService(ILogger<VirtualKeyboardService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Показать сенсорную клавиатуру
        /// </summary>
        public async Task<bool> ShowVirtualKeyboardAsync()
        {
            try
            {
                _logger.LogInformation("Попытка показать виртуальную клавиатуру");

                // Метод 1: Попытка показать уже запущенную клавиатуру через Windows API
                bool existingShown = await ShowExistingKeyboardWindow();
                if (existingShown)
                {
                    _logger.LogInformation("Показана существующая сенсорная клавиатура");
                    
                    // Дополнительная попытка принудительного позиционирования для уверенности
                    bool repositioned = await RepositionKeyboardAsync();
                    _logger.LogDebug("Принудительное позиционирование: {Repositioned}", repositioned);
                    
                    OnStateChanged(true, "Виртуальная клавиатура показана");
                    return true;
                }

                // Метод 2: Запуск TabTip с принудительным показом
                if (await ForceStartTabTipKeyboard())
                {
                    _logger.LogInformation("TabTip принудительно запущен и показан");
                    OnStateChanged(true, "Сенсорная клавиатура запущена");
                    return true;
                }

                // Метод 3: Запуск через реестр и перезапуск процесса
                if (await StartTabTipWithRegistryHack())
                {
                    _logger.LogInformation("TabTip запущен через реестр");
                    OnStateChanged(true, "Сенсорная клавиатура запущена");
                    return true;
                }

                // Метод 4: Fallback на OSK
                if (await TryStartOSK())
                {
                    _logger.LogInformation("Запущена классическая экранная клавиатура OSK");
                    OnStateChanged(true, "Экранная клавиатура запущена");
                    return true;
                }

                _logger.LogError("Не удалось запустить ни одну из виртуальных клавиатур");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске виртуальной клавиатуры");
                return false;
            }
        }

        private async Task<bool> ShowExistingKeyboardWindow()
        {
            try
            {
                // Ищем уже запущенное окно сенсорной клавиатуры
                var touchKeyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (touchKeyboardHandle != IntPtr.Zero)
                {
                    _logger.LogDebug("Найдено окно сенсорной клавиатуры, пытаемся показать и позиционировать");
                    
                    // Проверяем текущее состояние окна
                    bool wasVisible = IsWindowVisible(touchKeyboardHandle);
                    _logger.LogDebug("Окно было видимо: {WasVisible}", wasVisible);
                    
                    // Получаем текущую позицию окна
                    if (GetWindowRect(touchKeyboardHandle, out RECT rect))
                    {
                        _logger.LogDebug("Позиция окна: Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}", 
                                       rect.Left, rect.Top, rect.Right, rect.Bottom);
                    }
                    
                    // Мягкий показ окна без агрессивного перехвата фокуса
                    ShowWindow(touchKeyboardHandle, SW_SHOWNOACTIVATE); // Показываем БЕЗ активации
                    await Task.Delay(200);
                    
                    // НЕ вызываем SetForegroundWindow - это крадет фокус!
                    // НЕ устанавливаем TOPMOST - это мешает работе приложения!
                    
                    // Проверяем результат
                    bool isNowVisible = IsWindowVisible(touchKeyboardHandle);
                    _logger.LogInformation("Сенсорная клавиатура теперь видима: {IsVisible}", isNowVisible);
                    
                    return isNowVisible;
                }

                // Ищем окно OSK если TabTip не найден
                var oskHandle = FindWindow(OSK_CLASS_NAME, null);
                if (oskHandle != IntPtr.Zero)
                {
                    _logger.LogDebug("Найдено окно OSK, пытаемся показать");
                    ShowWindow(oskHandle, SW_SHOWNOACTIVATE); // Показываем БЕЗ активации
                    await Task.Delay(300);
                    return IsWindowVisible(oskHandle);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при попытке показать существующее окно клавиатуры");
                return false;
            }
        }

        private async Task<bool> ForceStartTabTipKeyboard()
        {
            try
            {
                // Сначала убиваем все существующие процессы TabTip
                await KillExistingTabTipProcesses();

                // Метод с использованием cmd для запуска
                var cmdStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + TOUCH_KEYBOARD_EXECUTABLE_PATH + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _logger.LogInformation("Запуск TabTip через cmd: {Path}", TOUCH_KEYBOARD_EXECUTABLE_PATH);
                
                if (File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH))
                {
                    var process = Process.Start(cmdStartInfo);
                    if (process != null)
                    {
                        await Task.Delay(2000); // Даем больше времени на запуск
                        
                        // Мягко показываем окно без перехвата фокуса
                        for (int i = 0; i < 3; i++) // Уменьшаем количество попыток
                        {
                            var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                            if (keyboardHandle != IntPtr.Zero)
                            {
                                ShowWindow(keyboardHandle, SW_SHOWNOACTIVATE); // БЕЗ активации
                                await Task.Delay(300);
                                
                                // Проверяем, действительно ли окно видимо
                                if (IsTabTipRunning() && IsKeyboardWindowVisible())
                                {
                                    return true;
                                }
                            }
                            await Task.Delay(700);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка принудительного запуска TabTip");
                return false;
            }
        }

        private async Task<bool> StartTabTipWithRegistryHack()
        {
            try
            {
                _logger.LogInformation("Пытаемся включить сенсорную клавиатуру через реестр");

                // Используем reg add для включения сенсорной клавиатуры
                var regProcess = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = "add \"HKCU\\Software\\Microsoft\\TabletTip\\1.7\" /v EnableDesktopModeAutoInvoke /t REG_DWORD /d 1 /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var regResult = Process.Start(regProcess);
                if (regResult != null)
                {
                    await regResult.WaitForExitAsync();
                    _logger.LogDebug("Реестр обновлен для включения автоматического вызова");
                }

                // Теперь пытаемся запустить TabTip
                if (File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH))
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = TOUCH_KEYBOARD_EXECUTABLE_PATH,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    });

                    if (process != null)
                    {
                        await Task.Delay(3000);
                        
                        // Ищем и мягко показываем окно
                        var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                        if (keyboardHandle != IntPtr.Zero)
                        {
                            ShowWindow(keyboardHandle, SW_SHOWNOACTIVATE);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка запуска через реестр");
                return false;
            }
        }

        private async Task KillExistingTabTipProcesses()
        {
            try
            {
                var processes = Process.GetProcessesByName(TOUCH_KEYBOARD_PROCESS_NAME);
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            await process.WaitForExitAsync();
                        }
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось завершить процесс TabTip {ProcessId}", process.Id);
                    }
                }
                
                if (processes.Length > 0)
                {
                    await Task.Delay(1000); // Даем время на завершение
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при завершении процессов TabTip");
            }
        }

        private bool IsKeyboardWindowVisible()
        {
            try
            {
                var touchKeyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (touchKeyboardHandle != IntPtr.Zero)
                {
                    // Здесь можно добавить дополнительные проверки видимости окна
                    return true;
                }

                var oskHandle = FindWindow(OSK_CLASS_NAME, null);
                return oskHandle != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка проверки видимости окна клавиатуры");
                return false;
            }
        }



        private async Task<bool> TryStartOSK()
        {
            try
            {
                if (!File.Exists(FALLBACK_OSK_EXECUTABLE_PATH))
                {
                    _logger.LogInformation("osk.exe не найден по пути {Path}", FALLBACK_OSK_EXECUTABLE_PATH);
                    return false;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = FALLBACK_OSK_EXECUTABLE_PATH,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var process = Process.Start(processStartInfo);
                
                if (process != null)
                {
                    // Ждем немного, чтобы процесс полностью запустился
                    await Task.Delay(500);
                    
                    // Проверяем, что процесс действительно запустился
                    if (IsOSKRunning())
                    {
                        _logger.LogInformation("Классическая экранная клавиатура OSK успешно запущена");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при запуске классической экранной клавиатуры OSK");
                return false;
            }
        }

        /// <summary>
        /// Скрыть виртуальную клавиатуру
        /// </summary>
        public async Task<bool> HideVirtualKeyboardAsync()
        {
            try
            {
                _logger.LogInformation("Попытка скрыть виртуальную клавиатуру");

                // Метод 1: Скрытие через Windows API
                if (await HideKeyboardWindow())
                {
                    _logger.LogInformation("Клавиатура скрыта через Windows API");
                    OnStateChanged(false, "Виртуальная клавиатура скрыта");
                    return true;
                }

                // Метод 2: Закрытие процессов
                bool anyProcessClosed = false;

                // Закрываем процессы TabTip
                var tabTipProcesses = Process.GetProcessesByName(TOUCH_KEYBOARD_PROCESS_NAME);
                if (tabTipProcesses.Length > 0)
                {
                    anyProcessClosed = true;
                    await CloseProcesses(tabTipProcesses, "TabTip");
                }

                // Закрываем процессы OSK
                var oskProcesses = Process.GetProcessesByName(FALLBACK_OSK_PROCESS_NAME);
                if (oskProcesses.Length > 0)
                {
                    anyProcessClosed = true;
                    await CloseProcesses(oskProcesses, "OSK");
                }

                if (!anyProcessClosed)
                {
                    _logger.LogInformation("Виртуальная клавиатура не запущена");
                    return true;
                }

                // Ждем немного и проверяем, что все процессы закрыты
                await Task.Delay(500);
                
                if (!IsVirtualKeyboardRunning())
                {
                    _logger.LogInformation("Виртуальная клавиатура успешно закрыта");
                    OnStateChanged(false, "Виртуальная клавиатура закрыта");
                    return true;
                }

                _logger.LogWarning("Не удалось полностью закрыть виртуальную клавиатуру");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при закрытии виртуальной клавиатуры");
                return false;
            }
        }

        private async Task<bool> HideKeyboardWindow()
        {
            try
            {
                // Скрываем сенсорную клавиатуру
                var touchKeyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (touchKeyboardHandle != IntPtr.Zero)
                {
                    ShowWindow(touchKeyboardHandle, SW_HIDE);
                    await Task.Delay(300);
                    return true;
                }

                // Скрываем OSK
                var oskHandle = FindWindow(OSK_CLASS_NAME, null);
                if (oskHandle != IntPtr.Zero)
                {
                    ShowWindow(oskHandle, SW_HIDE);
                    await Task.Delay(300);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка скрытия окна клавиатуры");
                return false;
            }
        }

        private async Task CloseProcesses(Process[] processes, string processType)
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        // Пытаемся корректно закрыть процесс
                        process.CloseMainWindow();
                        
                        // Ждем немного для корректного закрытия
                        if (!process.WaitForExit(3000))
                        {
                            // Если процесс не закрылся за 3 секунды, принудительно завершаем
                            process.Kill();
                        }
                        
                        _logger.LogInformation("Процесс {ProcessType} (ID: {ProcessId}) успешно закрыт", processType, process.Id);
                    }
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при закрытии процесса {ProcessType} {ProcessId}", processType, process.Id);
                }
            }
        }

        /// <summary>
        /// Проверить, запущена ли виртуальная клавиатура
        /// </summary>
        public bool IsVirtualKeyboardRunning()
        {
            try
            {
                return IsTabTipRunning() || IsOSKRunning();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке состояния виртуальной клавиатуры");
                return false;
            }
        }

        private bool IsTabTipRunning()
        {
            try
            {
                var tabTipProcesses = Process.GetProcessesByName(TOUCH_KEYBOARD_PROCESS_NAME);
                var isRunning = tabTipProcesses.Any(p => !p.HasExited);
                
                // Освобождаем ресурсы процессов
                foreach (var process in tabTipProcesses)
                {
                    process.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке состояния TabTip");
                return false;
            }
        }

        private bool IsOSKRunning()
        {
            try
            {
                var oskProcesses = Process.GetProcessesByName(FALLBACK_OSK_PROCESS_NAME);
                var isRunning = oskProcesses.Any(p => !p.HasExited);
                
                // Освобождаем ресурсы процессов
                foreach (var process in oskProcesses)
                {
                    process.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке состояния OSK");
                return false;
            }
        }

        /// <summary>
        /// Переключить состояние виртуальной клавиатуры
        /// </summary>
        public async Task<bool> ToggleVirtualKeyboardAsync()
        {
            try
            {
                if (IsVirtualKeyboardRunning())
                {
                    return await HideVirtualKeyboardAsync();
                }
                else
                {
                    return await ShowVirtualKeyboardAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переключении состояния виртуальной клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Принудительно позиционировать клавиатуру в видимой области экрана
        /// </summary>
        public async Task<bool> RepositionKeyboardAsync()
        {
            try
            {
                var touchKeyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (touchKeyboardHandle != IntPtr.Zero)
                {
                    _logger.LogInformation("Принудительное позиционирование сенсорной клавиатуры");
                    
                    // Получаем размеры экрана
                    var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                    var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                    
                    // Позиционируем внизу экрана по центру
                    int keyboardWidth = 800;  // Примерная ширина клавиатуры
                    int keyboardHeight = 300; // Примерная высота клавиатуры
                    int x = ((int)screenWidth - keyboardWidth) / 2;
                    int y = (int)screenHeight - keyboardHeight - 50; // Отступ от низа
                    
                    _logger.LogDebug("Позиционирование клавиатуры: x={X}, y={Y}, width={Width}, height={Height}", 
                                   x, y, keyboardWidth, keyboardHeight);
                    
                    // Показываем и позиционируем окно БЕЗ перехвата фокуса
                    ShowWindow(touchKeyboardHandle, SW_SHOWNOACTIVATE);
                    SetWindowPos(touchKeyboardHandle, HWND_NOTOPMOST, x, y, keyboardWidth, keyboardHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
                    await Task.Delay(300);
                    
                    return IsWindowVisible(touchKeyboardHandle);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка позиционирования клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Проверить доступность виртуальной клавиатуры в системе
        /// </summary>
        public bool IsVirtualKeyboardAvailable()
        {
            try
            {
                // Проверяем доступность сенсорной клавиатуры или классической экранной клавиатуры
                bool tabTipExists = File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH);
                bool oskExists = File.Exists(FALLBACK_OSK_EXECUTABLE_PATH);
                
                _logger.LogDebug("TabTip доступен: {TabTipExists}, OSK доступен: {OSKExists}", tabTipExists, oskExists);
                
                return tabTipExists || oskExists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке доступности виртуальной клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Диагностика состояния виртуальной клавиатуры для отладки
        /// </summary>
        public async Task<string> DiagnoseVirtualKeyboardAsync()
        {
            var diagnosis = new System.Text.StringBuilder();
            
            try
            {
                diagnosis.AppendLine("=== ДИАГНОСТИКА ВИРТУАЛЬНОЙ КЛАВИАТУРЫ ===");
                
                // Проверяем файлы
                diagnosis.AppendLine($"TabTip.exe существует: {File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH)}");
                diagnosis.AppendLine($"Путь TabTip: {TOUCH_KEYBOARD_EXECUTABLE_PATH}");
                diagnosis.AppendLine($"OSK.exe существует: {File.Exists(FALLBACK_OSK_EXECUTABLE_PATH)}");
                diagnosis.AppendLine($"Путь OSK: {FALLBACK_OSK_EXECUTABLE_PATH}");
                
                // Проверяем процессы
                var tabTipProcesses = Process.GetProcessesByName(TOUCH_KEYBOARD_PROCESS_NAME);
                diagnosis.AppendLine($"Процессов TabTip запущено: {tabTipProcesses.Length}");
                foreach (var p in tabTipProcesses)
                {
                    diagnosis.AppendLine($"  - PID: {p.Id}, HasExited: {p.HasExited}");
                    p.Dispose();
                }
                
                var oskProcesses = Process.GetProcessesByName(FALLBACK_OSK_PROCESS_NAME);
                diagnosis.AppendLine($"Процессов OSK запущено: {oskProcesses.Length}");
                foreach (var p in oskProcesses)
                {
                    diagnosis.AppendLine($"  - PID: {p.Id}, HasExited: {p.HasExited}");
                    p.Dispose();
                }
                
                // Проверяем окна
                var touchKeyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                diagnosis.AppendLine($"Окно сенсорной клавиатуры найдено: {touchKeyboardHandle != IntPtr.Zero}");
                if (touchKeyboardHandle != IntPtr.Zero)
                {
                    diagnosis.AppendLine($"  - Handle: {touchKeyboardHandle}");
                    bool isVisible = IsWindowVisible(touchKeyboardHandle);
                    diagnosis.AppendLine($"  - Видимо: {isVisible}");
                    
                    if (GetWindowRect(touchKeyboardHandle, out RECT rect))
                    {
                        diagnosis.AppendLine($"  - Позиция: Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");
                        diagnosis.AppendLine($"  - Размер: {rect.Right - rect.Left}x{rect.Bottom - rect.Top}");
                        
                        // Проверяем, находится ли окно в видимой области экрана
                        bool isOnScreen = rect.Left < 3000 && rect.Top < 3000 && rect.Right > -1000 && rect.Bottom > -1000;
                        diagnosis.AppendLine($"  - В пределах экрана: {isOnScreen}");
                    }
                }
                
                var oskHandle = FindWindow(OSK_CLASS_NAME, null);
                diagnosis.AppendLine($"Окно OSK найдено: {oskHandle != IntPtr.Zero}");
                if (oskHandle != IntPtr.Zero)
                {
                    diagnosis.AppendLine($"  - Handle: {oskHandle}");
                    bool isVisible = IsWindowVisible(oskHandle);
                    diagnosis.AppendLine($"  - Видимо: {isVisible}");
                    
                    if (GetWindowRect(oskHandle, out RECT rect))
                    {
                        diagnosis.AppendLine($"  - Позиция: Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");
                    }
                }
                
                // Проверяем реестр
                try
                {
                    var regProcess = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = "query \"HKCU\\Software\\Microsoft\\TabletTip\\1.7\" /v EnableDesktopModeAutoInvoke",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    var process = Process.Start(regProcess);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();
                        
                        diagnosis.AppendLine($"Реестр EnableDesktopModeAutoInvoke:");
                        diagnosis.AppendLine($"  - Exit Code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(output))
                        {
                            diagnosis.AppendLine($"  - Output: {output.Trim()}");
                        }
                        if (!string.IsNullOrEmpty(error))
                        {
                            diagnosis.AppendLine($"  - Error: {error.Trim()}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnosis.AppendLine($"Ошибка проверки реестра: {ex.Message}");
                }
                
                diagnosis.AppendLine("=== КОНЕЦ ДИАГНОСТИКИ ===");
            }
            catch (Exception ex)
            {
                diagnosis.AppendLine($"ОШИБКА ДИАГНОСТИКИ: {ex.Message}");
            }
            
            var result = diagnosis.ToString();
            _logger.LogInformation("Диагностика виртуальной клавиатуры:\n{Diagnosis}", result);
            return result;
        }

        /// <summary>
        /// Вызвать событие изменения состояния
        /// </summary>
        private void OnStateChanged(bool isVisible, string? message = null)
        {
            try
            {
                StateChanged?.Invoke(this, new VirtualKeyboardStateChangedEventArgs
                {
                    IsVisible = isVisible,
                    Message = message,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при вызове события изменения состояния виртуальной клавиатуры");
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Специальный сервис для управления сенсорной клавиатурой в Windows 10
    /// Использует COM интерфейс ITipInvocation для правильной работы TabTip.exe
    /// </summary>
    public class Windows10TouchKeyboardService : IVirtualKeyboardService
    {
        private readonly ILogger<Windows10TouchKeyboardService> _logger;
        private ITipInvocation? _tipInvocation;

        public event EventHandler<VirtualKeyboardStateChangedEventArgs>? StateChanged;

        #region COM Interface для ITipInvocation

        [ComImport]
        [Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITipInvocation
        {
            void Toggle(IntPtr hwnd);
        }

        [ComImport]
        [Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
        class UIHostNoLaunch
        {
        }

        #endregion

        #region Windows API

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Константы
        private const string TOUCH_KEYBOARD_CLASS_NAME = "IPTip_Main_Window";
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_HIDE = 0;
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        #endregion

        public Windows10TouchKeyboardService(ILogger<Windows10TouchKeyboardService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeCOMInterface();
        }

        private void InitializeCOMInterface()
        {
            try
        {
                _logger.LogInformation("Инициализация COM интерфейса ITipInvocation для Windows 10");
                _tipInvocation = (ITipInvocation)new UIHostNoLaunch();
                _logger.LogInformation("COM интерфейс ITipInvocation успешно инициализирован");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации COM интерфейса ITipInvocation");
                _tipInvocation = null;
            }
        }

        /// <summary>
        /// Показать виртуальную клавиатуру (Windows 10 специфичная реализация)
        /// </summary>
        public async Task<bool> ShowVirtualKeyboardAsync()
        {
            try
            {
                _logger.LogInformation("Показ виртуальной клавиатуры через Windows 10 ITipInvocation");

                // Метод 1: Используем COM интерфейс ITipInvocation (предпочтительный для Windows 10)
                if (_tipInvocation != null)
                {
                    try
                    {
                        // Получаем handle активного окна
                        var activeWindow = GetActiveWindowHandle();
                        _tipInvocation.Toggle(activeWindow);
                        
                        await Task.Delay(500); // Даем время TabTip запуститься
                        
                        // Проверяем результат и позиционируем
                        if (await VerifyAndPositionKeyboard())
                        {
                            _logger.LogInformation("Клавиатура успешно показана через ITipInvocation");
                            OnStateChanged(true, "Виртуальная клавиатура показана через COM интерфейс");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка при использовании ITipInvocation");
                    }
                }

                // Метод 2: Fallback - прямой запуск TabTip.exe
                if (await FallbackShowKeyboard())
                {
                    _logger.LogInformation("Клавиатура показана через fallback метод");
                    OnStateChanged(true, "Виртуальная клавиатура показана");
                    return true;
                }

                _logger.LogWarning("Не удалось показать виртуальную клавиатуру");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при показе виртуальной клавиатуры");
                return false;
            }
        }

        private IntPtr GetActiveWindowHandle()
        {
            try
            {
                // Попытка получить handle главного окна приложения
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                    return windowInteropHelper.Handle;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось получить handle главного окна");
            }

            return IntPtr.Zero;
        }

        private async Task<bool> VerifyAndPositionKeyboard()
        {
            // Ждем появления окна клавиатуры
            for (int i = 0; i < 10; i++) // 5 секунд ожидания максимум
            {
                var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (keyboardHandle != IntPtr.Zero && IsWindowVisible(keyboardHandle))
                {
                    _logger.LogDebug("Окно клавиатуры найдено и видимо");
                    
                    // Позиционируем клавиатуру
                    await PositionKeyboard(keyboardHandle);
                    return true;
                }
                await Task.Delay(500);
            }

            return false;
        }

        private async Task PositionKeyboard(IntPtr keyboardHandle)
        {
            try
            {
                // Получаем размеры экрана
                var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                
                // Позиционируем внизу экрана по центру
                int keyboardWidth = 800;
                int keyboardHeight = 300;
                int x = ((int)screenWidth - keyboardWidth) / 2;
                int y = (int)screenHeight - keyboardHeight - 50;
                
                _logger.LogDebug("Позиционирование клавиатуры: x={X}, y={Y}", x, y);
                
                // Показываем и позиционируем БЕЗ активации
                ShowWindow(keyboardHandle, SW_SHOWNOACTIVATE);
                SetWindowPos(keyboardHandle, HWND_NOTOPMOST, x, y, keyboardWidth, keyboardHeight, 
                    SWP_SHOWWINDOW | SWP_NOACTIVATE);
                
                await Task.Delay(200);
                _logger.LogInformation("Клавиатура позиционирована успешно");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка позиционирования клавиатуры");
            }
        }

        private async Task<bool> FallbackShowKeyboard()
        {
            try
            {
                _logger.LogInformation("Запуск TabTip.exe через fallback метод");
                
                var tabTipPath = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";
                if (!System.IO.File.Exists(tabTipPath))
                {
                    _logger.LogWarning("TabTip.exe не найден по пути: {Path}", tabTipPath);
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = tabTipPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
                await Task.Delay(1000);

                return await VerifyAndPositionKeyboard();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка fallback метода показа клавиатуры");
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
                _logger.LogInformation("Скрытие виртуальной клавиатуры");

                var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (keyboardHandle != IntPtr.Zero)
                {
                    ShowWindow(keyboardHandle, SW_HIDE);
                    await Task.Delay(200);
                    
                    bool isHidden = !IsWindowVisible(keyboardHandle);
                    if (isHidden)
                    {
                        OnStateChanged(false, "Виртуальная клавиатура скрыта");
                        return true;
                    }
                }

                // Попытка завершения процесса TabTip
                var tabTipProcesses = Process.GetProcessesByName("TabTip");
                foreach (var process in tabTipProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось завершить процесс TabTip");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при скрытии виртуальной клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Переключить состояние виртуальной клавиатуры
        /// </summary>
        public async Task<bool> ToggleVirtualKeyboardAsync()
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

        /// <summary>
        /// Принудительно позиционировать клавиатуру
        /// </summary>
        public async Task<bool> RepositionKeyboardAsync()
        {
            try
            {
                var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                if (keyboardHandle != IntPtr.Zero)
                {
                    await PositionKeyboard(keyboardHandle);
                    return IsWindowVisible(keyboardHandle);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка репозиционирования клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Проверить, запущена ли виртуальная клавиатура
        /// </summary>
        public bool IsVirtualKeyboardRunning()
        {
            try
            {
                var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                return keyboardHandle != IntPtr.Zero && IsWindowVisible(keyboardHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка проверки состояния клавиатуры");
                return false;
            }
        }

        /// <summary>
        /// Проверить доступность виртуальной клавиатуры
        /// </summary>
        public bool IsVirtualKeyboardAvailable()
        {
            var tabTipPath = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";
            return System.IO.File.Exists(tabTipPath);
        }

        /// <summary>
        /// Диагностика состояния виртуальной клавиатуры
        /// </summary>
        public async Task<string> DiagnoseVirtualKeyboardAsync()
        {
            var diagnosis = new System.Text.StringBuilder();
            
            try
            {
                diagnosis.AppendLine("=== ДИАГНОСТИКА WINDOWS 10 TOUCH KEYBOARD ===");
                
                // Проверяем COM интерфейс
                diagnosis.AppendLine($"ITipInvocation инициализирован: {_tipInvocation != null}");
                
                // Проверяем TabTip.exe
                var tabTipPath = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";
                diagnosis.AppendLine($"TabTip.exe найден: {System.IO.File.Exists(tabTipPath)}");
                
                // Проверяем процессы
                var tabTipProcesses = Process.GetProcessesByName("TabTip");
                diagnosis.AppendLine($"Процессов TabTip: {tabTipProcesses.Length}");
                
                // Проверяем окно
                var keyboardHandle = FindWindow(TOUCH_KEYBOARD_CLASS_NAME, null);
                diagnosis.AppendLine($"Окно клавиатуры найдено: {keyboardHandle != IntPtr.Zero}");
                if (keyboardHandle != IntPtr.Zero)
                {
                    diagnosis.AppendLine($"Окно видимо: {IsWindowVisible(keyboardHandle)}");
                }
                
                // Проверяем версию Windows
                var osVersion = Environment.OSVersion;
                diagnosis.AppendLine($"Версия ОС: {osVersion.VersionString}");
                
                diagnosis.AppendLine("=== КОНЕЦ ДИАГНОСТИКИ ===");
                
                foreach (var process in tabTipProcesses)
                {
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                diagnosis.AppendLine($"ОШИБКА ДИАГНОСТИКИ: {ex.Message}");
            }
            
            var result = diagnosis.ToString();
            _logger.LogInformation("Диагностика Windows 10 клавиатуры:\n{Diagnosis}", result);
            return result;
        }

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
                _logger.LogError(ex, "Ошибка при вызове события изменения состояния");
            }
        }
    }
}
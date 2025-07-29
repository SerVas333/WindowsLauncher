using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.UI.Services
{
    /// <summary>
    /// Сервис для регистрации глобальных горячих клавиш
    /// </summary>
    public class GlobalHotKeyService : IDisposable
    {
        private readonly ILogger<GlobalHotKeyService> _logger;
        private IntPtr _windowHandle;
        private bool _disposed = false;

        // Константы для регистрации горячих клавиш
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private const int VK_TAB = 0x09;
        private const int VK_GRAVE = 0xC0; // ` клавиша

        // ID горячих клавиш для Shell режима
        private const int HOTKEY_ALT_TAB = 1;
        private const int HOTKEY_CTRL_ALT_TAB = 2;
        
        // ID горячих клавиш для Normal режима  
        private const int HOTKEY_WIN_GRAVE = 3;
        private const int HOTKEY_WIN_SHIFT_GRAVE = 4;

        public event EventHandler? AltTabPressed;
        public event EventHandler? CtrlAltTabPressed;
        
        private ShellMode _currentMode = ShellMode.Normal;

        public GlobalHotKeyService(ILogger<GlobalHotKeyService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Инициализировать сервис с привязкой к окну
        /// </summary>
        public async Task InitializeAsync(Window window, ShellMode shellMode = ShellMode.Normal)
        {
            await Task.CompletedTask;
            
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            try
            {
                _currentMode = shellMode;
                
                // Получаем handle окна
                var helper = new WindowInteropHelper(window);
                _windowHandle = helper.Handle;

                if (_windowHandle == IntPtr.Zero)
                {
                    _logger.LogError("Failed to get window handle for global hotkeys");
                    return;
                }

                // Подписываемся на оконные сообщения
                var source = HwndSource.FromHwnd(_windowHandle);
                source?.AddHook(WndProc);

                // Регистрируем горячие клавиши
                await RegisterHotKeysAsync();

                _logger.LogInformation("Global hotkey service initialized successfully in {Mode} mode", _currentMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing global hotkey service");
            }
        }

        /// <summary>
        /// Регистрация горячих клавиш
        /// </summary>
        private async Task RegisterHotKeysAsync()
        {
            await Task.CompletedTask;

            try
            {
                if (_currentMode == ShellMode.Shell)
                {
                    // В режиме Shell используем стандартные комбинации
                    await RegisterShellModeHotKeysAsync();
                }
                else
                {
                    // В обычном режиме используем альтернативные комбинации
                    await RegisterNormalModeHotKeysAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering hotkeys for {Mode} mode", _currentMode);
            }
        }

        /// <summary>
        /// Регистрация хоткеев для Shell режима
        /// </summary>
        private async Task RegisterShellModeHotKeysAsync()
        {
            await Task.CompletedTask;

            // Alt+Tab - основной переключатель
            bool altTabRegistered = RegisterHotKey(_windowHandle, HOTKEY_ALT_TAB, MOD_ALT, VK_TAB);
            if (altTabRegistered)
            {
                _logger.LogInformation("Shell mode: Alt+Tab hotkey registered successfully");
            }
            else
            {
                _logger.LogWarning("Shell mode: Failed to register Alt+Tab hotkey - may be already in use");
            }

            // Ctrl+Alt+Tab - обратный переключатель
            bool ctrlAltTabRegistered = RegisterHotKey(_windowHandle, HOTKEY_CTRL_ALT_TAB, MOD_CONTROL | MOD_ALT, VK_TAB);
            if (ctrlAltTabRegistered)
            {
                _logger.LogInformation("Shell mode: Ctrl+Alt+Tab hotkey registered successfully");
            }
            else
            {
                _logger.LogWarning("Shell mode: Failed to register Ctrl+Alt+Tab hotkey - may be already in use");
            }
        }

        /// <summary>
        /// Регистрация хоткеев для обычного режима
        /// </summary>
        private async Task RegisterNormalModeHotKeysAsync()
        {
            await Task.CompletedTask;

            // Win+` - основной переключатель (аналог Alt+Tab)
            bool winGraveRegistered = RegisterHotKey(_windowHandle, HOTKEY_WIN_GRAVE, MOD_WIN, VK_GRAVE);
            if (winGraveRegistered)
            {
                _logger.LogInformation("Normal mode: Win+` hotkey registered successfully");
            }
            else
            {
                _logger.LogWarning("Normal mode: Failed to register Win+` hotkey - may be already in use");
            }

            // Win+Shift+` - обратный переключатель
            bool winShiftGraveRegistered = RegisterHotKey(_windowHandle, HOTKEY_WIN_SHIFT_GRAVE, MOD_WIN | MOD_SHIFT, VK_GRAVE);
            if (winShiftGraveRegistered)
            {
                _logger.LogInformation("Normal mode: Win+Shift+` hotkey registered successfully");
            }
            else
            {
                _logger.LogWarning("Normal mode: Failed to register Win+Shift+` hotkey - may be already in use");
            }
        }

        /// <summary>
        /// Отмена регистрации горячих клавиш
        /// </summary>
        private async Task UnregisterHotKeysAsync()
        {
            await Task.CompletedTask;

            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    // Отменяем регистрацию всех возможных хоткеев
                    UnregisterHotKey(_windowHandle, HOTKEY_ALT_TAB);
                    UnregisterHotKey(_windowHandle, HOTKEY_CTRL_ALT_TAB);
                    UnregisterHotKey(_windowHandle, HOTKEY_WIN_GRAVE);
                    UnregisterHotKey(_windowHandle, HOTKEY_WIN_SHIFT_GRAVE);
                    
                    _logger.LogInformation("All global hotkeys unregistered");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering hotkeys");
            }
        }

        /// <summary>
        /// Обработчик оконных сообщений
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                
                switch (hotkeyId)
                {
                    // Shell режим
                    case HOTKEY_ALT_TAB:
                        _logger.LogDebug("Alt+Tab hotkey triggered (Shell mode)");
                        AltTabPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;

                    case HOTKEY_CTRL_ALT_TAB:
                        _logger.LogDebug("Ctrl+Alt+Tab hotkey triggered (Shell mode)");
                        CtrlAltTabPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;

                    // Normal режим
                    case HOTKEY_WIN_GRAVE:
                        _logger.LogDebug("Win+` hotkey triggered (Normal mode)");
                        AltTabPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;

                    case HOTKEY_WIN_SHIFT_GRAVE:
                        _logger.LogDebug("Win+Shift+` hotkey triggered (Normal mode)");
                        CtrlAltTabPressed?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        #region Windows API

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterHotKeysAsync().Wait();
                _disposed = true;
            }
        }

        #endregion
    }
}
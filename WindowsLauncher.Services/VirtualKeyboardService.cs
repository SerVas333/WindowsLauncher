using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                // Проверяем, не запущена ли уже виртуальная клавиатура
                if (IsVirtualKeyboardRunning())
                {
                    _logger.LogInformation("Виртуальная клавиатура уже запущена");
                    return true;
                }

                // Пробуем запустить сенсорную клавиатуру (TabTip.exe)
                bool success = await TryStartTouchKeyboard();
                
                if (!success)
                {
                    // Если не удалось, пробуем запустить классическую экранную клавиатуру (osk.exe)
                    _logger.LogInformation("Сенсорная клавиатура недоступна, пробуем классическую экранную клавиатуру");
                    success = await TryStartOSK();
                }

                if (success)
                {
                    _logger.LogInformation("Виртуальная клавиатура успешно запущена");
                    OnStateChanged(true, "Виртуальная клавиатура запущена");
                }
                else
                {
                    _logger.LogError("Не удалось запустить ни одну из виртуальных клавиатур");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске виртуальной клавиатуры");
                return false;
            }
        }

        private async Task<bool> TryStartTouchKeyboard()
        {
            try
            {
                if (!File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH))
                {
                    _logger.LogInformation("TabTip.exe не найден по пути {Path}", TOUCH_KEYBOARD_EXECUTABLE_PATH);
                    return false;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = TOUCH_KEYBOARD_EXECUTABLE_PATH,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var process = Process.Start(processStartInfo);
                
                if (process != null)
                {
                    // Ждем немного, чтобы процесс полностью запустился
                    await Task.Delay(1000);
                    
                    // Проверяем, что процесс действительно запустился
                    if (IsTabTipRunning())
                    {
                        _logger.LogInformation("Сенсорная клавиатура TabTip успешно запущена");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при запуске сенсорной клавиатуры TabTip");
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
        /// Проверить доступность виртуальной клавиатуры в системе
        /// </summary>
        public bool IsVirtualKeyboardAvailable()
        {
            try
            {
                // Проверяем доступность сенсорной клавиатуры или классической экранной клавиатуры
                return File.Exists(TOUCH_KEYBOARD_EXECUTABLE_PATH) || File.Exists(FALLBACK_OSK_EXECUTABLE_PATH);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке доступности виртуальной клавиатуры");
                return false;
            }
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
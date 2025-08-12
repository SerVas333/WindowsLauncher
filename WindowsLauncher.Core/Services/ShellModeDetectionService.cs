using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Services
{
    /// <summary>
    /// Сервис для определения режима работы приложения (Shell vs Normal)
    /// </summary>
    public class ShellModeDetectionService
    {
        private readonly ILogger<ShellModeDetectionService> _logger;

        public ShellModeDetectionService(ILogger<ShellModeDetectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Определить текущий режим работы приложения
        /// </summary>
        public Task<ShellMode> DetectShellModeAsync()
        {
            try
            {
                // Проверяем несколько индикаторов Shell режима
                
                // 1. Проверяем реестр - настроен ли WindowsLauncher как Shell
                if (IsRegisteredAsShell())
                {
                    _logger.LogInformation("Detected Shell mode: Application is registered as Windows Shell");
                    return Task.FromResult(ShellMode.Shell);
                }

                // 2. Проверяем переменную окружения (можно задать вручную)
                var shellModeEnv = Environment.GetEnvironmentVariable("WINDOWSLAUNCHER_SHELL_MODE");
                if (!string.IsNullOrEmpty(shellModeEnv) && 
                    Enum.TryParse<ShellMode>(shellModeEnv, true, out var envMode))
                {
                    _logger.LogInformation("Detected Shell mode from environment variable: {Mode}", envMode);
                    return Task.FromResult(envMode);
                }

                // 3. Проверяем аргументы командной строки
                var args = Environment.GetCommandLineArgs();
                foreach (var arg in args)
                {
                    if (arg.Equals("--shell", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("/shell", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Detected Shell mode from command line argument");
                        return Task.FromResult(ShellMode.Shell);
                    }
                }

                // 4. Проверяем наличие explorer.exe процесса (если нет - возможно Shell режим)
                if (!IsExplorerRunning())
                {
                    _logger.LogInformation("Explorer.exe not detected - assuming Shell mode");
                    return Task.FromResult(ShellMode.Shell);
                }

                // По умолчанию - обычный режим
                _logger.LogInformation("Detected Normal mode (default)");
                return Task.FromResult(ShellMode.Normal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting shell mode, defaulting to Normal");
                return Task.FromResult(ShellMode.Normal);
            }
        }

        /// <summary>
        /// Проверить, зарегистрирован ли WindowsLauncher как Shell в реестре
        /// </summary>
        private bool IsRegisteredAsShell()
        {

            try
            {
                // Проверяем HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");
                var shellValue = key?.GetValue("Shell")?.ToString();
                
                if (!string.IsNullOrEmpty(shellValue))
                {
                    var currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentExePath))
                    {
                        var currentExeName = Path.GetFileName(currentExePath);
                        bool isRegistered = shellValue.Contains(currentExeName, StringComparison.OrdinalIgnoreCase) ||
                                          shellValue.Contains("WindowsLauncher", StringComparison.OrdinalIgnoreCase);
                        
                        _logger.LogDebug("Registry Shell value: {ShellValue}, Current exe: {ExeName}, Registered: {IsRegistered}", 
                            shellValue, currentExeName, isRegistered);
                        
                        return isRegistered;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to check registry for Shell registration");
                return false;
            }
        }

        /// <summary>
        /// Проверить, запущен ли процесс explorer.exe
        /// </summary>
        private bool IsExplorerRunning()
        {

            try
            {
                var explorerProcesses = System.Diagnostics.Process.GetProcessesByName("explorer");
                bool isRunning = explorerProcesses.Length > 0;
                
                _logger.LogDebug("Explorer.exe processes found: {Count}", explorerProcesses.Length);
                
                // Освобождаем ресурсы
                foreach (var process in explorerProcesses)
                {
                    process.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to check for explorer.exe processes");
                return true; // По умолчанию считаем что explorer запущен
            }
        }

        /// <summary>
        /// Получить описание текущего режима для отображения пользователю
        /// </summary>
        public string GetModeDescription(ShellMode mode)
        {
            return mode switch
            {
                ShellMode.Shell => "Режим Shell (замена проводника Windows)",
                ShellMode.Normal => "Обычный режим (приложение Windows)",
                _ => "Неизвестный режим"
            };
        }

        /// <summary>
        /// Получить описание доступных хоткеев для текущего режима
        /// </summary>
        public string GetHotKeysDescription(ShellMode mode)
        {
            return mode switch
            {
                ShellMode.Shell => "Alt+Tab - переключение вперед, Ctrl+Alt+Tab - назад",
                ShellMode.Normal => "Win+` - переключение вперед, Win+Shift+` - назад",
                _ => "Хоткеи недоступны"
            };
        }
    }
}
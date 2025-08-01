using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;

namespace WindowsLauncher.Services.Lifecycle.Monitoring
{
    /// <summary>
    /// Монитор процессов для безопасной работы с Process объектами
    /// Предоставляет надежные методы получения информации о процессах и управления ими
    /// </summary>
    public class ProcessMonitor : IProcessMonitor, IDisposable
    {
        private readonly ILogger<ProcessMonitor> _logger;
        private bool _disposed;
        
        // События
        public event EventHandler<ProcessExitedEventArgs>? ProcessExited;
        public event EventHandler<ProcessNotRespondingEventArgs>? ProcessNotResponding;
        public event EventHandler<ProcessMemoryChangedEventArgs>? ProcessMemoryChanged;
        
        public ProcessMonitor(ILogger<ProcessMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region Проверка состояния процесса
        
        public async Task<bool> IsProcessAliveAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                // Process with specified ID does not exist
                return false;
            }
            catch (InvalidOperationException)
            {
                // Process has exited
                return false;
            }
            catch (Win32Exception ex)
            {
                _logger.LogTrace("Win32Exception checking process {ProcessId}: {Message}", processId, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking if process {ProcessId} is alive", processId);
                return false;
            }
        }
        
        public async Task<bool> IsProcessRespondingAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return false;
                
                return process.Responding;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception ex)
            {
                _logger.LogTrace("Win32Exception checking process {ProcessId} responding: {Message}", processId, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if process {ProcessId} is responding", processId);
                return false;
            }
        }
        
        public async Task<ProcessInfo?> GetProcessInfoAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                
                var processInfo = new ProcessInfo
                {
                    ProcessId = processId,
                    CollectedAt = DateTime.Now,
                    Source = "ProcessMonitor"
                };
                
                // Безопасно собираем информацию с проверкой HasExited
                try
                {
                    if (process.HasExited)
                    {
                        processInfo.IsAlive = false;
                        processInfo.HasExited = true;
                        
                        // Пытаемся получить информацию о завершении
                        try
                        {
                            processInfo.ExitCode = process.ExitCode;
                            processInfo.ExitTime = process.ExitTime;
                        }
                        catch (Exception ex)
                        {
                            processInfo.CollectionErrors.Add($"Error getting exit info: {ex.Message}");
                        }
                        
                        return processInfo;
                    }
                    
                    // Процесс жив, собираем полную информацию
                    processInfo.IsAlive = true;
                    processInfo.HasExited = false;
                    
                    // Основная информация
                    SafeSetProcessProperty(() => processInfo.ProcessName = process.ProcessName, 
                        processInfo.CollectionErrors, "ProcessName");
                    SafeSetProcessProperty(() => processInfo.IsResponding = process.Responding, 
                        processInfo.CollectionErrors, "Responding");
                    
                    // Информация о времени
                    SafeSetProcessProperty(() => processInfo.StartTime = process.StartTime, 
                        processInfo.CollectionErrors, "StartTime");
                    SafeSetProcessProperty(() => processInfo.TotalProcessorTime = process.TotalProcessorTime, 
                        processInfo.CollectionErrors, "TotalProcessorTime");
                    
                    // Информация о памяти
                    SafeSetProcessProperty(() => processInfo.WorkingSetMemory = process.WorkingSet64, 
                        processInfo.CollectionErrors, "WorkingSet64");
                    SafeSetProcessProperty(() => processInfo.PeakWorkingSetMemory = process.PeakWorkingSet64, 
                        processInfo.CollectionErrors, "PeakWorkingSet64");
                    SafeSetProcessProperty(() => processInfo.VirtualMemory = process.VirtualMemorySize64, 
                        processInfo.CollectionErrors, "VirtualMemorySize64");
                    SafeSetProcessProperty(() => processInfo.PrivateMemory = process.PrivateMemorySize64, 
                        processInfo.CollectionErrors, "PrivateMemorySize64");
                    
                    // Информация о потоках и дескрипторах
                    SafeSetProcessProperty(() => processInfo.ThreadCount = process.Threads.Count, 
                        processInfo.CollectionErrors, "Threads.Count");
                    SafeSetProcessProperty(() => processInfo.HandleCount = process.HandleCount, 
                        processInfo.CollectionErrors, "HandleCount");
                    
                    // Информация об окнах
                    SafeSetProcessProperty(() => processInfo.MainWindowHandle = process.MainWindowHandle, 
                        processInfo.CollectionErrors, "MainWindowHandle");
                    SafeSetProcessProperty(() => processInfo.MainWindowTitle = process.MainWindowTitle ?? string.Empty, 
                        processInfo.CollectionErrors, "MainWindowTitle");
                    
                    // Приоритет
                    SafeSetProcessProperty(() => processInfo.BasePriority = process.BasePriority, 
                        processInfo.CollectionErrors, "BasePriority");
                    
                    // Дополнительная информация через WMI (опционально)
                    await EnrichProcessInfoWithWMIAsync(processInfo);
                }
                catch (InvalidOperationException)
                {
                    // Процесс завершился во время сбора информации
                    processInfo.IsAlive = false;
                    processInfo.HasExited = true;
                    processInfo.CollectionErrors.Add("Process exited during information collection");
                }
                
                return processInfo;
            }
            catch (ArgumentException)
            {
                return ProcessInfo.CreateWithError(processId, "Process not found");
            }
            catch (Win32Exception ex)
            {
                return ProcessInfo.CreateWithError(processId, $"Win32Exception: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting process info for {ProcessId}", processId);
                return ProcessInfo.CreateWithError(processId, $"Unexpected error: {ex.Message}");
            }
        }
        
        private void SafeSetProcessProperty(Action setter, List<string> errors, string propertyName)
        {
            try
            {
                setter();
            }
            catch (Win32Exception ex)
            {
                errors.Add($"{propertyName}: Win32Exception - {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"{propertyName}: InvalidOperationException - {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"{propertyName}: {ex.GetType().Name} - {ex.Message}");
            }
        }
        
        private async Task EnrichProcessInfoWithWMIAsync(ProcessInfo processInfo)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_Process WHERE ProcessId = {processInfo.ProcessId}");
                    
                    using var collection = searcher.Get();
                    var processObject = collection.Cast<ManagementObject>().FirstOrDefault();
                    
                    if (processObject != null)
                    {
                        try
                        {
                            processInfo.ExecutablePath = processObject["ExecutablePath"]?.ToString() ?? string.Empty;
                            processInfo.CommandLine = processObject["CommandLine"]?.ToString() ?? string.Empty;
                            processInfo.WorkingDirectory = processObject["WorkingSetSize"]?.ToString() ?? string.Empty;
                            
                            var parentProcessId = processObject["ParentProcessId"];
                            if (parentProcessId != null && int.TryParse(parentProcessId.ToString(), out int parentPid))
                            {
                                processInfo.ParentProcessId = parentPid;
                            }
                        }
                        finally
                        {
                            processObject.Dispose();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                processInfo.CollectionErrors.Add($"WMI enrichment failed: {ex.Message}");
            }
        }
        
        public async Task<long> GetProcessMemoryUsageAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return 0;
                
                return process.WorkingSet64;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (Win32Exception)
            {
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage for process {ProcessId}", processId);
                return 0;
            }
        }
        
        public async Task<DateTime?> GetProcessStartTimeAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return null;
                
                return process.StartTime;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (Win32Exception)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting start time for process {ProcessId}", processId);
                return null;
            }
        }
        
        public async Task<string> GetProcessNameAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return string.Empty;
                
                return process.ProcessName;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (Win32Exception)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting name for process {ProcessId}", processId);
                return string.Empty;
            }
        }
        
        #endregion
        
        #region Управление процессами
        
        public async Task<bool> CloseProcessGracefullyAsync(int processId, int timeoutMs = 5000)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                
                if (process.HasExited)
                {
                    _logger.LogDebug("Process {ProcessId} already exited", processId);
                    return true;
                }
                
                _logger.LogDebug("Attempting graceful close of process {ProcessId} ({ProcessName})", 
                    processId, process.ProcessName);
                
                // Пытаемся закрыть через CloseMainWindow
                bool closed = process.CloseMainWindow();
                
                if (!closed)
                {
                    _logger.LogDebug("CloseMainWindow returned false for process {ProcessId}", processId);
                    return false;
                }
                
                // Ждем завершения
                bool exited = await Task.Run(() => process.WaitForExit(timeoutMs));
                
                if (exited)
                {
                    _logger.LogInformation("Process {ProcessId} ({ProcessName}) closed gracefully", 
                        processId, process.ProcessName);
                    
                    // Генерируем событие
                    ProcessExited?.Invoke(this, new ProcessExitedEventArgs(
                        processId, process.ProcessName, DateTime.Now, process.ExitCode)
                    {
                        IsExpected = true,
                        AdditionalInfo = "Graceful close"
                    });
                    
                    return true;
                }
                else
                {
                    _logger.LogWarning("Process {ProcessId} did not exit within {Timeout}ms", processId, timeoutMs);
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Process not found - считаем что уже закрыт
                return true;
            }
            catch (InvalidOperationException)
            {
                // Process already exited
                return true;
            }
            catch (Win32Exception ex)
            {
                _logger.LogWarning("Win32Exception closing process {ProcessId} gracefully: {Message}", 
                    processId, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing process {ProcessId} gracefully", processId);
                return false;
            }
        }
        
        public async Task<bool> KillProcessAsync(int processId, int timeoutMs = 3000)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                
                if (process.HasExited)
                {
                    _logger.LogDebug("Process {ProcessId} already exited", processId);
                    return true;
                }
                
                var processName = process.ProcessName;
                _logger.LogWarning("Force killing process {ProcessId} ({ProcessName})", processId, processName);
                
                process.Kill();
                
                // Ждем завершения
                bool exited = await Task.Run(() => process.WaitForExit(timeoutMs));
                
                if (exited)
                {
                    _logger.LogInformation("Process {ProcessId} ({ProcessName}) killed successfully", 
                        processId, processName);
                    
                    // Генерируем событие
                    ProcessExited?.Invoke(this, new ProcessExitedEventArgs(
                        processId, processName, DateTime.Now, process.ExitCode)
                    {
                        IsExpected = false,
                        AdditionalInfo = "Force killed"
                    });
                    
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to kill process {ProcessId} within {Timeout}ms", processId, timeoutMs);
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Process not found - считаем что уже завершен
                return true;
            }
            catch (InvalidOperationException)
            {
                // Process already exited
                return true;
            }
            catch (Win32Exception ex)
            {
                _logger.LogError("Win32Exception killing process {ProcessId}: {Message}", processId, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error killing process {ProcessId}", processId);
                return false;
            }
        }
        
        public async Task<bool> TerminateProcessAsync(int processId, int gracefulTimeoutMs = 5000, int killTimeoutMs = 3000)
        {
            _logger.LogDebug("Attempting to terminate process {ProcessId} (graceful timeout: {GracefulTimeout}ms, kill timeout: {KillTimeout}ms)", 
                processId, gracefulTimeoutMs, killTimeoutMs);
            
            // Сначала пытаемся корректно закрыть
            bool gracefullyClosed = await CloseProcessGracefullyAsync(processId, gracefulTimeoutMs);
            
            if (gracefullyClosed)
            {
                return true;
            }
            
            // Если корректное закрытие не сработало, принудительно завершаем
            _logger.LogWarning("Graceful close failed for process {ProcessId}, attempting force kill", processId);
            return await KillProcessAsync(processId, killTimeoutMs);
        }
        
        #endregion
        
        #region Поиск процессов
        
        public async Task<int[]> FindProcessesByNameAsync(string processName)
        {
            await Task.CompletedTask;
            
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var processIds = new List<int>();
                
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            processIds.Add(process.Id);
                        }
                    }
                    catch (Win32Exception)
                    {
                        // Process не доступен, пропускаем
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                return processIds.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding processes by name '{ProcessName}'", processName);
                return Array.Empty<int>();
            }
        }
        
        public async Task<int[]> FindProcessesByPartialNameAsync(string partialName, bool ignoreCase = true)
        {
            await Task.CompletedTask;
            
            try
            {
                var allProcesses = Process.GetProcesses();
                var matchingProcessIds = new List<int>();
                var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                
                foreach (var process in allProcesses)
                {
                    try
                    {
                        if (!process.HasExited && 
                            process.ProcessName.Contains(partialName, comparison))
                        {
                            matchingProcessIds.Add(process.Id);
                        }
                    }
                    catch (Win32Exception)
                    {
                        // Process не доступен, пропускаем
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                return matchingProcessIds.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding processes by partial name '{PartialName}'", partialName);
                return Array.Empty<int>();
            }
        }
        
        public async Task<int[]> FindChildProcessesAsync(int parentProcessId)
        {
            await Task.CompletedTask;
            
            try
            {
                var childProcessIds = new List<int>();
                
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentProcessId}");
                
                using var collection = searcher.Get();
                
                foreach (ManagementObject process in collection)
                {
                    try
                    {
                        var processId = process["ProcessId"];
                        if (processId != null && int.TryParse(processId.ToString(), out int pid))
                        {
                            childProcessIds.Add(pid);
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                return childProcessIds.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding child processes for parent {ParentProcessId}", parentProcessId);
                return Array.Empty<int>();
            }
        }
        
        public async Task<int[]> FindProcessesWithWindowsAsync(string? processName = null)
        {
            await Task.CompletedTask;
            
            try
            {
                var allProcesses = Process.GetProcesses();
                var processesWithWindows = new List<int>();
                
                foreach (var process in allProcesses)
                {
                    try
                    {
                        if (process.HasExited)
                            continue;
                        
                        // Фильтр по имени процесса если указан
                        if (!string.IsNullOrEmpty(processName) && 
                            !process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        // Проверяем наличие главного окна
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            processesWithWindows.Add(process.Id);
                        }
                    }
                    catch (Win32Exception)
                    {
                        // Process не доступен, пропускаем
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                return processesWithWindows.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding processes with windows (filter: '{ProcessName}')", processName);
                return Array.Empty<int>();
            }
        }
        
        #endregion
        
        #region Безопасная работа с Process объектами
        
        public async Task<Process?> GetProcessSafelyAsync(int processId)
        {
            await Task.CompletedTask;
            
            try
            {
                var process = Process.GetProcessById(processId);
                
                // Проверяем что процесс не завершился сразу после получения
                if (process.HasExited)
                {
                    process.Dispose();
                    return null;
                }
                
                return process;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Win32Exception ex)
            {
                _logger.LogTrace("Win32Exception getting process {ProcessId}: {Message}", processId, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting process {ProcessId} safely", processId);
                return null;
            }
        }
        
        public async Task<bool> RefreshProcessSafelyAsync(Process process)
        {
            await Task.CompletedTask;
            
            try
            {
                if (process.HasExited)
                    return false;
                
                process.Refresh();
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception ex)
            {
                _logger.LogTrace("Win32Exception refreshing process {ProcessId}: {Message}", 
                    process.Id, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing process {ProcessId}", process.Id);
                return false;
            }
        }
        
        public async Task DisposeProcessSafelyAsync(Process process)
        {
            await Task.CompletedTask;
            
            try
            {
                process?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error disposing process object");
            }
        }

        public async Task CleanupProcessAsync(int processId)
        {
            try
            {
                _logger.LogDebug("Cleaning up process {ProcessId}", processId);
                
                // Сначала проверяем что процесс существует
                bool isAlive = await IsProcessAliveAsync(processId);
                if (!isAlive)
                {
                    _logger.LogDebug("Process {ProcessId} is already terminated", processId);
                    return;
                }

                // Пытаемся корректно завершить процесс
                bool terminated = await TerminateProcessAsync(processId);
                if (terminated)
                {
                    _logger.LogDebug("Process {ProcessId} terminated successfully", processId);
                }
                else
                {
                    _logger.LogWarning("Failed to terminate process {ProcessId}", processId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of process {ProcessId}", processId);
            }
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                // Очищаем события
                ProcessExited = null;
                ProcessNotResponding = null;
                ProcessMemoryChanged = null;
            }
        }
        
        #endregion
    }
}
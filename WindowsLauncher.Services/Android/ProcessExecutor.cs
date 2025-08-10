using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Text;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис для выполнения внешних процессов (ADB, AAPT, PowerShell команды)
    /// </summary>
    public class ProcessExecutor : IProcessExecutor
    {
        private readonly ILogger<ProcessExecutor> _logger;
        private readonly Dictionary<string, string> _cachedPaths;

        public ProcessExecutor(ILogger<ProcessExecutor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cachedPaths = new Dictionary<string, string>();
        }

        public async Task<ProcessResult> ExecuteAsync(
            string fileName, 
            string arguments, 
            int timeoutMs = 30000,
            string? workingDirectory = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Executing command: {FileName} {Arguments}", fileName, arguments);

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                bool started = process.Start();
                if (!started)
                {
                    var errorMessage = $"Failed to start process: {fileName}";
                    _logger.LogError(errorMessage);
                    return new ProcessResult
                    {
                        ExitCode = -1,
                        StandardError = errorMessage,
                        ExecutionTime = stopwatch.Elapsed,
                        StartTime = startTime,
                        Command = fileName,
                        Arguments = arguments
                    };
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool finished = await WaitForExitAsync(process, timeoutMs);
                stopwatch.Stop();

                var result = new ProcessResult
                {
                    ExitCode = finished ? process.ExitCode : -1,
                    StandardOutput = outputBuilder.ToString().Trim(),
                    StandardError = errorBuilder.ToString().Trim(),
                    ExecutionTime = stopwatch.Elapsed,
                    StartTime = startTime,
                    Command = fileName,
                    Arguments = arguments,
                    TimedOut = !finished
                };

                if (!finished)
                {
                    _logger.LogWarning("Process timed out after {TimeoutMs}ms: {FileName} {Arguments}", 
                        timeoutMs, fileName, arguments);
                    
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to kill timed out process");
                    }
                }
                else if (result.ExitCode != 0)
                {
                    _logger.LogWarning("Process exited with code {ExitCode}: {FileName} {Arguments}. Error: {Error}", 
                        result.ExitCode, fileName, arguments, result.StandardError);
                }
                else
                {
                    _logger.LogDebug("Process completed successfully in {ExecutionTime}ms: {FileName}", 
                        stopwatch.ElapsedMilliseconds, fileName);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception executing command: {FileName} {Arguments}", fileName, arguments);
                
                return new ProcessResult
                {
                    ExitCode = -1,
                    StandardError = ex.Message,
                    ExecutionTime = stopwatch.Elapsed,
                    StartTime = startTime,
                    Command = fileName,
                    Arguments = arguments
                };
            }
        }

        public async Task<bool> IsCommandAvailableAsync(string commandName)
        {
            try
            {
                var path = await GetCommandPathAsync(commandName);
                return !string.IsNullOrEmpty(path);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetCommandPathAsync(string commandName)
        {
            // Check cache first
            if (_cachedPaths.TryGetValue(commandName, out var cachedPath))
            {
                return cachedPath;
            }

            try
            {
                // Try using 'where' command on Windows with better encoding handling
                var whereResult = await ExecuteWithEncodingAsync("where", commandName, 5000);
                
                if (whereResult.IsSuccess && !string.IsNullOrEmpty(whereResult.StandardOutput))
                {
                    var path = whereResult.StandardOutput.Split('\n')[0].Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        _cachedPaths[commandName] = path;
                        return path;
                    }
                }

                // Fallback to PowerShell Get-Command with UTF8 output
                var psResult = await ExecutePowerShellWithUtf8Async($"Get-Command {commandName} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source", 5000);
                
                if (psResult.IsSuccess && !string.IsNullOrEmpty(psResult.StandardOutput))
                {
                    var path = psResult.StandardOutput.Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        _cachedPaths[commandName] = path;
                        return path;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get path for command: {CommandName}", commandName);
                return null;
            }
        }

        public async Task<ProcessResult> ExecutePowerShellAsync(string script, int timeoutMs = 30000)
        {
            var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return await ExecuteAsync("powershell", $"-EncodedCommand {encodedCommand}", timeoutMs);
        }

        public async Task<ProcessResult> ExecuteWithRetryAsync(
            string fileName, 
            string arguments, 
            int maxRetries = 3,
            int retryDelayMs = 1000,
            int timeoutMs = 30000)
        {
            ProcessResult lastResult = ProcessResult.Failure(-1, "No attempts made");

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    _logger.LogDebug("Retrying command (attempt {Attempt}/{MaxRetries}): {FileName} {Arguments}", 
                        attempt + 1, maxRetries + 1, fileName, arguments);
                    await Task.Delay(retryDelayMs);
                }

                lastResult = await ExecuteAsync(fileName, arguments, timeoutMs);

                if (lastResult.IsSuccess)
                {
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Command succeeded on attempt {Attempt}: {FileName}", 
                            attempt + 1, fileName);
                    }
                    return lastResult;
                }

                if (attempt < maxRetries)
                {
                    _logger.LogWarning("Command failed (attempt {Attempt}), will retry: {FileName}. Error: {Error}", 
                        attempt + 1, fileName, lastResult.StandardError);
                }
            }

            _logger.LogError("Command failed after {TotalAttempts} attempts: {FileName} {Arguments}. Final error: {Error}", 
                maxRetries + 1, fileName, arguments, lastResult.StandardError);
            
            return lastResult;
        }

        private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                
                while (!process.HasExited)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        return false; // Timeout
                    }
                    
                    await Task.Delay(50, cts.Token);
                }
                
                return true; // Process completed
            }
            catch (OperationCanceledException)
            {
                return false; // Timeout
            }
        }

        /// <summary>
        /// Выполняет команду с автоматическим определением кодировки консоли
        /// </summary>
        private async Task<ProcessResult> ExecuteWithEncodingAsync(
            string fileName, 
            string arguments, 
            int timeoutMs = 30000,
            string? workingDirectory = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Executing command with encoding detection: {FileName} {Arguments}", fileName, arguments);

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    // Попробуем использовать кодировку консоли по умолчанию
                    StandardOutputEncoding = Console.OutputEncoding,
                    StandardErrorEncoding = Console.OutputEncoding
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await WaitForExitAsync(process, timeoutMs);

                if (!completed)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            await process.WaitForExitAsync();
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogWarning(killEx, "Failed to kill timed out process");
                    }
                }

                stopwatch.Stop();

                var result = new ProcessResult
                {
                    ExitCode = completed ? process.ExitCode : -1,
                    StandardOutput = outputBuilder.ToString(),
                    StandardError = errorBuilder.ToString(),
                    ExecutionTime = stopwatch.Elapsed,
                    StartTime = startTime,
                    Command = fileName,
                    Arguments = arguments
                };

                if (!completed)
                {
                    _logger.LogWarning("Command timed out after {TimeoutMs}ms: {FileName} {Arguments}",
                        timeoutMs, fileName, arguments);
                }
                else if (result.ExitCode != 0)
                {
                    _logger.LogWarning("Process exited with code {ExitCode}: {FileName} {Arguments}. Error: {Error}",
                        result.ExitCode, fileName, arguments, result.StandardError);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception executing command with encoding detection: {FileName} {Arguments}", fileName, arguments);

                return new ProcessResult
                {
                    ExitCode = -1,
                    StandardError = ex.Message,
                    ExecutionTime = stopwatch.Elapsed,
                    StartTime = startTime,
                    Command = fileName,
                    Arguments = arguments
                };
            }
        }

        /// <summary>
        /// Выполняет PowerShell команду с UTF-8 выводом
        /// </summary>
        private async Task<ProcessResult> ExecutePowerShellWithUtf8Async(string script, int timeoutMs = 30000)
        {
            // Добавляем установку OutputEncoding в UTF-8 для правильного вывода
            var wrappedScript = $"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {script}";
            var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));
            return await ExecuteWithEncodingAsync("powershell", $"-EncodedCommand {encodedCommand}", timeoutMs);
        }
    }
}
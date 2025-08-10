namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Результат выполнения внешнего процесса
    /// </summary>
    public class ProcessResult
    {
        /// <summary>
        /// Код возврата процесса (0 обычно означает успех)
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Вывод процесса в стандартный поток (stdout)
        /// </summary>
        public string StandardOutput { get; set; } = string.Empty;

        /// <summary>
        /// Вывод процесса в поток ошибок (stderr)
        /// </summary>
        public string StandardError { get; set; } = string.Empty;

        /// <summary>
        /// Время выполнения процесса
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Время начала выполнения процесса
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Команда, которая была выполнена
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Аргументы команды
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Был ли процесс завершен по таймауту
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Проверить, завершился ли процесс успешно
        /// </summary>
        public bool IsSuccess => ExitCode == 0 && !TimedOut;

        /// <summary>
        /// Получить объединенный вывод (stdout + stderr)
        /// </summary>
        public string GetCombinedOutput()
        {
            var output = StandardOutput;
            if (!string.IsNullOrEmpty(StandardError))
            {
                output += Environment.NewLine + StandardError;
            }
            return output;
        }

        /// <summary>
        /// Получить детальную информацию о результате
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = $"Command: {Command} {Arguments}" + Environment.NewLine;
            info += $"Exit Code: {ExitCode}" + Environment.NewLine;
            info += $"Execution Time: {ExecutionTime.TotalMilliseconds:F0}ms" + Environment.NewLine;
            
            if (TimedOut)
            {
                info += "Status: TIMED OUT" + Environment.NewLine;
            }
            else
            {
                info += $"Status: {(IsSuccess ? "SUCCESS" : "FAILED")}" + Environment.NewLine;
            }
            
            if (!string.IsNullOrEmpty(StandardOutput))
            {
                info += $"Standard Output:{Environment.NewLine}{StandardOutput}{Environment.NewLine}";
            }
            
            if (!string.IsNullOrEmpty(StandardError))
            {
                info += $"Standard Error:{Environment.NewLine}{StandardError}{Environment.NewLine}";
            }
            
            return info;
        }

        /// <summary>
        /// Создать результат успешного выполнения
        /// </summary>
        public static ProcessResult Success(string output = "", TimeSpan? executionTime = null)
        {
            return new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = output,
                ExecutionTime = executionTime ?? TimeSpan.Zero,
                StartTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Создать результат неудачного выполнения
        /// </summary>
        public static ProcessResult Failure(int exitCode, string error = "", string output = "")
        {
            return new ProcessResult
            {
                ExitCode = exitCode,
                StandardOutput = output,
                StandardError = error,
                StartTime = DateTime.UtcNow
            };
        }
    }
}
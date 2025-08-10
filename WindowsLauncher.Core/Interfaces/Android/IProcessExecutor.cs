using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Интерфейс для выполнения внешних процессов (ADB, AAPT, PowerShell команды)
    /// </summary>
    public interface IProcessExecutor
    {
        /// <summary>
        /// Выполнить внешнюю команду асинхронно
        /// </summary>
        /// <param name="fileName">Имя исполняемого файла или команды</param>
        /// <param name="arguments">Аргументы команды</param>
        /// <param name="timeoutMs">Таймаут выполнения в миллисекундах (по умолчанию 30 секунд)</param>
        /// <param name="workingDirectory">Рабочая директория для выполнения команды</param>
        /// <returns>Результат выполнения команды</returns>
        Task<ProcessResult> ExecuteAsync(
            string fileName, 
            string arguments, 
            int timeoutMs = 30000,
            string? workingDirectory = null);

        /// <summary>
        /// Проверить, доступна ли команда в системе
        /// </summary>
        /// <param name="commandName">Имя команды для проверки</param>
        /// <returns>True, если команда доступна</returns>
        Task<bool> IsCommandAvailableAsync(string commandName);

        /// <summary>
        /// Получить путь к команде в системе
        /// </summary>
        /// <param name="commandName">Имя команды</param>
        /// <returns>Полный путь к команде или null, если не найдена</returns>
        Task<string?> GetCommandPathAsync(string commandName);

        /// <summary>
        /// Выполнить PowerShell команду
        /// </summary>
        /// <param name="script">PowerShell скрипт для выполнения</param>
        /// <param name="timeoutMs">Таймаут выполнения в миллисекундах</param>
        /// <returns>Результат выполнения PowerShell команды</returns>
        Task<ProcessResult> ExecutePowerShellAsync(string script, int timeoutMs = 30000);

        /// <summary>
        /// Выполнить команду с повторными попытками
        /// </summary>
        /// <param name="fileName">Имя исполняемого файла</param>
        /// <param name="arguments">Аргументы команды</param>
        /// <param name="maxRetries">Максимальное количество повторных попыток</param>
        /// <param name="retryDelayMs">Задержка между попытками в миллисекундах</param>
        /// <param name="timeoutMs">Таймаут каждой попытки</param>
        /// <returns>Результат выполнения команды</returns>
        Task<ProcessResult> ExecuteWithRetryAsync(
            string fileName, 
            string arguments, 
            int maxRetries = 3,
            int retryDelayMs = 1000,
            int timeoutMs = 30000);
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для управления запущенными из лаунчера приложениями
    /// </summary>
    public interface IRunningApplicationsService
    {
        /// <summary>
        /// Событие при запуске нового приложения
        /// </summary>
        event EventHandler<RunningApplicationEventArgs> ApplicationStarted;

        /// <summary>
        /// Событие при завершении приложения
        /// </summary>
        event EventHandler<RunningApplicationEventArgs> ApplicationExited;

        /// <summary>
        /// Событие при изменении статуса приложения
        /// </summary>
        event EventHandler<RunningApplicationEventArgs> ApplicationStatusChanged;

        /// <summary>
        /// Зарегистрировать запущенное приложение
        /// </summary>
        Task RegisterApplicationAsync(Application application, System.Diagnostics.Process process, string launchedBy);

        /// <summary>
        /// Получить все запущенные приложения
        /// </summary>
        Task<IReadOnlyList<RunningApplication>> GetRunningApplicationsAsync();

        /// <summary>
        /// Получить запущенные приложения пользователя
        /// </summary>
        Task<IReadOnlyList<RunningApplication>> GetUserRunningApplicationsAsync(string username);

        /// <summary>
        /// Получить запущенное приложение по ID процесса
        /// </summary>
        Task<RunningApplication?> GetRunningApplicationByProcessIdAsync(int processId);

        /// <summary>
        /// Получить запущенные приложения по ID приложения
        /// </summary>
        Task<IReadOnlyList<RunningApplication>> GetRunningApplicationsByAppIdAsync(int applicationId);

        /// <summary>
        /// Переключиться на приложение (активировать окно)
        /// </summary>
        Task<bool> SwitchToApplicationAsync(int processId);

        /// <summary>
        /// Свернуть приложение
        /// </summary>
        Task<bool> MinimizeApplicationAsync(int processId);

        /// <summary>
        /// Развернуть приложение
        /// </summary>
        Task<bool> RestoreApplicationAsync(int processId);

        /// <summary>
        /// Закрыть приложение корректно
        /// </summary>
        Task<bool> CloseApplicationAsync(int processId);

        /// <summary>
        /// Принудительно завершить приложение
        /// </summary>
        Task<bool> KillApplicationAsync(int processId);

        /// <summary>
        /// Обновить статус всех приложений
        /// </summary>
        Task RefreshApplicationStatusAsync();

        /// <summary>
        /// Получить количество запущенных приложений
        /// </summary>
        Task<int> GetRunningApplicationsCountAsync();

        /// <summary>
        /// Получить общее использование памяти запущенными приложениями
        /// </summary>
        Task<long> GetTotalMemoryUsageAsync();

        /// <summary>
        /// Проверить, запущено ли приложение
        /// </summary>
        Task<bool> IsApplicationRunningAsync(int applicationId);

        /// <summary>
        /// Запустить мониторинг процессов
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Остановить мониторинг процессов
        /// </summary>
        Task StopMonitoringAsync();
    }

    /// <summary>
    /// Аргументы события запущенного приложения
    /// </summary>
    public class RunningApplicationEventArgs : EventArgs
    {
        public RunningApplication Application { get; set; } = null!;
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;

namespace WindowsLauncher.Core.Interfaces.Lifecycle
{
    /// <summary>
    /// Интерфейс для мониторинга и управления процессами приложений
    /// Абстрагирует работу с System.Diagnostics.Process и предоставляет безопасные методы
    /// </summary>
    public interface IProcessMonitor
    {
        #region Проверка состояния процесса
        
        /// <summary>
        /// Проверить, жив ли процесс
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>true если процесс существует и не завершен</returns>
        Task<bool> IsProcessAliveAsync(int processId);
        
        /// <summary>
        /// Проверить, отвечает ли процесс на запросы
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>true если процесс отвечает</returns>
        Task<bool> IsProcessRespondingAsync(int processId);
        
        /// <summary>
        /// Получить подробную информацию о процессе
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Информация о процессе или null если процесс не найден</returns>
        Task<ProcessInfo?> GetProcessInfoAsync(int processId);
        
        /// <summary>
        /// Получить использование памяти процессом
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Использование памяти в байтах или 0 если процесс не найден</returns>
        Task<long> GetProcessMemoryUsageAsync(int processId);
        
        /// <summary>
        /// Получить время запуска процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Время запуска или null если недоступно</returns>
        Task<DateTime?> GetProcessStartTimeAsync(int processId);
        
        /// <summary>
        /// Получить имя процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Имя процесса или пустая строка</returns>
        Task<string> GetProcessNameAsync(int processId);
        
        #endregion

        #region Управление процессами
        
        /// <summary>
        /// Корректно закрыть процесс (через CloseMainWindow)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="timeoutMs">Время ожидания завершения в миллисекундах</param>
        /// <returns>true если процесс завершился корректно</returns>
        Task<bool> CloseProcessGracefullyAsync(int processId, int timeoutMs = 5000);
        
        /// <summary>
        /// Принудительно завершить процесс (Kill)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="timeoutMs">Время ожидания завершения в миллисекундах</param>
        /// <returns>true если процесс завершен</returns>
        Task<bool> KillProcessAsync(int processId, int timeoutMs = 3000);
        
        /// <summary>
        /// Попытаться корректно закрыть процесс, при неудаче - принудительно завершить
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="gracefulTimeoutMs">Время ожидания корректного закрытия</param>
        /// <param name="killTimeoutMs">Время ожидания принудительного завершения</param>
        /// <returns>true если процесс завершен любым способом</returns>
        Task<bool> TerminateProcessAsync(int processId, int gracefulTimeoutMs = 5000, int killTimeoutMs = 3000);
        
        #endregion

        #region Поиск процессов
        
        /// <summary>
        /// Найти процессы по имени
        /// </summary>
        /// <param name="processName">Имя процесса (без .exe)</param>
        /// <returns>Массив ID найденных процессов</returns>
        Task<int[]> FindProcessesByNameAsync(string processName);
        
        /// <summary>
        /// Найти процессы по частичному совпадению имени
        /// </summary>
        /// <param name="partialName">Часть имени процесса</param>
        /// <param name="ignoreCase">Игнорировать регистр</param>
        /// <returns>Массив ID найденных процессов</returns>
        Task<int[]> FindProcessesByPartialNameAsync(string partialName, bool ignoreCase = true);
        
        /// <summary>
        /// Найти дочерние процессы для родительского процесса
        /// </summary>
        /// <param name="parentProcessId">ID родительского процесса</param>
        /// <returns>Массив ID дочерних процессов</returns>
        Task<int[]> FindChildProcessesAsync(int parentProcessId);
        
        /// <summary>
        /// Найти процессы, имеющие окна
        /// </summary>
        /// <param name="processName">Имя процесса (опционально)</param>
        /// <returns>Массив ID процессов с окнами</returns>
        Task<int[]> FindProcessesWithWindowsAsync(string? processName = null);
        
        #endregion

        #region Безопасная работа с Process объектами
        
        /// <summary>
        /// Безопасно получить Process объект по ID
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Process объект или null если процесс недоступен</returns>
        Task<Process?> GetProcessSafelyAsync(int processId);
        
        /// <summary>
        /// Безопасно обновить информацию о процессе
        /// </summary>
        /// <param name="process">Process объект</param>
        /// <returns>true если обновление успешно</returns>
        Task<bool> RefreshProcessSafelyAsync(Process process);
        
        /// <summary>
        /// Безопасно освободить ресурсы Process объекта
        /// </summary>
        /// <param name="process">Process объект</param>
        Task DisposeProcessSafelyAsync(Process process);

        /// <summary>
        /// Выполнить очистку ресурсов процесса (комбинация завершения и освобождения ресурсов)
        /// </summary>
        /// <param name="processId">ID процесса</param>
        Task CleanupProcessAsync(int processId);
        
        #endregion

        #region События мониторинга
        
        /// <summary>
        /// Событие обнаружения завершения процесса
        /// </summary>
        event EventHandler<ProcessExitedEventArgs> ProcessExited;
        
        /// <summary>
        /// Событие обнаружения процесса, который перестал отвечать
        /// </summary>
        event EventHandler<ProcessNotRespondingEventArgs> ProcessNotResponding;
        
        /// <summary>
        /// Событие изменения использования памяти процессом
        /// </summary>
        event EventHandler<ProcessMemoryChangedEventArgs> ProcessMemoryChanged;
        
        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;

namespace WindowsLauncher.Core.Interfaces.Lifecycle
{
    /// <summary>
    /// Главный сервис управления жизненным циклом приложений
    /// Унифицированный интерфейс для запуска, мониторинга и управления всеми типами приложений
    /// </summary>
    public interface IApplicationLifecycleService
    {
        #region Запуск приложений
        
        /// <summary>
        /// Запустить приложение и зарегистрировать его в системе отслеживания
        /// </summary>
        /// <param name="application">Приложение для запуска</param>
        /// <param name="launchedBy">Пользователь, запустивший приложение</param>
        /// <returns>Результат запуска с информацией об экземпляре</returns>
        Task<LaunchResult> LaunchAsync(Application application, string launchedBy);
        
        /// <summary>
        /// Зарегистрировать уже запущенное приложение (для внешних процессов)
        /// </summary>
        /// <param name="application">Модель приложения</param>
        /// <param name="processId">ID процесса</param>
        /// <param name="launchedBy">Пользователь</param>
        /// <returns>Экземпляр приложения или null при ошибке</returns>
        Task<ApplicationInstance?> RegisterExistingAsync(Application application, int processId, string launchedBy);
        
        #endregion

        #region Управление экземплярами
        
        /// <summary>
        /// Переключиться на приложение (активировать окно)
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>true если переключение успешно</returns>
        Task<bool> SwitchToAsync(string instanceId);
        
        /// <summary>
        /// Свернуть приложение
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> MinimizeAsync(string instanceId);
        
        /// <summary>
        /// Развернуть приложение
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>true если операция успешна</returns>
        Task<bool> RestoreAsync(string instanceId);
        
        /// <summary>
        /// Корректно закрыть приложение (WM_CLOSE)
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>true если приложение закрылось</returns>
        Task<bool> CloseAsync(string instanceId);
        
        /// <summary>
        /// Принудительно завершить приложение (Kill процесса)
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>true если процесс завершен</returns>
        Task<bool> KillAsync(string instanceId);
        
        /// <summary>
        /// Корректно закрыть все запущенные приложения (graceful shutdown)
        /// </summary>
        /// <param name="timeoutMs">Максимальное время ожидания закрытия каждого приложения (мс)</param>
        /// <returns>Результат операции с детальной информацией</returns>
        Task<ShutdownResult> CloseAllAsync(int timeoutMs = 5000);
        
        /// <summary>
        /// Принудительно завершить все запущенные приложения (kill всех процессов)
        /// </summary>
        /// <returns>Количество завершенных приложений</returns>
        Task<int> KillAllAsync();
        
        /// <summary>
        /// Комбинированное закрытие: сначала graceful, затем kill для несговорчивых
        /// </summary>
        /// <param name="gracefulTimeoutMs">Время на graceful закрытие</param>
        /// <param name="finalTimeoutMs">Время ожидания перед принудительным завершением</param>
        /// <returns>Результат операции</returns>
        Task<ShutdownResult> ShutdownAllAsync(int gracefulTimeoutMs = 5000, int finalTimeoutMs = 2000);
        
        #endregion

        #region Получение информации
        
        /// <summary>
        /// Получить все запущенные экземпляры приложений
        /// </summary>
        /// <returns>Коллекция всех активных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetRunningAsync();
        
        /// <summary>
        /// Получить экземпляр приложения по ID
        /// </summary>
        /// <param name="instanceId">Уникальный ID экземпляра</param>
        /// <returns>Экземпляр или null если не найден</returns>
        Task<ApplicationInstance?> GetByIdAsync(string instanceId);
        
        /// <summary>
        /// Получить экземпляры приложения по ID приложения
        /// </summary>
        /// <param name="applicationId">ID приложения в базе данных</param>
        /// <returns>Список экземпляров данного приложения</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetByApplicationIdAsync(int applicationId);
        
        /// <summary>
        /// Получить экземпляры приложений пользователя
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <returns>Список экземпляров пользователя</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetByUserAsync(string username);
        
        /// <summary>
        /// Получить общее количество запущенных приложений
        /// </summary>
        /// <returns>Количество активных экземпляров</returns>
        Task<int> GetCountAsync();
        
        /// <summary>
        /// Получить общее использование памяти всеми приложениями
        /// </summary>
        /// <returns>Использование памяти в МБ</returns>
        Task<long> GetTotalMemoryUsageAsync();
        
        /// <summary>
        /// Проверить, запущено ли приложение
        /// </summary>
        /// <param name="applicationId">ID приложения</param>
        /// <returns>true если есть активные экземпляры</returns>
        Task<bool> IsApplicationRunningAsync(int applicationId);
        
        #endregion

        #region Мониторинг и жизненный цикл сервиса
        
        /// <summary>
        /// Запустить мониторинг состояния приложений
        /// </summary>
        Task StartMonitoringAsync();
        
        /// <summary>
        /// Остановить мониторинг
        /// </summary>
        Task StopMonitoringAsync();
        
        /// <summary>
        /// Обновить состояние всех экземпляров (принудительно)
        /// </summary>
        Task RefreshAllAsync();
        
        /// <summary>
        /// Очистить завершенные экземпляры из коллекции
        /// </summary>
        Task CleanupAsync();
        
        #endregion

        #region События жизненного цикла
        
        /// <summary>
        /// Событие запуска нового экземпляра приложения
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceStarted;
        
        /// <summary>
        /// Событие завершения экземпляра приложения
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceStopped;
        
        /// <summary>
        /// Событие изменения состояния экземпляра (активация, сворачивание и т.д.)
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceStateChanged;
        
        /// <summary>
        /// Событие активации экземпляра (переключение фокуса)
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceActivated;
        
        /// <summary>
        /// Событие обновления метаданных экземпляра (память, заголовок окна)
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceUpdated;
        
        #endregion
    }
}
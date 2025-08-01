using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;

namespace WindowsLauncher.Core.Interfaces.Lifecycle
{
    /// <summary>
    /// Интерфейс для управления коллекцией экземпляров приложений
    /// Отвечает за регистрацию, хранение и предоставление доступа к экземплярам
    /// </summary>
    public interface IApplicationInstanceManager
    {
        #region Управление экземплярами
        
        /// <summary>
        /// Добавить новый экземпляр приложения
        /// </summary>
        /// <param name="instance">Экземпляр для добавления</param>
        /// <returns>true если экземпляр добавлен успешно</returns>
        Task<bool> AddInstanceAsync(ApplicationInstance instance);
        
        /// <summary>
        /// Удалить экземпляр приложения
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <returns>Удаленный экземпляр или null если не найден</returns>
        Task<ApplicationInstance?> RemoveInstanceAsync(string instanceId);
        
        /// <summary>
        /// Обновить экземпляр приложения
        /// </summary>
        /// <param name="instance">Обновленный экземпляр</param>
        /// <returns>true если обновление успешно</returns>
        Task<bool> UpdateInstanceAsync(ApplicationInstance instance);
        
        /// <summary>
        /// Проверить, существует ли экземпляр с данным ID
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <returns>true если экземпляр существует</returns>
        Task<bool> ContainsInstanceAsync(string instanceId);
        
        #endregion

        #region Получение экземпляров
        
        /// <summary>
        /// Получить все экземпляры приложений
        /// </summary>
        /// <returns>Коллекция всех экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetAllInstancesAsync();
        
        /// <summary>
        /// Получить экземпляр по ID
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <returns>Экземпляр или null если не найден</returns>
        Task<ApplicationInstance?> GetInstanceAsync(string instanceId);
        
        /// <summary>
        /// Получить экземпляры по ID приложения
        /// </summary>
        /// <param name="applicationId">ID приложения</param>
        /// <returns>Список экземпляров приложения</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetInstancesByApplicationIdAsync(int applicationId);
        
        /// <summary>
        /// Получить экземпляры по ID процесса
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <returns>Список экземпляров с данным процессом</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetInstancesByProcessIdAsync(int processId);
        
        /// <summary>
        /// Получить экземпляры пользователя
        /// </summary>
        /// <param name="username">Имя пользователя</param>
        /// <returns>Список экземпляров пользователя</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetInstancesByUserAsync(string username);
        
        /// <summary>
        /// Получить экземпляры по состоянию
        /// </summary>
        /// <param name="state">Состояние приложения</param>
        /// <returns>Список экземпляров в данном состоянии</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetInstancesByStateAsync(ApplicationState state);
        
        /// <summary>
        /// Получить экземпляры по типу приложения
        /// </summary>
        /// <param name="applicationType">Тип приложения</param>
        /// <returns>Список экземпляров данного типа</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetInstancesByTypeAsync(Enums.ApplicationType applicationType);
        
        #endregion

        #region Фильтрация и поиск
        
        /// <summary>
        /// Найти экземпляры по предикату
        /// </summary>
        /// <param name="predicate">Функция фильтрации</param>
        /// <returns>Список найденных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> FindInstancesAsync(Func<ApplicationInstance, bool> predicate);
        
        /// <summary>
        /// Найти первый экземпляр по предикату
        /// </summary>
        /// <param name="predicate">Функция поиска</param>
        /// <returns>Первый найденный экземпляр или null</returns>
        Task<ApplicationInstance?> FindFirstInstanceAsync(Func<ApplicationInstance, bool> predicate);
        
        /// <summary>
        /// Получить активные экземпляры (не завершенные)
        /// </summary>
        /// <returns>Список активных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync();
        
        /// <summary>
        /// Получить завершенные экземпляры
        /// </summary>
        /// <returns>Список завершенных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetTerminatedInstancesAsync();
        
        #endregion

        #region Статистика и мониторинг
        
        /// <summary>
        /// Получить общее количество экземпляров
        /// </summary>
        /// <returns>Общее количество</returns>
        Task<int> GetTotalCountAsync();
        
        /// <summary>
        /// Получить количество активных экземпляров
        /// </summary>
        /// <returns>Количество активных</returns>
        Task<int> GetActiveCountAsync();
        
        /// <summary>
        /// Получить общее использование памяти всеми экземплярами
        /// </summary>
        /// <returns>Использование памяти в МБ</returns>
        Task<long> GetTotalMemoryUsageAsync();
        
        /// <summary>
        /// Получить статистику по типам приложений
        /// </summary>
        /// <returns>Словарь: тип приложения -> количество экземпляров</returns>
        Task<Dictionary<Enums.ApplicationType, int>> GetTypeStatisticsAsync();
        
        /// <summary>
        /// Получить статистику по состояниям
        /// </summary>
        /// <returns>Словарь: состояние -> количество экземпляров</returns>
        Task<Dictionary<ApplicationState, int>> GetStateStatisticsAsync();
        
        #endregion

        #region Очистка и обслуживание
        
        /// <summary>
        /// Очистить завершенные экземпляры из коллекции
        /// </summary>
        /// <returns>Количество удаленных экземпляров</returns>
        Task<int> CleanupTerminatedInstancesAsync();
        
        /// <summary>
        /// Очистить экземпляры старше указанного времени
        /// </summary>
        /// <param name="maxAge">Максимальный возраст экземпляра</param>
        /// <returns>Количество удаленных экземпляров</returns>
        Task<int> CleanupOldInstancesAsync(TimeSpan maxAge);
        
        /// <summary>
        /// Очистить все экземпляры
        /// </summary>
        /// <returns>Количество удаленных экземпляров</returns>
        Task<int> ClearAllInstancesAsync();
        
        /// <summary>
        /// Проверить целостность коллекции экземпляров
        /// </summary>
        /// <returns>Список проблемных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> ValidateInstancesAsync();
        
        #endregion

        #region События коллекции
        
        /// <summary>
        /// Событие добавления экземпляра
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceAdded;
        
        /// <summary>
        /// Событие удаления экземпляра
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceRemoved;
        
        /// <summary>
        /// Событие обновления экземпляра
        /// </summary>
        event EventHandler<ApplicationInstanceEventArgs> InstanceUpdated;
        
        /// <summary>
        /// Событие очистки коллекции
        /// </summary>
        event EventHandler<EventArgs> CollectionCleared;
        
        #endregion
    }
}
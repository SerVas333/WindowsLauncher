using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Core.Interfaces.Lifecycle
{
    /// <summary>
    /// Интерфейс специализированного лаунчера для конкретного типа приложений
    /// Каждый лаунчер знает, как правильно запустить и найти окно для своего типа приложений
    /// </summary>
    public interface IApplicationLauncher
    {
        /// <summary>
        /// Тип приложений, которые поддерживает данный лаунчер
        /// </summary>
        ApplicationType SupportedType { get; }
        
        /// <summary>
        /// Приоритет лаунчера (для случаев когда несколько лаунчеров поддерживают один тип)
        /// Чем больше значение, тем выше приоритет
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Проверить, может ли лаунчер обработать данное приложение
        /// </summary>
        /// <param name="application">Приложение для проверки</param>
        /// <returns>true если лаунчер может обработать приложение</returns>
        bool CanLaunch(Application application);
        
        /// <summary>
        /// Запустить приложение
        /// </summary>
        /// <param name="application">Приложение для запуска</param>
        /// <param name="launchedBy">Пользователь, запускающий приложение</param>
        /// <returns>Результат запуска</returns>
        Task<LaunchResult> LaunchAsync(Application application, string launchedBy);
        
        /// <summary>
        /// Найти главное окно для запущенного процесса приложения
        /// Специализированная логика поиска окна для каждого типа приложений
        /// </summary>
        /// <param name="processId">ID процесса</param>
        /// <param name="application">Модель приложения (для контекста поиска)</param>
        /// <returns>Информация о найденном окне или null</returns>
        Task<WindowInfo?> FindMainWindowAsync(int processId, Application application);
        
        /// <summary>
        /// Найти существующий экземпляр приложения (для избежания дублирования)
        /// </summary>
        /// <param name="application">Приложение для поиска</param>
        /// <returns>Найденный экземпляр или null</returns>
        Task<ApplicationInstance?> FindExistingInstanceAsync(Application application);
        
        /// <summary>
        /// Получить время ожидания инициализации окна для данного типа приложений
        /// </summary>
        /// <param name="application">Приложение</param>
        /// <returns>Время ожидания в миллисекундах</returns>
        int GetWindowInitializationTimeoutMs(Application application);
        
        /// <summary>
        /// Выполнить специфичную для типа приложения очистку ресурсов
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        Task CleanupAsync(ApplicationInstance instance);

        /// <summary>
        /// Переключиться на указанный экземпляр приложения (активировать окно)
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <returns>true если переключение успешно</returns>
        Task<bool> SwitchToAsync(string instanceId);

        /// <summary>
        /// Завершить указанный экземпляр приложения
        /// </summary>
        /// <param name="instanceId">ID экземпляра</param>
        /// <param name="force">Принудительное завершение</param>
        /// <returns>true если завершение успешно</returns>
        Task<bool> TerminateAsync(string instanceId, bool force = false);

        /// <summary>
        /// Получить все активные экземпляры приложений этого лаунчера
        /// </summary>
        /// <returns>Список активных экземпляров</returns>
        Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync();

        /// <summary>
        /// Событие активации окна приложения (для интеграции с AppSwitcher)
        /// Генерируется когда окно приложения становится активным/получает фокус
        /// </summary>
        event EventHandler<ApplicationInstance>? WindowActivated;

        /// <summary>
        /// Событие закрытия окна приложения (для управления жизненным циклом)
        /// Генерируется когда окно приложения закрывается пользователем
        /// </summary>
        event EventHandler<ApplicationInstance>? WindowClosed;
    }
}
using System;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Core.Models.Lifecycle.Events
{
    /// <summary>
    /// Аргументы события изменения экземпляра приложения
    /// Используется во всех событиях жизненного цикла приложений
    /// </summary>
    public class ApplicationInstanceEventArgs : EventArgs
    {
        #region Основная информация о событии
        
        /// <summary>
        /// Экземпляр приложения, с которым произошло событие
        /// </summary>
        public ApplicationInstance Instance { get; }
        
        /// <summary>
        /// Тип события
        /// </summary>
        public ApplicationInstanceEventType EventType { get; }
        
        /// <summary>
        /// Время возникновения события
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Описание события (дополнительная информация)
        /// </summary>
        public string? Description { get; set; }
        
        #endregion

        #region Информация об изменениях состояния
        
        /// <summary>
        /// Предыдущее состояние приложения (для событий изменения состояния)
        /// </summary>
        public ApplicationState? PreviousState { get; set; }
        
        /// <summary>
        /// Новое состояние приложения (для событий изменения состояния)
        /// </summary>
        public ApplicationState? NewState { get; set; }
        
        /// <summary>
        /// Причина изменения состояния
        /// </summary>
        public string? StateChangeReason { get; set; }
        
        /// <summary>
        /// Алиас для StateChangeReason для обратной совместимости
        /// </summary>
        public string? Reason
        {
            get => StateChangeReason;
            set => StateChangeReason = value;
        }
        
        #endregion

        #region Диагностическая информация
        
        /// <summary>
        /// Источник события (компонент, который инициировал событие)
        /// </summary>
        public string? Source { get; set; }
        
        /// <summary>
        /// Дополнительные метаданные события
        /// </summary>
        public object? AdditionalData { get; set; }
        
        /// <summary>
        /// Было ли событие обработано автоматически
        /// </summary>
        public bool IsAutomated { get; set; }
        
        #endregion

        #region Конструкторы
        
        /// <summary>
        /// Основной конструктор для событий экземпляра приложения
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="eventType">Тип события</param>
        /// <param name="description">Описание события</param>
        public ApplicationInstanceEventArgs(
            ApplicationInstance instance, 
            ApplicationInstanceEventType eventType, 
            string? description = null)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            EventType = eventType;
            Timestamp = DateTime.Now;
            Description = description;
        }
        
        /// <summary>
        /// Конструктор для событий изменения состояния
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="eventType">Тип события</param>
        /// <param name="previousState">Предыдущее состояние</param>
        /// <param name="newState">Новое состояние</param>
        /// <param name="reason">Причина изменения</param>
        public ApplicationInstanceEventArgs(
            ApplicationInstance instance,
            ApplicationInstanceEventType eventType,
            ApplicationState previousState,
            ApplicationState newState,
            string? reason = null) : this(instance, eventType)
        {
            PreviousState = previousState;
            NewState = newState;
            StateChangeReason = reason;
        }
        
        #endregion

        #region Статические методы создания событий
        
        /// <summary>
        /// Создать событие запуска экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события запуска</returns>
        public static ApplicationInstanceEventArgs Started(ApplicationInstance instance, string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.Started, "Application instance started")
            {
                Source = source,
                NewState = instance.State
            };
        }
        
        /// <summary>
        /// Создать событие завершения экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="reason">Причина завершения</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события завершения</returns>
        public static ApplicationInstanceEventArgs Stopped(ApplicationInstance instance, string? reason = null, string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.Stopped, reason ?? "Application instance stopped")
            {
                Source = source,
                StateChangeReason = reason,
                NewState = ApplicationState.Terminated
            };
        }
        
        /// <summary>
        /// Создать событие изменения состояния
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="previousState">Предыдущее состояние</param>
        /// <param name="newState">Новое состояние</param>
        /// <param name="reason">Причина изменения</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события изменения состояния</returns>
        public static ApplicationInstanceEventArgs StateChanged(
            ApplicationInstance instance, 
            ApplicationState previousState, 
            ApplicationState newState, 
            string? reason = null, 
            string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.StateChanged, previousState, newState, reason)
            {
                Description = $"State changed from {previousState} to {newState}",
                Source = source
            };
        }
        
        /// <summary>
        /// Создать событие активации экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события активации</returns>
        public static ApplicationInstanceEventArgs Activated(ApplicationInstance instance, string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.Activated, "Application instance activated")
            {
                Source = source,
                NewState = ApplicationState.Active
            };
        }
        
        /// <summary>
        /// Создать событие обновления экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="updateDetails">Детали обновления</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события обновления</returns>
        public static ApplicationInstanceEventArgs Updated(ApplicationInstance instance, string? updateDetails = null, string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.Updated, updateDetails ?? "Application instance updated")
            {
                Source = source
            };
        }
        
        /// <summary>
        /// Создать событие ошибки экземпляра
        /// </summary>
        /// <param name="instance">Экземпляр приложения</param>
        /// <param name="errorMessage">Сообщение об ошибке</param>
        /// <param name="exception">Исключение (опционально)</param>
        /// <param name="source">Источник события</param>
        /// <returns>Аргументы события ошибки</returns>
        public static ApplicationInstanceEventArgs Error(ApplicationInstance instance, string errorMessage, Exception? exception = null, string? source = null)
        {
            return new ApplicationInstanceEventArgs(instance, ApplicationInstanceEventType.Error, errorMessage)
            {
                Source = source,
                AdditionalData = exception,
                NewState = ApplicationState.Error
            };
        }
        
        #endregion

        #region Методы проверки
        
        /// <summary>
        /// Проверить, является ли событие изменением состояния
        /// </summary>
        /// <returns>true если событие связано с изменением состояния</returns>
        public bool IsStateChangeEvent()
        {
            return EventType == ApplicationInstanceEventType.StateChanged || 
                   (PreviousState.HasValue && NewState.HasValue && PreviousState != NewState);
        }
        
        /// <summary>
        /// Проверить, является ли событие критическим (требует внимания)
        /// </summary>
        /// <returns>true если событие критическое</returns>
        public bool IsCriticalEvent()
        {
            return EventType switch
            {
                ApplicationInstanceEventType.Error => true,
                ApplicationInstanceEventType.Stopped when StateChangeReason?.Contains("crash", StringComparison.OrdinalIgnoreCase) == true => true,
                ApplicationInstanceEventType.StateChanged when NewState == ApplicationState.NotResponding => true,
                ApplicationInstanceEventType.StateChanged when NewState == ApplicationState.Error => true,
                _ => false
            };
        }
        
        /// <summary>
        /// Получить уровень важности события
        /// </summary>
        /// <returns>Уровень важности</returns>
        public EventSeverity GetSeverity()
        {
            return EventType switch
            {
                ApplicationInstanceEventType.Error => EventSeverity.Error,
                ApplicationInstanceEventType.Stopped when StateChangeReason?.Contains("crash", StringComparison.OrdinalIgnoreCase) == true => EventSeverity.Error,
                ApplicationInstanceEventType.StateChanged when NewState == ApplicationState.NotResponding => EventSeverity.Warning,
                ApplicationInstanceEventType.StateChanged when NewState == ApplicationState.Error => EventSeverity.Error,
                ApplicationInstanceEventType.Started => EventSeverity.Info,
                ApplicationInstanceEventType.Stopped => EventSeverity.Info,
                ApplicationInstanceEventType.Activated => EventSeverity.Debug,
                ApplicationInstanceEventType.Updated => EventSeverity.Debug,
                ApplicationInstanceEventType.StateChanged => EventSeverity.Info,
                _ => EventSeverity.Debug
            };
        }
        
        #endregion

        #region Переопределенные методы
        
        /// <summary>
        /// Строковое представление события
        /// </summary>
        /// <returns>Описание события</returns>
        public override string ToString()
        {
            var appName = Instance.Application?.Name ?? "Unknown";
            var eventDesc = Description ?? EventType.ToString();
            
            if (IsStateChangeEvent() && PreviousState.HasValue && NewState.HasValue)
            {
                return $"{EventType}: {appName} ({PreviousState} → {NewState}) - {eventDesc}";
            }
            
            return $"{EventType}: {appName} - {eventDesc}";
        }
        
        #endregion
    }
    
    /// <summary>
    /// Типы событий экземпляра приложения
    /// </summary>
    public enum ApplicationInstanceEventType
    {
        /// <summary>
        /// Экземпляр приложения запущен
        /// </summary>
        Started,
        
        /// <summary>
        /// Экземпляр приложения завершен
        /// </summary>
        Stopped,
        
        /// <summary>
        /// Изменилось состояние экземпляра
        /// </summary>
        StateChanged,
        
        /// <summary>
        /// Экземпляр активирован (переключен в фокус)
        /// </summary>
        Activated,
        
        /// <summary>
        /// Обновлена информация об экземпляре
        /// </summary>
        Updated,
        
        /// <summary>
        /// Произошла ошибка с экземпляром
        /// </summary>
        Error
    }
    
    /// <summary>
    /// Уровень важности события
    /// </summary>
    public enum EventSeverity
    {
        /// <summary>
        /// Отладочная информация
        /// </summary>
        Debug,
        
        /// <summary>
        /// Информационное событие
        /// </summary>
        Info,
        
        /// <summary>
        /// Предупреждение
        /// </summary>
        Warning,
        
        /// <summary>
        /// Ошибка
        /// </summary>
        Error,
        
        /// <summary>
        /// Критическая ошибка
        /// </summary>
        Critical
    }
}
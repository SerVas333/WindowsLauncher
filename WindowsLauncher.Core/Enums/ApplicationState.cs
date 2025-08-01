namespace WindowsLauncher.Core.Enums
{
    /// <summary>
    /// Состояния экземпляра приложения в системе управления жизненным циклом
    /// Определяет текущее состояние приложения с момента запуска до завершения
    /// </summary>
    public enum ApplicationState
    {
        /// <summary>
        /// Приложение в процессе запуска (процесс создан, но окно еще не найдено)
        /// </summary>
        Starting = 0,
        
        /// <summary>
        /// Приложение запущено и работает нормально
        /// </summary>
        Running = 1,
        
        /// <summary>
        /// Приложение активно (окно в фокусе)
        /// </summary>
        Active = 2,
        
        /// <summary>
        /// Приложение свернуто
        /// </summary>
        Minimized = 3,
        
        /// <summary>
        /// Приложение не отвечает на запросы (зависло)
        /// </summary>
        NotResponding = 4,
        
        /// <summary>
        /// Приложение в процессе закрытия (получило WM_CLOSE)
        /// </summary>
        Closing = 5,
        
        /// <summary>
        /// Приложение завершено (процесс завершился)
        /// </summary>
        Terminated = 6,
        
        /// <summary>
        /// Ошибка в работе приложения (неожиданное завершение, критическая ошибка)
        /// </summary>
        Error = 7,
        
        /// <summary>
        /// Приложение временно приостановлено системой
        /// </summary>
        Suspended = 8
    }
    
    /// <summary>
    /// Расширения для работы с ApplicationState
    /// </summary>
    public static class ApplicationStateExtensions
    {
        /// <summary>
        /// Проверить, является ли состояние активным (приложение работает)
        /// </summary>
        /// <param name="state">Состояние для проверки</param>
        /// <returns>true если приложение активно</returns>
        public static bool IsActive(this ApplicationState state)
        {
            return state switch
            {
                ApplicationState.Starting => true,
                ApplicationState.Running => true,
                ApplicationState.Active => true,
                ApplicationState.Minimized => true,
                ApplicationState.NotResponding => true,
                ApplicationState.Suspended => true,
                ApplicationState.Closing => false,
                ApplicationState.Terminated => false,
                ApplicationState.Error => false,
                _ => false
            };
        }
        
        /// <summary>
        /// Проверить, является ли состояние завершенным (приложение не работает)
        /// </summary>
        /// <param name="state">Состояние для проверки</param>
        /// <returns>true если приложение завершено</returns>
        public static bool IsTerminated(this ApplicationState state)
        {
            return state switch
            {
                ApplicationState.Terminated => true,
                ApplicationState.Error => true,
                _ => false
            };
        }
        
        /// <summary>
        /// Проверить, можно ли переключиться на приложение в данном состоянии
        /// </summary>
        /// <param name="state">Состояние для проверки</param>
        /// <returns>true если можно переключиться</returns>
        public static bool CanSwitchTo(this ApplicationState state)
        {
            return state switch
            {
                ApplicationState.Running => true,
                ApplicationState.Active => true,
                ApplicationState.Minimized => true,
                ApplicationState.NotResponding => true, // Можно попробовать переключиться
                ApplicationState.Starting => false,
                ApplicationState.Closing => false,
                ApplicationState.Terminated => false,
                ApplicationState.Error => false,
                ApplicationState.Suspended => false,
                _ => false
            };
        }
        
        /// <summary>
        /// Получить отображаемое имя состояния для UI
        /// </summary>
        /// <param name="state">Состояние</param>
        /// <returns>Локализованное имя состояния</returns>
        public static string GetDisplayName(this ApplicationState state)
        {
            return state switch
            {
                ApplicationState.Starting => "Запускается",
                ApplicationState.Running => "Работает",  
                ApplicationState.Active => "Активно",
                ApplicationState.Minimized => "Свернуто",
                ApplicationState.NotResponding => "Не отвечает",
                ApplicationState.Closing => "Закрывается",
                ApplicationState.Terminated => "Завершено",
                ApplicationState.Error => "Ошибка",
                ApplicationState.Suspended => "Приостановлено",
                _ => "Неизвестно"
            };
        }
        
        /// <summary>
        /// Получить цвет состояния для UI (HEX формат)
        /// </summary>
        /// <param name="state">Состояние</param>
        /// <returns>HEX цвет состояния</returns>
        public static string GetStateColor(this ApplicationState state)
        {
            return state switch
            {
                ApplicationState.Starting => "#FFA500", // Orange
                ApplicationState.Running => "#4CAF50",  // Green
                ApplicationState.Active => "#2196F3",   // Blue
                ApplicationState.Minimized => "#9E9E9E", // Gray
                ApplicationState.NotResponding => "#FF9800", // Orange/Warning
                ApplicationState.Closing => "#FF5722",  // Deep Orange
                ApplicationState.Terminated => "#607D8B", // Blue Gray
                ApplicationState.Error => "#F44336",    // Red
                ApplicationState.Suspended => "#795548", // Brown
                _ => "#9E9E9E" // Default Gray
            };
        }
        
        /// <summary>
        /// Проверить, является ли переход между состояниями валидным
        /// </summary>
        /// <param name="from">Исходное состояние</param>
        /// <param name="to">Целевое состояние</param>
        /// <returns>true если переход валиден</returns>
        public static bool IsValidTransition(ApplicationState from, ApplicationState to)
        {
            // Из завершенных состояний нельзя переходить никуда  
            if (from.IsTerminated()) return false;
            
            return (from, to) switch
            {
                // Из Starting можно в любое активное состояние или ошибку
                (ApplicationState.Starting, ApplicationState.Running) => true,
                (ApplicationState.Starting, ApplicationState.Active) => true,
                (ApplicationState.Starting, ApplicationState.Error) => true,
                (ApplicationState.Starting, ApplicationState.Terminated) => true,
                
                // Из Running в любое состояние кроме Starting
                (ApplicationState.Running, ApplicationState.Active) => true,
                (ApplicationState.Running, ApplicationState.Minimized) => true,
                (ApplicationState.Running, ApplicationState.NotResponding) => true,
                (ApplicationState.Running, ApplicationState.Closing) => true,
                (ApplicationState.Running, ApplicationState.Suspended) => true,
                
                // Из Active в любое состояние кроме Starting
                (ApplicationState.Active, ApplicationState.Running) => true,
                (ApplicationState.Active, ApplicationState.Minimized) => true,
                (ApplicationState.Active, ApplicationState.NotResponding) => true,
                (ApplicationState.Active, ApplicationState.Closing) => true,
                (ApplicationState.Active, ApplicationState.Suspended) => true,
                
                // Из Minimized можно активировать или закрыть
                (ApplicationState.Minimized, ApplicationState.Running) => true,
                (ApplicationState.Minimized, ApplicationState.Active) => true,
                (ApplicationState.Minimized, ApplicationState.NotResponding) => true,
                (ApplicationState.Minimized, ApplicationState.Closing) => true,
                
                // Из NotResponding можно восстановиться или завершиться
                (ApplicationState.NotResponding, ApplicationState.Running) => true,
                (ApplicationState.NotResponding, ApplicationState.Active) => true,
                (ApplicationState.NotResponding, ApplicationState.Closing) => true,
                (ApplicationState.NotResponding, ApplicationState.Error) => true,
                (ApplicationState.NotResponding, ApplicationState.Terminated) => true,
                
                // Из Closing только в завершенные состояния
                (ApplicationState.Closing, ApplicationState.Terminated) => true,
                (ApplicationState.Closing, ApplicationState.Error) => true,
                
                // Из Suspended можно вернуться в активные состояния
                (ApplicationState.Suspended, ApplicationState.Running) => true,
                (ApplicationState.Suspended, ApplicationState.Active) => true,
                (ApplicationState.Suspended, ApplicationState.Terminated) => true,
                
                // Переход в то же состояние всегда валиден
                _ when from == to => true,
                
                // Все остальные переходы невалидны
                _ => false
            };
        }
    }
}
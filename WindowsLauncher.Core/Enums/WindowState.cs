using System;

namespace WindowsLauncher.Core.Enums
{
    /// <summary>
    /// Состояния окна приложения
    /// </summary>
    public enum ApplicationWindowState
    {
        /// <summary>
        /// Обычное состояние
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// Свернуто
        /// </summary>
        Minimized = 1,
        
        /// <summary>
        /// Развернуто на весь экран
        /// </summary>
        Maximized = 2,
        
        /// <summary>
        /// Скрыто
        /// </summary>
        Hidden = 3,
        
        /// <summary>
        /// Активно (имеет фокус)
        /// </summary>
        Active = 4
    }
}
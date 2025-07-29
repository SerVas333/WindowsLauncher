namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Режим работы приложения
    /// </summary>
    public enum ShellMode
    {
        /// <summary>
        /// Обычный режим - работа как обычное приложение в Windows
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// Режим Shell - замена стандартного проводника Windows
        /// </summary>
        Shell = 1
    }

    /// <summary>
    /// Конфигурация хоткеев для переключателя приложений
    /// </summary>
    public class AppSwitcherHotKeyConfig
    {
        /// <summary>
        /// Основная комбинация для переключения вперед
        /// </summary>
        public string ForwardHotKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Комбинация для переключения назад
        /// </summary>
        public string BackwardHotKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Включен ли переключатель приложений
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Режим работы приложения
        /// </summary>
        public ShellMode ShellMode { get; set; } = ShellMode.Normal;
    }
}
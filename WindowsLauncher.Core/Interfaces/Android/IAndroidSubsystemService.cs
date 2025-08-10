using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Сервис управления жизненным циклом Android подсистемы (WSA)
    /// </summary>
    public interface IAndroidSubsystemService
    {
        /// <summary>
        /// Текущий режим работы Android подсистемы
        /// </summary>
        AndroidMode CurrentMode { get; }

        /// <summary>
        /// Статус WSA (запущен/остановлен/недоступен)
        /// </summary>
        string WSAStatus { get; }

        /// <summary>
        /// Инициализировать сервис и применить настройки
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Проверить, доступна ли Android функциональность в текущем режиме
        /// </summary>
        Task<bool> IsAndroidAvailableAsync();

        /// <summary>
        /// Предварительно запустить WSA (для режима Preload)
        /// </summary>
        Task<bool> PreloadWSAAsync();

        /// <summary>
        /// Остановить WSA (для оптимизации ресурсов)
        /// </summary>
        Task<bool> StopWSAAsync();

        /// <summary>
        /// Проверить потребление памяти и применить оптимизации
        /// </summary>
        Task OptimizeResourceUsageAsync();

        /// <summary>
        /// Получить детальную информацию о статусе Android подсистемы
        /// </summary>
        Task<Dictionary<string, object>> GetDetailedStatusAsync();

        /// <summary>
        /// Событие изменения статуса WSA
        /// </summary>
        event EventHandler<string>? StatusChanged;
    }
}
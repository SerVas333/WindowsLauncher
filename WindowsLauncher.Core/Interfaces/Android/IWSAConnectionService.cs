using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Сервис для управления подключениями к Windows Subsystem for Android (WSA)
    /// Отвечает за: проверку доступности, запуск/остановку WSA, подключение через ADB
    /// </summary>
    public interface IWSAConnectionService
    {
        /// <summary>
        /// Проверить доступность Windows Subsystem for Android в системе
        /// </summary>
        /// <returns>True, если WSA установлен и доступен</returns>
        Task<bool> IsWSAAvailableAsync();

        /// <summary>
        /// Проверить запущен ли WSA в данный момент
        /// </summary>
        /// <returns>True, если WSA активно работает</returns>
        Task<bool> IsWSARunningAsync();

        /// <summary>
        /// Запустить WSA если он не запущен
        /// </summary>
        /// <returns>True, если WSA был успешно запущен или уже работал</returns>
        Task<bool> StartWSAAsync();

        /// <summary>
        /// Остановить WSA
        /// </summary>
        /// <returns>True, если WSA был успешно остановлен</returns>
        Task<bool> StopWSAAsync();

        /// <summary>
        /// Проверить доступность ADB (Android Debug Bridge)
        /// </summary>
        /// <returns>True, если ADB доступен для выполнения команд</returns>
        Task<bool> IsAdbAvailableAsync();

        /// <summary>
        /// Подключиться к WSA через ADB с retry механизмом
        /// </summary>
        /// <returns>True, если подключение установлено успешно</returns>
        Task<bool> ConnectToWSAAsync();

        /// <summary>
        /// Получить версию Android в WSA
        /// </summary>
        /// <returns>Версия Android или null, если не удалось определить</returns>
        Task<string?> GetAndroidVersionAsync();

        /// <summary>
        /// Получить подробную информацию о статусе WSA и ADB подключения
        /// </summary>
        /// <returns>Словарь с детальной информацией о состоянии соединения</returns>
        Task<Dictionary<string, object>> GetConnectionStatusAsync();

        /// <summary>
        /// Событие изменения статуса подключения к WSA
        /// </summary>
        event EventHandler<WSAConnectionStatusEventArgs>? ConnectionStatusChanged;
    }

    /// <summary>
    /// Аргументы события изменения статуса подключения WSA
    /// </summary>
    public class WSAConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? ErrorMessage { get; set; }
    }
}
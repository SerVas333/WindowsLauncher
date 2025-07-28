using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис управления сессиями пользователей
    /// </summary>
    public interface ISessionManagementService
    {
        /// <summary>
        /// Событие изменения сессии
        /// </summary>
        event EventHandler<SessionEventArgs>? SessionEvent;

        /// <summary>
        /// Текущий пользователь сессии
        /// </summary>
        User? CurrentUser { get; }

        /// <summary>
        /// Активна ли сессия
        /// </summary>
        bool IsSessionActive { get; }

        /// <summary>
        /// Режим работы как Shell
        /// </summary>
        bool IsRunningAsShell { get; }

        /// <summary>
        /// Конфигурация управления сессиями
        /// </summary>
        SessionConfiguration Configuration { get; }

        /// <summary>
        /// Начать сессию пользователя
        /// </summary>
        Task<bool> StartSessionAsync(User user);

        /// <summary>
        /// Завершить текущую сессию
        /// </summary>
        Task<bool> EndSessionAsync(string? reason = null);

        /// <summary>
        /// Обработать закрытие главного окна
        /// </summary>
        Task<bool> HandleMainWindowClosingAsync();

        /// <summary>
        /// Обработать запрос выхода пользователя
        /// </summary>
        Task<bool> HandleLogoutRequestAsync();

        /// <summary>
        /// Перезапустить приложение (для Shell режима)
        /// </summary>
        Task RestartApplicationAsync();

        /// <summary>
        /// Показать окно входа
        /// </summary>
        Task<User?> ShowLoginWindowAsync();

        /// <summary>
        /// Загрузить конфигурацию управления сессиями
        /// </summary>
        Task LoadConfigurationAsync();

        /// <summary>
        /// Проверить, можно ли закрыть приложение
        /// </summary>
        Task<bool> CanCloseApplicationAsync();
    }
}
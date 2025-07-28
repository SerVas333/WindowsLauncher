using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Конфигурация управления сессиями и режимами работы приложения
    /// </summary>
    public class SessionConfiguration
    {
        /// <summary>
        /// Работать как Shell (замена explorer.exe)
        /// </summary>
        public bool RunAsShell { get; set; } = false;

        /// <summary>
        /// Автоматически перезапускать при закрытии (для Shell режима)
        /// </summary>
        public bool AutoRestartOnClose { get; set; } = false;

        /// <summary>
        /// Выполнять разлогинивание при закрытии главного окна
        /// </summary>
        public bool LogoutOnMainWindowClose { get; set; } = true;

        /// <summary>
        /// Возвращаться к окну входа после разлогинивания
        /// </summary>
        public bool ReturnToLoginOnLogout { get; set; } = true;

        /// <summary>
        /// Разрешить множественные сессии пользователя
        /// </summary>
        public bool AllowMultipleSessions { get; set; } = false;

        /// <summary>
        /// Сообщение предупреждения для Shell режима
        /// </summary>
        public string ShellWarningMessage { get; set; } = "Приложение работает в режиме Shell. Закрытие приведет к перезапуску.";

        /// <summary>
        /// Сообщение подтверждения выхода
        /// </summary>
        public string LogoutConfirmationMessage { get; set; } = "Вы действительно хотите выйти из системы?";

        /// <summary>
        /// Сворачивать вместо закрытия
        /// </summary>
        public bool MinimizeInsteadOfClose { get; set; } = false;

        /// <summary>
        /// Валидация конфигурации сессии
        /// </summary>
        public SessionValidationResult Validate()
        {
            var errors = new List<string>();

            if (RunAsShell && !AutoRestartOnClose)
            {
                errors.Add("В режиме Shell рекомендуется включить AutoRestartOnClose");
            }

            if (string.IsNullOrWhiteSpace(ShellWarningMessage))
            {
                errors.Add("ShellWarningMessage не может быть пустым");
            }

            if (string.IsNullOrWhiteSpace(LogoutConfirmationMessage))
            {
                errors.Add("LogoutConfirmationMessage не может быть пустым");
            }

            return new SessionValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray()
            };
        }
    }

    /// <summary>
    /// Результат валидации конфигурации сессии
    /// </summary>
    public class SessionValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Типы событий сессии
    /// </summary>
    public enum SessionEventType
    {
        Login,
        Logout,
        MainWindowClosing,
        SessionExpired,
        ForceLogout,
        ShellRestart
    }

    /// <summary>
    /// Аргументы события сессии
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        public SessionEventType EventType { get; set; }
        public User? User { get; set; }
        public string? Reason { get; set; }
        public bool CanCancel { get; set; } = false;
        public bool Cancel { get; set; } = false;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
using System.Threading.Tasks;

namespace WindowsLauncher.UI.Infrastructure.Services
{
    /// <summary>
    /// Сервис для отображения диалогов и уведомлений
    /// Абстрагирует UI от ViewModels для лучшей testability
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Показать сообщение об ошибке
        /// </summary>
        void ShowError(string message, string title = "Error");

        /// <summary>
        /// Показать предупреждение
        /// </summary>
        void ShowWarning(string message, string title = "Warning");

        /// <summary>
        /// Показать информационное сообщение
        /// </summary>
        void ShowInfo(string message, string title = "Information");

        /// <summary>
        /// Показать диалог подтверждения
        /// </summary>
        bool ShowConfirmation(string message, string title = "Confirmation");

        /// <summary>
        /// Показать диалог ввода текста
        /// </summary>
        string? ShowInputDialog(string message, string title = "Input", string defaultValue = "");

        /// <summary>
        /// Показать асинхронное уведомление (toast)
        /// </summary>
        Task ShowToastAsync(string message, ToastType type = ToastType.Information, int durationMs = 3000);
    }

    public enum ToastType
    {
        Information,
        Success,
        Warning,
        Error
    }
}
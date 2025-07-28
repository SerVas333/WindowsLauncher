using System;
using System.Threading.Tasks;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис для управления виртуальной клавиатурой
    /// </summary>
    public interface IVirtualKeyboardService
    {
        /// <summary>
        /// Показать виртуальную клавиатуру
        /// </summary>
        /// <returns>True если виртуальная клавиатура была запущена успешно</returns>
        Task<bool> ShowVirtualKeyboardAsync();

        /// <summary>
        /// Скрыть виртуальную клавиатуру
        /// </summary>
        /// <returns>True если виртуальная клавиатура была закрыта успешно</returns>
        Task<bool> HideVirtualKeyboardAsync();

        /// <summary>
        /// Проверить, запущена ли виртуальная клавиатура
        /// </summary>
        /// <returns>True если виртуальная клавиатура активна</returns>
        bool IsVirtualKeyboardRunning();

        /// <summary>
        /// Переключить состояние виртуальной клавиатуры
        /// </summary>
        /// <returns>True если операция выполнена успешно</returns>
        Task<bool> ToggleVirtualKeyboardAsync();

        /// <summary>
        /// Проверить доступность виртуальной клавиатуры в системе
        /// </summary>
        /// <returns>True если виртуальная клавиатура доступна</returns>
        bool IsVirtualKeyboardAvailable();

        /// <summary>
        /// Диагностика состояния виртуальной клавиатуры для отладки
        /// </summary>
        /// <returns>Строка с результатами диагностики</returns>
        Task<string> DiagnoseVirtualKeyboardAsync();

        /// <summary>
        /// Принудительно позиционировать клавиатуру в видимой области экрана
        /// </summary>
        /// <returns>True если операция выполнена успешно</returns>
        Task<bool> RepositionKeyboardAsync();

        /// <summary>
        /// Событие изменения состояния виртуальной клавиатуры
        /// </summary>
        event EventHandler<VirtualKeyboardStateChangedEventArgs>? StateChanged;
    }

    /// <summary>
    /// Аргументы события изменения состояния виртуальной клавиатуры
    /// </summary>
    public class VirtualKeyboardStateChangedEventArgs : EventArgs
    {
        public bool IsVisible { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Message { get; set; }
    }
}
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsLauncher.UI.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel для диалога ввода текста
    /// </summary>
    public class InputDialogViewModel : INotifyPropertyChanged
    {
        private string _inputValue = string.Empty;
        private readonly string _defaultValue;

        /// <summary>
        /// Сообщение для пользователя
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Заголовок диалога
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Введенное пользователем значение
        /// </summary>
        public string InputValue
        {
            get => _inputValue;
            set
            {
                if (SetProperty(ref _inputValue, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        /// <summary>
        /// Проверка валидности введенного значения
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(InputValue);

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="message">Сообщение для пользователя</param>
        /// <param name="title">Заголовок диалога</param>
        /// <param name="defaultValue">Значение по умолчанию</param>
        public InputDialogViewModel(string message, string title = "Ввод", string defaultValue = "")
        {
            Message = message ?? string.Empty;
            Title = title ?? "Ввод";
            _defaultValue = defaultValue ?? string.Empty;
            _inputValue = _defaultValue;
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
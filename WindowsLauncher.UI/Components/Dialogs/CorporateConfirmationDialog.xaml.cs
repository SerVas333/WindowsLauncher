using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
// using MaterialDesignThemes.Wpf; // Убрано - не используем MaterialDesignThemes
using WindowsLauncher.UI.Infrastructure.Localization;

namespace WindowsLauncher.UI.Components.Dialogs
{
    /// <summary>
    /// Корпоративный диалог подтверждения с Material Design стилизацией
    /// </summary>
    public partial class CorporateConfirmationDialog : Window, INotifyPropertyChanged
    {
        #region Fields

        private string _dialogTitle = "";
        private string _dialogMessage = "";
        private string _dialogDetails = "";
        private string _confirmButtonText = "Да";
        private string _cancelButtonText = "Отмена";
        private string _dialogIcon = "⚠️"; // Используем эмодзи вместо PackIconKind

        #endregion

        #region Properties

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string DialogMessage
        {
            get => _dialogMessage;
            set => SetProperty(ref _dialogMessage, value);
        }

        public string DialogDetails
        {
            get => _dialogDetails;
            set => SetProperty(ref _dialogDetails, value);
        }

        public string ConfirmButtonText
        {
            get => _confirmButtonText;
            set => SetProperty(ref _confirmButtonText, value);
        }

        public string CancelButtonText
        {
            get => _cancelButtonText;
            set => SetProperty(ref _cancelButtonText, value);
        }

        public string DialogIcon
        {
            get => _dialogIcon;
            set => SetProperty(ref _dialogIcon, value);
        }

        public bool HasDetails => !string.IsNullOrEmpty(DialogDetails);

        #endregion

        #region Constructors

        public CorporateConfirmationDialog()
        {
            InitializeComponent();
            DataContext = this;
            
            // Подписываемся на изменение языка для динамического обновления
            LocalizationHelper.Instance.LanguageChanged += OnLanguageChanged;
        }

        public CorporateConfirmationDialog(string title, string message, string details = "", 
            string confirmText = "", string cancelText = "", 
            string iconEmoji = "⚠️") : this()
        {
            DialogTitle = title;
            DialogMessage = message;
            DialogDetails = details;
            
            // Используем локализованные значения по умолчанию
            ConfirmButtonText = string.IsNullOrEmpty(confirmText) 
                ? LocalizationHelper.Instance.GetString("Dialog_Yes") 
                : confirmText;
            CancelButtonText = string.IsNullOrEmpty(cancelText) 
                ? LocalizationHelper.Instance.GetString("Common_Cancel") 
                : cancelText;
                
            DialogIcon = iconEmoji;
        }

        #endregion

        #region Event Handlers

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // ИСПРАВЛЕНИЕ: Устанавливаем стандартное WPF DialogResult для корректного ShowDialog()
            this.DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // ИСПРАВЛЕНИЕ: Устанавливаем стандартное WPF DialogResult для корректного ShowDialog()
            this.DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ИСПРАВЛЕНИЕ: Устанавливаем стандартное WPF DialogResult для корректного ShowDialog()
            this.DialogResult = false;
            Close();
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Показать диалог подтверждения выхода из системы
        /// </summary>
        public static bool ShowLogoutConfirmation(Window? owner = null)
        {
            var dialog = new CorporateConfirmationDialog(
                title: LocalizationHelper.Instance.GetString("Dialog_LogoutTitle"),
                message: LocalizationHelper.Instance.GetString("Dialog_LogoutMessage"),
                details: LocalizationHelper.Instance.GetString("Dialog_LogoutDetails"),
                confirmText: LocalizationHelper.Instance.GetString("Dialog_Confirm"),
                cancelText: LocalizationHelper.Instance.GetString("Common_Cancel"),
                iconEmoji: "🚪" // Эмодзи двери для выхода
            );

            if (owner != null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        /// <summary>
        /// Показать диалог подтверждения удаления
        /// </summary>
        public static bool ShowDeleteConfirmation(string itemName, Window? owner = null)
        {
            var dialog = new CorporateConfirmationDialog(
                title: LocalizationHelper.Instance.GetString("Dialog_DeleteTitle"),
                message: string.Format(LocalizationHelper.Instance.GetString("Dialog_DeleteMessage"), itemName),
                details: LocalizationHelper.Instance.GetString("Dialog_DeleteDetails"),
                confirmText: LocalizationHelper.Instance.GetString("Dialog_Delete"),
                cancelText: LocalizationHelper.Instance.GetString("Common_Cancel"),
                iconEmoji: "🗑️" // Эмодзи корзины для удаления
            );

            if (owner != null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        /// <summary>
        /// Показать общий диалог подтверждения
        /// </summary>
        public static bool ShowConfirmation(string title, string message, string details = "",
            string confirmText = "", string cancelText = "", 
            string iconEmoji = "⚠️", Window? owner = null)
        {
            // Используем локализованные значения по умолчанию
            if (string.IsNullOrEmpty(confirmText))
                confirmText = LocalizationHelper.Instance.GetString("Dialog_Yes");
            if (string.IsNullOrEmpty(cancelText))
                cancelText = LocalizationHelper.Instance.GetString("Common_Cancel");
                
            var dialog = new CorporateConfirmationDialog(title, message, details, confirmText, cancelText, iconEmoji);

            if (owner != null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Localization Support

        /// <summary>
        /// Обработчик смены языка для динамического обновления текстов
        /// </summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Обновляем все свойства для обновления привязок
            OnPropertyChanged(nameof(DialogTitle));
            OnPropertyChanged(nameof(DialogMessage));
            OnPropertyChanged(nameof(DialogDetails));
            OnPropertyChanged(nameof(ConfirmButtonText));
            OnPropertyChanged(nameof(CancelButtonText));
            OnPropertyChanged(nameof(HasDetails));
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // Отписываемся от событий для предотвращения утечек памяти
            LocalizationHelper.Instance.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }

        #endregion
    }
}
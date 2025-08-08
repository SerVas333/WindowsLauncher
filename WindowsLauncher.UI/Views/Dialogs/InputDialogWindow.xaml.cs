using System.Windows;
using System.Windows.Input;
using WindowsLauncher.UI.ViewModels.Dialogs;

namespace WindowsLauncher.UI.Views.Dialogs
{
    /// <summary>
    /// Диалоговое окно для ввода текста
    /// </summary>
    public partial class InputDialogWindow : Window
    {
        public InputDialogViewModel ViewModel { get; }

        /// <summary>
        /// Результат диалога - введенный текст или null если отменено
        /// </summary>
        public string? InputResult { get; private set; }

        public InputDialogWindow(string message, string title = "Ввод", string defaultValue = "")
        {
            InitializeComponent();
            
            ViewModel = new InputDialogViewModel(message, title, defaultValue);
            DataContext = ViewModel;
            
            // Фокус на поле ввода при загрузке
            Loaded += (s, e) => InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputResult = ViewModel.InputValue;
            this.DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            InputResult = null;
            this.DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ViewModel.IsValid)
            {
                InputResult = ViewModel.InputValue;
                this.DialogResult = true;
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                InputResult = null;
                this.DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// Статический метод для показа диалога
        /// </summary>
        public static string? ShowDialog(string message, string title = "Ввод", string defaultValue = "", Window? owner = null)
        {
            var dialog = new InputDialogWindow(message, title, defaultValue);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();
            return result == true ? dialog.InputResult : null;
        }
    }
}
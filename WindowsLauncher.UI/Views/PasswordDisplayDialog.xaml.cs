// WindowsLauncher.UI/Views/PasswordDisplayDialog.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Диалог для отображения сгенерированного пароля с возможностью копирования
    /// </summary>
    public partial class PasswordDisplayDialog : Window
    {
        public string Password { get; }

        public PasswordDisplayDialog(string password)
        {
            InitializeComponent();
            Password = password;
            DataContext = this;
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(Password);
                
                // Временно меняем текст кнопки для обратной связи
                if (sender is Button button)
                {
                    var originalContent = button.Content;
                    button.Content = "✓";
                    button.IsEnabled = false;
                    
                    // Возвращаем исходный вид через 1 секунду
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(1);
                    timer.Tick += (s, args) =>
                    {
                        button.Content = originalContent;
                        button.IsEnabled = true;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось скопировать пароль: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
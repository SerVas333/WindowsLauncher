using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно отображения результатов диагностики системы
    /// </summary>
    public partial class DiagnosticsWindow : Window
    {
        private readonly string _diagnosticsText;

        public DiagnosticsWindow(string diagnosticsText)
        {
            InitializeComponent();
            _diagnosticsText = diagnosticsText ?? string.Empty;
            DiagnosticsTextBox.Text = _diagnosticsText;
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_diagnosticsText);
                MessageBox.Show("Результаты диагностики скопированы в буфер обмена", 
                               "Скопировано", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка копирования в буфер: {ex.Message}", 
                               "Ошибка", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void SaveToFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Сохранить результаты диагностики",
                    Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"WindowsLauncher_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, _diagnosticsText);
                    MessageBox.Show($"Результаты диагностики сохранены в:\n{saveDialog.FileName}", 
                                   "Сохранено", 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", 
                               "Ошибка", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
// WindowsLauncher.UI/AdminWindow.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI
{
    /// <summary>
    /// Окно администрирования приложений
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly AdminViewModel _viewModel;

        public AdminWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            // Получаем ViewModel через DI
            _viewModel = serviceProvider.GetRequiredService<AdminViewModel>();
            DataContext = _viewModel;

            // Подписываемся на события закрытия
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Проверяем несохраненные изменения
            if (_viewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "У вас есть несохраненные изменения. Вы действительно хотите закрыть окно?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Очищаем ресурсы
            await _viewModel.CleanupAsync();
            _viewModel.Dispose();
        }

        private void BrowseExecutablePath_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.EditingApplication == null) return;

            var dialog = new OpenFileDialog();

            // Настраиваем диалог в зависимости от типа приложения
            switch (_viewModel.EditingApplication.Type)
            {
                case Core.Enums.ApplicationType.Desktop:
                    dialog.Filter = "Исполняемые файлы (*.exe)|*.exe|Пакетные файлы (*.bat;*.cmd)|*.bat;*.cmd|Все файлы (*.*)|*.*";
                    dialog.Title = "Выберите исполняемый файл";
                    break;

                case Core.Enums.ApplicationType.Web:
                    // Для веб-ссылок показываем диалог ввода URL
                    ShowUrlInputDialog();
                    return;

                case Core.Enums.ApplicationType.Folder:
                    // Для папок используем FolderBrowserDialog
                    ShowFolderBrowserDialog();
                    return;
            }

            if (dialog.ShowDialog() == true)
            {
                _viewModel.EditingApplication.ExecutablePath = dialog.FileName;
            }
        }

        private void ShowUrlInputDialog()
        {
            var currentUrl = _viewModel.EditingApplication?.ExecutablePath ?? "https://";

            var dialog = new Window
            {
                Title = "Введите URL",
                Width = 500,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Style = Application.Current.MainWindow?.FindResource("CorporateWindowStyle") as Style
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Введите URL адрес:",
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = currentUrl,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 3);

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    // Простая валидация URL
                    if (!textBox.Text.StartsWith("http://") && !textBox.Text.StartsWith("https://"))
                    {
                        textBox.Text = "https://" + textBox.Text;
                    }

                    if (_viewModel.EditingApplication != null)
                    {
                        _viewModel.EditingApplication.ExecutablePath = textBox.Text;
                    }
                }
                dialog.DialogResult = true;
            };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 80,
                IsCancel = true
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            // Фокус на текстовое поле и выделение всего текста
            textBox.Focus();
            textBox.SelectAll();

            dialog.ShowDialog();
        }

        private void ShowFolderBrowserDialog()
        {
            // Используем альтернативный способ выбора папки через Win32
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Выберите папку",
                FileName = "Выбор папки",
                Filter = "Папка|*.none",
                CheckFileExists = false,
                CheckPathExists = true,
                OverwritePrompt = false,
                InitialDirectory = _viewModel.EditingApplication?.ExecutablePath ?? ""
            };

            // Хак для выбора папки через SaveFileDialog
            dialog.FileName = "Folder Selection";

            if (dialog.ShowDialog() == true)
            {
                // Получаем только путь к папке
                var folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);

                if (!string.IsNullOrEmpty(folderPath) && _viewModel.EditingApplication != null)
                {
                    _viewModel.EditingApplication.ExecutablePath = folderPath;
                }
            }
        }

        // Дополнительные вспомогательные методы

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Применяем корпоративный стиль к окну
            ApplyCorporateWindowStyle();
        }

        private void ApplyCorporateWindowStyle()
        {
            // Здесь можно настроить дополнительные стили окна
            // Например, убрать стандартную рамку Windows и добавить свою
        }
    }
}
// WindowsLauncher.UI/AdminWindow.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Android;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Окно администрирования приложений
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly AdminViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;

        public AdminWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _serviceProvider = serviceProvider;

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

                case Core.Enums.ApplicationType.Android:
                    // Для Android приложений используем специальный APK диалог
                    ShowApkFileDialog();
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
                Style = Application.Current.MainWindow?.FindResource("WindowStyle") as Style
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

        private async void VirtualKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var virtualKeyboardService = _serviceProvider.GetRequiredService<IVirtualKeyboardService>();
                await virtualKeyboardService.ToggleVirtualKeyboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переключении виртуальной клавиатуры: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BrowseApkFile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.EditingApplication == null) return;

            // Проверяем доступность Android подсистемы
            if (!_viewModel.IsAndroidEnabled)
            {
                MessageBox.Show(
                    "Android подсистема отключена в конфигурации.\nДля работы с APK файлами включите режим OnDemand или Preload в appsettings.json",
                    "Android недоступен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите APK/XAPK файл",
                Filter = "Android файлы|*.apk;*.xapk|APK файлы (*.apk)|*.apk|XAPK файлы (*.xapk)|*.xapk|Все файлы (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.EditingApplication.ApkFilePath = dialog.FileName;
                
                // Автоматическое извлечение метаданных APK
                await ExtractApkMetadataAsync(dialog.FileName);
            }
        }

        private async void ShowApkFileDialog()
        {
            // Проверяем доступность Android подсистемы
            if (!_viewModel.IsAndroidEnabled)
            {
                MessageBox.Show(
                    "Android подсистема отключена в конфигурации.\nДля работы с APK файлами включите режим OnDemand или Preload в appsettings.json",
                    "Android недоступен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите APK/XAPK файл",
                Filter = "Android файлы|*.apk;*.xapk|APK файлы (*.apk)|*.apk|XAPK файлы (*.xapk)|*.xapk|Все файлы (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                if (_viewModel.EditingApplication != null)
                {
                    _viewModel.EditingApplication.ExecutablePath = dialog.FileName;
                    _viewModel.EditingApplication.ApkFilePath = dialog.FileName;
                    
                    // Автоматическое извлечение метаданных APK
                    await ExtractApkMetadataAsync(dialog.FileName);
                }
            }
        }

        /// <summary>
        /// Извлекает метаданные из APK файла и заполняет поля автоматически
        /// </summary>
        private async Task ExtractApkMetadataAsync(string apkFilePath)
        {
            if (_viewModel.EditingApplication == null || string.IsNullOrEmpty(apkFilePath))
                return;

            // Проверяем доступность Android подсистемы
            if (!_viewModel.IsAndroidEnabled)
            {
                MessageBox.Show(
                    "Android подсистема отключена. Метаданные APK недоступны.",
                    "Android недоступен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Получаем Android сервис через DI
                var androidManager = _serviceProvider.GetRequiredService<IAndroidApplicationManager>();
                
                // Извлекаем метаданные из APK
                var metadata = await androidManager.ExtractApkMetadataAsync(apkFilePath);
                
                if (metadata != null && metadata.IsValid())
                {
                    // Заполняем поля автоматически через ViewModel свойства (строковые для UI биндинга)
                    _viewModel.EditingApplication.ApkPackageName = metadata.PackageName;
                    _viewModel.EditingApplication.ApkVersionCode = metadata.VersionCode > 0 ? metadata.VersionCode.ToString() : null;
                    _viewModel.EditingApplication.ApkVersionName = metadata.VersionName;
                    _viewModel.EditingApplication.ApkMinSdk = metadata.MinSdkVersion > 0 ? metadata.MinSdkVersion.ToString() : null;
                    _viewModel.EditingApplication.ApkTargetSdk = metadata.TargetSdkVersion > 0 ? metadata.TargetSdkVersion.ToString() : null;
                    
                    // Если имя приложения пустое, используем имя из APK
                    if (string.IsNullOrEmpty(_viewModel.EditingApplication.Name) && !string.IsNullOrEmpty(metadata.AppName))
                    {
                        _viewModel.EditingApplication.Name = metadata.AppName;
                    }
                    
                    // Устанавливаем категорию "Android" если пустая
                    if (string.IsNullOrEmpty(_viewModel.EditingApplication.Category) || _viewModel.EditingApplication.Category == "General")
                    {
                        _viewModel.EditingApplication.Category = "Android";
                    }
                    
                    // Устанавливаем статус установки
                    _viewModel.EditingApplication.ApkInstallStatus = "NotInstalled";
                    
                    // Показываем сообщение об успехе
                    MessageBox.Show(
                        $"Метаданные APK успешно извлечены:\n" +
                        $"Пакет: {metadata.PackageName}\n" +
                        $"Версия: {metadata.VersionName} ({metadata.VersionCode})\n" +
                        $"SDK: {metadata.MinSdkVersion} - {metadata.TargetSdkVersion}",
                        "APK Метаданные", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Не удалось извлечь метаданные из выбранного APK файла.\nВозможно, файл поврежден или имеет неподдерживаемый формат.",
                        "Ошибка APK",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при извлечении метаданных APK:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Обработчик кнопки UI Demo
        /// </summary>
        private void UIDemoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Открываем UI Demo окно как дочернее
                UIDemoWindow.ShowDemo(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия UI Demo: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обработчик кнопки диагностики Android
        /// </summary>
        private async void DiagnoseAndroidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем доступность Android подсистемы
                if (!_viewModel.IsAndroidEnabled)
                {
                    MessageBox.Show(
                        "Android подсистема отключена в конфигурации.\nДля использования Android функций включите режим OnDemand или Preload в appsettings.json",
                        "Android недоступен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Получаем Android сервис через DI
                var androidManager = _serviceProvider.GetRequiredService<IAndroidApplicationManager>();
                
                // Отключаем кнопку во время выполнения
                if (sender is Button button)
                {
                    button.IsEnabled = false;
                }

                // Показываем индикатор загрузки
                Mouse.OverrideCursor = Cursors.Wait;

                // Выполняем диагностику
                string diagnosticsResult = await androidManager.RunAndroidDiagnosticsAsync();

                // Показываем результаты в диалоге с прокруткой
                ShowAndroidDiagnosticsDialog(diagnosticsResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при выполнении диагностики Android:\n{ex.Message}",
                    "Ошибка диагностики",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Восстанавливаем состояние UI
                Mouse.OverrideCursor = null;
                
                if (sender is Button button)
                {
                    button.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Показывает результаты диагностики Android в отдельном диалоге
        /// </summary>
        private void ShowAndroidDiagnosticsDialog(string diagnosticsText)
        {
            var dialog = new Window
            {
                Title = "Диагностика Android",
                Width = 650,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Style = Application.Current.MainWindow?.FindResource("WindowStyle") as Style
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Заголовок
            var header = new TextBlock
            {
                Text = "🤖 Результаты диагностики Android",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Application.Current.MainWindow?.FindResource("PrimaryBrush") as Brush
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Текст результатов с прокруткой
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var textBox = new TextBox
            {
                Text = diagnosticsText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            scrollViewer.Content = textBox;
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            // Панель кнопок
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);

            // Кнопка копирования в буфер обмена
            var copyButton = new Button
            {
                Content = "📋 Копировать",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0),
                Style = Application.Current.MainWindow?.FindResource("SecondaryButtonStyle") as Style
            };
            copyButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(diagnosticsText);
                    MessageBox.Show("Результаты диагностики скопированы в буфер обмена!", 
                        "Скопировано", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка копирования: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(copyButton);

            // Кнопка обновления
            var refreshButton = new Button
            {
                Content = "🔄 Обновить",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0),
                Style = Application.Current.MainWindow?.FindResource("SecondaryButtonStyle") as Style
            };
            refreshButton.Click += async (s, e) =>
            {
                try
                {
                    refreshButton.IsEnabled = false;
                    Mouse.OverrideCursor = Cursors.Wait;

                    var androidManager = _serviceProvider.GetRequiredService<IAndroidApplicationManager>();
                    string newDiagnostics = await androidManager.RunAndroidDiagnosticsAsync();
                    textBox.Text = newDiagnostics;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка обновления: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                    refreshButton.IsEnabled = true;
                }
            };
            buttonPanel.Children.Add(refreshButton);

            // Кнопка закрытия
            var closeButton = new Button
            {
                Content = "Закрыть",
                Width = 100,
                IsDefault = true,
                Style = Application.Current.MainWindow?.FindResource("PrimaryButtonStyle") as Style
            };
            closeButton.Click += (s, e) => dialog.Close();
            buttonPanel.Children.Add(closeButton);

            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            dialog.ShowDialog();
        }
    }
}
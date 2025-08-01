using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowsLauncher.Core.Models;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Components.TextEditor
{
    /// <summary>
    /// Аргументы для настройки функциональности текстового редактора
    /// </summary>
    public class TextEditorArguments
    {
        public bool AllowOpenFiles { get; set; } = true;      // --notopen отключает
        public bool AllowSaveFiles { get; set; } = true;     // --notsave отключает
        public bool AllowPrint { get; set; } = true;         // --notprint отключает
        public bool AllowFormatting { get; set; } = true;    // --notformat отключает
        public bool ReadOnlyMode { get; set; } = false;      // --readonly включает
        public bool ShowToolbar { get; set; } = true;        // --notoolbar отключает
        public bool ShowStatusbar { get; set; } = true;      // --nostatusbar отключает
        public string InitialFilePath { get; set; } = string.Empty;
        
        public static TextEditorArguments Parse(string? argumentsString)
        {
            var args = new TextEditorArguments();
            
            if (string.IsNullOrEmpty(argumentsString))
            {
                System.Diagnostics.Debug.WriteLine("[TextEditor] No arguments provided");
                return args;
            }
            
            System.Diagnostics.Debug.WriteLine($"[TextEditor] Parsing arguments: '{argumentsString}'");
            
            // Правильный парсинг аргументов с поддержкой кавычек
            var parts = ParseCommandLineArguments(argumentsString);
            System.Diagnostics.Debug.WriteLine($"[TextEditor] Parsed {parts.Count} argument parts: [{string.Join(", ", parts.Select(p => $"'{p}'"))}]");
            
            foreach (var part in parts)
            {
                var arg = part.ToLowerInvariant();
                switch (arg)
                {
                    case "--notopen":
                    case "--no-open":
                        args.AllowOpenFiles = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: AllowOpenFiles = false");
                        break;
                    case "--notsave":
                    case "--no-save":
                        args.AllowSaveFiles = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: AllowSaveFiles = false");
                        break;
                    case "--notprint":
                    case "--no-print":
                        args.AllowPrint = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: AllowPrint = false");
                        break;
                    case "--notformat":
                    case "--no-format":
                        args.AllowFormatting = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: AllowFormatting = false");
                        break;
                    case "--readonly":
                    case "--read-only":
                        args.ReadOnlyMode = true;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: ReadOnlyMode = true");
                        break;
                    case "--notoolbar":
                    case "--no-toolbar":
                        args.ShowToolbar = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: ShowToolbar = false");
                        break;
                    case "--nostatusbar":
                    case "--no-statusbar":
                        args.ShowStatusbar = false;
                        System.Diagnostics.Debug.WriteLine("[TextEditor] Flag: ShowStatusbar = false");
                        break;
                    default:
                        // Если это не флаг, считаем путем к файлу
                        if (!arg.StartsWith("--") && !arg.StartsWith("-"))
                        {
                            // Используем оригинальный part (не lowered) для пути к файлу
                            args.InitialFilePath = part.Trim('"');
                            System.Diagnostics.Debug.WriteLine($"[TextEditor] File path detected: '{args.InitialFilePath}'");
                        }
                        break;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[TextEditor] Final parsed arguments: FilePath='{args.InitialFilePath}', ReadOnly={args.ReadOnlyMode}, AllowOpen={args.AllowOpenFiles}, AllowSave={args.AllowSaveFiles}");
            return args;
        }
        
        /// <summary>
        /// Парсит командную строку с поддержкой кавычек
        /// </summary>
        private static List<string> ParseCommandLineArguments(string commandLine)
        {
            var args = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                
                if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                
                // Добавляем все символы включая обратные слеши
                current.Append(c);
            }
            
            if (current.Length > 0)
            {
                args.Add(current.ToString());
            }
            
            return args;
        }
        
        public override string ToString()
        {
            var flags = new List<string>();
            if (!AllowOpenFiles) flags.Add("--notopen");
            if (!AllowSaveFiles) flags.Add("--notsave");
            if (!AllowPrint) flags.Add("--notprint");
            if (!AllowFormatting) flags.Add("--notformat");
            if (ReadOnlyMode) flags.Add("--readonly");
            if (!ShowToolbar) flags.Add("--notoolbar");
            if (!ShowStatusbar) flags.Add("--nostatusbar");
            
            var result = string.Join(" ", flags);
            if (!string.IsNullOrEmpty(InitialFilePath))
            {
                result += $" \"{InitialFilePath}\"";
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Встроенный текстовый редактор с XAML UI и MVVM паттерном
    /// </summary>
    public partial class TextEditorWindow : Window
    {
        private readonly CoreApplication _application;
        private readonly string _launchedBy;
        private readonly TextEditorViewModel _viewModel;
        private string _originalContent = string.Empty;
        private string _originalFilePath; // Путь к оригинальному файлу для защиты от перезаписи

        #region Public Properties

        public string InstanceId { get; private set; }
        public CoreApplication Application => _application;
        public string FilePath => _viewModel.FilePath;
        public bool IsClosed { get; private set; }
        public bool IsReadOnlyMode => _viewModel.IsReadOnlyMode;

        #endregion

        #region Constructor

        public TextEditorWindow(CoreApplication application, string filePath, string launchedBy, string instanceId = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            var arguments = TextEditorArguments.Parse(application.Arguments);
            
            // Определяем итоговый путь к файлу
            var finalPath = !string.IsNullOrEmpty(arguments.InitialFilePath) 
                ? arguments.InitialFilePath 
                : filePath ?? string.Empty;
                
            _originalFilePath = finalPath; // Сохраняем оригинальный путь
            _launchedBy = launchedBy;
            
            // Используем переданный instanceId или генерируем новый
            InstanceId = instanceId ?? $"texteditor_{application.Id}_{Guid.NewGuid():N}";
            
            // Создаем ViewModel
            _viewModel = new TextEditorViewModel(arguments, application);
            _viewModel.SetFilePath(finalPath);
            
            InitializeComponent();
            
            // Привязываем ViewModel к окну
            DataContext = _viewModel;
            
            // Инициализируем окно
            SetupWindow();
            LoadFileIfExists();
            UpdateReadOnlyMode();
            
            Closing += TextEditorWindow_Closing;
            Closed += (s, e) => IsClosed = true;
        }

        #endregion

        #region Window Setup

        private void SetupWindow()
        {
            // Привязываем заголовок окна
            SetBinding(TitleProperty, new System.Windows.Data.Binding("WindowTitle"));
            
            // Если есть MainEditor в XAML, подписываемся на его события
            if (MainEditor != null)
            {
                MainEditor.TextChanged += MainEditor_TextChanged;
                MainEditor.SelectionChanged += MainEditor_SelectionChanged;
            }
        }

        private void LoadFileIfExists()
        {
            var filePath = _viewModel.FilePath;
            
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                if (MainEditor != null)
                {
                    MainEditor.Document = new FlowDocument(new Paragraph(new Run("")));
                }
                _originalContent = string.Empty;
                return;
            }
            
            try
            {
                if (MainEditor != null)
                {
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    
                    if (extension == ".rtf")
                    {
                        // Загружаем RTF файл с сохранением форматирования
                        var rtfContent = File.ReadAllText(filePath, Encoding.UTF8);
                        var flowDocument = new FlowDocument();
                        var textRange = new TextRange(flowDocument.ContentStart, flowDocument.ContentEnd);
                        
                        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtfContent)))
                        {
                            textRange.Load(stream, DataFormats.Rtf);
                        }
                        
                        MainEditor.Document = flowDocument;
                        _originalContent = rtfContent;
                        _viewModel.StatusText = "RTF файл загружен с форматированием";
                    }
                    else
                    {
                        // Загружаем как обычный текст
                        var content = File.ReadAllText(filePath, Encoding.UTF8);
                        MainEditor.Document = new FlowDocument(new Paragraph(new Run(content)));
                        _originalContent = content;
                        _viewModel.StatusText = "Текстовый файл загружен";
                    }
                }
                
                _viewModel.SetModified(false);
                
                // Проверяем доступность файла для записи
                CheckFileWriteAccess();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _viewModel.StatusText = "Ошибка загрузки файла";
            }
        }

        private void CheckFileWriteAccess()
        {
            var filePath = _viewModel.FilePath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;
            
            try
            {
                // Проверяем атрибуты файла
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    _viewModel.SetReadOnlyMode(true, "Файл доступен только для чтения");
                    return;
                }
                
                // Пытаемся открыть файл для записи
                using (var stream = File.OpenWrite(filePath))
                {
                    // Файл доступен для записи
                }
            }
            catch (UnauthorizedAccessException)
            {
                _viewModel.SetReadOnlyMode(true, "Недостаточно прав для изменения файла");
            }
            catch (Exception)
            {
                _viewModel.SetReadOnlyMode(true, "Файл недоступен для записи");
            }
        }

        private void UpdateReadOnlyMode()
        {
            if (MainEditor != null)
            {
                MainEditor.IsReadOnly = _viewModel.IsReadOnlyMode;
                MainEditor.Background = _viewModel.IsReadOnlyMode ? Brushes.Lavender : Brushes.White;
            }
        }

        #endregion

        #region Event Handlers - Text Editor

        private void MainEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel.IsReadOnlyMode || MainEditor == null) 
                return;
            
            var currentContent = new TextRange(MainEditor.Document.ContentStart, MainEditor.Document.ContentEnd).Text;
            var isModified = currentContent != _originalContent;
            _viewModel.SetModified(isModified);
        }

        private void MainEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (MainEditor == null) return;
            
            // Обновляем информацию о позиции курсора
            var caretPos = MainEditor.CaretPosition;
            var paragraph = caretPos.Paragraph;
            
            if (paragraph != null)
            {
                var doc = MainEditor.Document;
                var blocks = doc.Blocks.ToList();
                var lineNumber = blocks.IndexOf(paragraph) + 1;
                
                // Примерное вычисление столбца
                var textRange = new TextRange(paragraph.ContentStart, caretPos);
                var columnNumber = textRange.Text.Length + 1;
                
                _viewModel.UpdateCursorPosition(lineNumber, columnNumber);
            }
        }

        private void MainEditor_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // Перехватываем команды форматирования если они запрещены
            if (!_viewModel.AllowFormatting)
            {
                if (e.Command == EditingCommands.ToggleBold ||
                    e.Command == EditingCommands.ToggleItalic ||
                    e.Command == EditingCommands.ToggleUnderline)
                {
                    e.Handled = true;
                }
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainEditor != null && sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                if (double.TryParse(item.Content.ToString(), out double fontSize))
                {
                    MainEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
                }
            }
        }

        #endregion

        #region Command Handlers

        // Команды файлов
        private void NewFile_Executed(object sender, ExecutedRoutedEventArgs e) => NewFile();
        private void NewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowOpenFiles;

        private void OpenFile_Executed(object sender, ExecutedRoutedEventArgs e) => OpenFile();
        private void OpenFile_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowOpenFiles;

        private void SaveFile_Executed(object sender, ExecutedRoutedEventArgs e) => SaveFile();
        private void SaveFile_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowSaveFiles && !_viewModel.IsReadOnlyMode;

        private void SaveFileAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveFileAs();
        private void SaveFileAs_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowSaveFiles;

        private void PrintDocument_Executed(object sender, ExecutedRoutedEventArgs e) => PrintDocument();
        private void PrintDocument_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowPrint;

        private void FindText_Executed(object sender, ExecutedRoutedEventArgs e) => FindText();
        private void FindText_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = true;

        // Команды форматирования
        private void ToggleBold_Executed(object sender, ExecutedRoutedEventArgs e) => 
            MainEditor?.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
        private void ToggleBold_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowFormatting && !_viewModel.IsReadOnlyMode;

        private void ToggleItalic_Executed(object sender, ExecutedRoutedEventArgs e) => 
            MainEditor?.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        private void ToggleItalic_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowFormatting && !_viewModel.IsReadOnlyMode;

        private void ToggleUnderline_Executed(object sender, ExecutedRoutedEventArgs e) => 
            MainEditor?.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        private void ToggleUnderline_CanExecute(object sender, CanExecuteRoutedEventArgs e) => 
            e.CanExecute = _viewModel.AllowFormatting && !_viewModel.IsReadOnlyMode;

        #endregion

        #region Click Handlers

        private void PrintPreview_Click(object sender, RoutedEventArgs e) => PrintPreview();
        private void PageSetup_Click(object sender, RoutedEventArgs e) => PageSetup();
        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
        private void ChangeTextColor_Click(object sender, RoutedEventArgs e) => ChangeTextColor();
        private void ChangeTextBackground_Click(object sender, RoutedEventArgs e) => ChangeTextBackground();
        private void ChooseFont_Click(object sender, RoutedEventArgs e) => ChooseFont();
        private void ToggleReadOnly_Click(object sender, RoutedEventArgs e) => ToggleReadOnlyMode();
        private void ChangeZoom_Click(object sender, RoutedEventArgs e) => ChangeZoom();

        #endregion

        #region File Operations

        private void NewFile()
        {
            if (CheckSaveChanges())
            {
                if (MainEditor != null)
                {
                    MainEditor.Document = new FlowDocument(new Paragraph(new Run("")));
                }
                _viewModel.SetFilePath(string.Empty);
                _originalFilePath = string.Empty;
                _originalContent = string.Empty;
                _viewModel.SetModified(false);
                _viewModel.SetReadOnlyMode(false);
                UpdateReadOnlyMode();
                _viewModel.StatusText = "Новый документ";
            }
        }

        private void OpenFile()
        {
            if (!CheckSaveChanges()) return;
            
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|RTF файлы (*.rtf)|*.rtf|Все файлы (*.*)|*.*",
                Title = "Открыть файл"
            };
            
            if (dialog.ShowDialog() == true)
            {
                _viewModel.SetFilePath(dialog.FileName);
                _originalFilePath = dialog.FileName; // Обновляем оригинальный путь
                LoadFileIfExists();
                UpdateReadOnlyMode();
            }
        }

        private void SaveFile()
        {
            if (_viewModel.IsReadOnlyMode)
            {
                MessageBox.Show("Файл доступен только для чтения. Используйте 'Сохранить как...'", 
                    "Режим только чтения", MessageBoxButton.OK, MessageBoxImage.Information);
                SaveFileAs();
                return;
            }
            
            var filePath = _viewModel.FilePath;
            
            // Защита от перезаписи оригинала
            if (!string.IsNullOrEmpty(_originalFilePath) && 
                string.Equals(filePath, _originalFilePath, StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(
                    "Вы пытаетесь перезаписать оригинальный файл. Это может привести к потере данных.\n\n" +
                    "Рекомендуется сохранить под другим именем.\n\n" +
                    "Продолжить перезапись оригинала?",
                    "Предупреждение о перезаписи",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    SaveFileAs();
                    return;
                }
            }
            
            if (string.IsNullOrEmpty(filePath))
            {
                SaveFileAs();
                return;
            }
            
            try
            {
                if (MainEditor == null) return;
                
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Создаем резервную копию если перезаписываем оригинал
                if (!string.IsNullOrEmpty(_originalFilePath) && 
                    string.Equals(filePath, _originalFilePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(_originalFilePath))
                {
                    var backupPath = _originalFilePath + ".backup";
                    File.Copy(_originalFilePath, backupPath, true);
                    _viewModel.StatusText = $"Создана резервная копия: {Path.GetFileName(backupPath)}";
                }
                
                if (extension == ".rtf")
                {
                    // Сохраняем в RTF формате с форматированием
                    var textRange = new TextRange(MainEditor.Document.ContentStart, MainEditor.Document.ContentEnd);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        textRange.Save(fileStream, DataFormats.Rtf);
                    }
                    
                    // Обновляем _originalContent для отслеживания изменений
                    var rtfContent = File.ReadAllText(filePath, Encoding.UTF8);
                    _originalContent = rtfContent;
                    _viewModel.StatusText = "RTF файл сохранен с форматированием";
                }
                else
                {
                    // Сохраняем как обычный текст
                    var content = new TextRange(MainEditor.Document.ContentStart, MainEditor.Document.ContentEnd).Text;
                    File.WriteAllText(filePath, content, Encoding.UTF8);
                    _originalContent = content;
                    _viewModel.StatusText = "Текстовый файл сохранен";
                }
                
                _viewModel.SetModified(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить файл:\n{ex.Message}", "Ошибка сохранения", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveFileAs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|RTF файлы (*.rtf)|*.rtf|Все файлы (*.*)|*.*",
                Title = "Сохранить файл как",
                FileName = Path.GetFileName(_viewModel.FilePath)
            };
            
            // Предлагаем имя с суффиксом если это копия оригинала
            if (!string.IsNullOrEmpty(_originalFilePath))
            {
                var originalName = Path.GetFileNameWithoutExtension(_originalFilePath);
                var extension = Path.GetExtension(_originalFilePath);
                dialog.FileName = $"{originalName}_копия{extension}";
            }
            
            if (dialog.ShowDialog() == true)
            {
                var oldFilePath = _viewModel.FilePath;
                _viewModel.SetFilePath(dialog.FileName);
                
                try
                {
                    if (MainEditor == null) return;
                    
                    var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    
                    if (extension == ".rtf")
                    {
                        // Сохраняем в RTF формате с форматированием
                        var textRange = new TextRange(MainEditor.Document.ContentStart, MainEditor.Document.ContentEnd);
                        using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
                        {
                            textRange.Save(fileStream, DataFormats.Rtf);
                        }
                        
                        // Обновляем _originalContent для отслеживания изменений
                        var rtfContent = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                        _originalContent = rtfContent;
                        _viewModel.StatusText = "RTF файл сохранен как новый с форматированием";
                    }
                    else
                    {
                        // Сохраняем как обычный текст
                        var content = new TextRange(MainEditor.Document.ContentStart, MainEditor.Document.ContentEnd).Text;
                        File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
                        _originalContent = content;
                        _viewModel.StatusText = "Текстовый файл сохранен как новый";
                    }
                    
                    _viewModel.SetModified(false);
                    _viewModel.SetReadOnlyMode(false); // Новый файл доступен для записи
                    UpdateReadOnlyMode();
                }
                catch (Exception ex)
                {
                    _viewModel.SetFilePath(oldFilePath); // Восстанавливаем старый путь
                    MessageBox.Show($"Не удалось сохранить файл:\n{ex.Message}", "Ошибка сохранения", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Print Operations

        private void PrintDocument()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                
                if (printDialog.ShowDialog() == true && MainEditor != null)
                {
                    // Создаем документ для печати
                    var flowDocument = CloneDocument(MainEditor.Document);
                    
                    // Настраиваем параметры печати
                    flowDocument.PageHeight = printDialog.PrintableAreaHeight;
                    flowDocument.PageWidth = printDialog.PrintableAreaWidth;
                    flowDocument.PagePadding = new Thickness(50);
                    flowDocument.ColumnGap = 0;
                    flowDocument.ColumnWidth = printDialog.PrintableAreaWidth - 100;
                    
                    // Добавляем заголовок с именем файла
                    var titleParagraph = new Paragraph(new Run(Path.GetFileName(_viewModel.FilePath) ?? "Документ"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    
                    flowDocument.Blocks.InsertBefore(flowDocument.Blocks.FirstBlock, titleParagraph);
                    
                    // Добавляем дату печати
                    var dateParagraph = new Paragraph(new Run($"Напечатано: {DateTime.Now:dd.MM.yyyy HH:mm}"))
                    {
                        FontSize = 10,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    flowDocument.Blocks.InsertAfter(titleParagraph, dateParagraph);
                    
                    // Печатаем
                    IDocumentPaginatorSource idpSource = flowDocument;
                    printDialog.PrintDocument(idpSource.DocumentPaginator, $"{_application.Name} - {Path.GetFileName(_viewModel.FilePath)}");
                    
                    _viewModel.StatusText = "Документ отправлен на печать";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати:\n{ex.Message}", "Ошибка печати", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintPreview()
        {
            try
            {
                if (MainEditor == null) return;
                
                // Создаем окно предварительного просмотра
                var previewWindow = new Window
                {
                    Title = "Предварительный просмотр",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                
                var flowDocument = CloneDocument(MainEditor.Document);
                var documentViewer = new FlowDocumentScrollViewer
                {
                    Document = flowDocument,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                
                previewWindow.Content = documentViewer;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка предварительного просмотра:\n{ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PageSetup()
        {
            MessageBox.Show("Функция настройки страницы будет добавлена в следующих версиях", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private FlowDocument CloneDocument(FlowDocument original)
        {
            var range = new TextRange(original.ContentStart, original.ContentEnd);
            var clone = new FlowDocument();
            var cloneRange = new TextRange(clone.ContentStart, clone.ContentEnd);
            
            using (var stream = new MemoryStream())
            {
                range.Save(stream, DataFormats.XamlPackage);
                stream.Position = 0;
                cloneRange.Load(stream, DataFormats.XamlPackage);
            }
            
            return clone;
        }

        #endregion

        #region Format Operations

        private void ChangeTextColor()
        {
            if (MainEditor == null) return;
            
            // Простой выбор цвета (можно заменить на полноценный ColorPicker)
            var colors = new[] { Brushes.Black, Brushes.Red, Brushes.Blue, Brushes.Green, Brushes.Purple };
            var colorNames = new[] { "Черный", "Красный", "Синий", "Зеленый", "Фиолетовый" };
            
            var dialog = new Window
            {
                Title = "Выбор цвета текста",
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var name = colorNames[i];
                var button = new Button
                {
                    Content = name,
                    Background = color,
                    Foreground = color == Brushes.Black ? Brushes.White : Brushes.Black,
                    Margin = new Thickness(0, 5, 0, 0),
                    Padding = new Thickness(10, 5, 10, 5)
                };
                
                button.Click += (s, e) =>
                {
                    MainEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                    dialog.Close();
                };
                
                stackPanel.Children.Add(button);
            }
            
            dialog.Content = stackPanel;
            dialog.ShowDialog();
        }

        private void ChangeTextBackground()
        {
            if (MainEditor == null) return;
            
            var colors = new[] { Brushes.Transparent, Brushes.Yellow, Brushes.LightBlue, Brushes.LightGreen, Brushes.Pink };
            var colorNames = new[] { "Без выделения", "Желтый", "Голубой", "Зеленый", "Розовый" };
            
            var dialog = new Window
            {
                Title = "Выбор цвета выделения",
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var name = colorNames[i];
                var button = new Button
                {
                    Content = name,
                    Background = color,
                    Margin = new Thickness(0, 5, 0, 0),
                    Padding = new Thickness(10, 5, 10, 5)
                };
                
                button.Click += (s, e) =>
                {
                    MainEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, color);
                    dialog.Close();
                };
                
                stackPanel.Children.Add(button);
            }
            
            dialog.Content = stackPanel;
            dialog.ShowDialog();
        }

        private void ChooseFont()
        {
            MessageBox.Show("Диалог выбора шрифта будет добавлен в следующих версиях", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FindText()
        {
            MessageBox.Show("Функция поиска текста будет добавлена в следующих версиях", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChangeZoom()
        {
            MessageBox.Show("Функция масштабирования будет добавлена в следующих версиях", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToggleReadOnlyMode()
        {
            if (!string.IsNullOrEmpty(_originalFilePath) && File.Exists(_originalFilePath))
            {
                // Для существующих файлов проверяем права доступа
                CheckFileWriteAccess();
            }
            else
            {
                // Для новых документов можно переключать режим
                _viewModel.SetReadOnlyMode(!_viewModel.IsReadOnlyMode);
            }
            
            UpdateReadOnlyMode();
        }

        #endregion

        #region Helper Methods

        private bool CheckSaveChanges()
        {
            if (!_viewModel.IsModified) return true;
            
            var result = MessageBox.Show(
                "Документ был изменен. Сохранить изменения?",
                "Сохранение документа",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            
            switch (result)
            {
                case MessageBoxResult.Yes:
                    SaveFile();
                    return !_viewModel.IsModified; // Если сохранение не удалось, отменяем операцию
                case MessageBoxResult.No:
                    return true;
                case MessageBoxResult.Cancel:
                default:
                    return false;
            }
        }

        private void TextEditorWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_viewModel.IsModified)
            {
                e.Cancel = !CheckSaveChanges();
            }
        }

        public void ForceClose()
        {
            Closing -= TextEditorWindow_Closing; // Отключаем проверку сохранения
            Close();
        }

        public IntPtr GetWindowHandle()
        {
            return new WindowInteropHelper(this).Handle;
        }

        #endregion
    }
    
    /// <summary>
    /// Простая реализация RelayCommand для команд
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public event EventHandler? CanExecuteChanged;
        
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        
        public void Execute(object? parameter) => _execute();
        
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
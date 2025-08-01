using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowsLauncher.Core.Models;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.Components.TextEditor
{
    /// <summary>
    /// ViewModel для окна текстового редактора с поддержкой привязки данных
    /// </summary>
    public class TextEditorViewModel : INotifyPropertyChanged
    {
        private readonly TextEditorArguments _arguments;
        private bool _isModified;
        private bool _isReadOnlyMode;
        private string _statusText = "Готово";
        private string _lineColumnText = "Строка 1, Столбец 1";
        private string _windowTitle = "Текстовый редактор";

        public TextEditorViewModel(TextEditorArguments arguments, CoreApplication application)
        {
            _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Application = application ?? throw new ArgumentNullException(nameof(application));
            
            // Инициализируем свойства из аргументов
            _isReadOnlyMode = arguments.ReadOnlyMode;
            UpdateWindowTitle();
        }

        #region Properties

        public CoreApplication Application { get; }

        /// <summary>
        /// Разрешено ли открытие файлов
        /// </summary>
        public bool AllowOpenFiles => _arguments.AllowOpenFiles;

        /// <summary>
        /// Разрешено ли сохранение файлов
        /// </summary>
        public bool AllowSaveFiles => _arguments.AllowSaveFiles;

        /// <summary>
        /// Разрешена ли печать
        /// </summary>
        public bool AllowPrint => _arguments.AllowPrint;

        /// <summary>
        /// Разрешено ли форматирование
        /// </summary>
        public bool AllowFormatting => _arguments.AllowFormatting;

        /// <summary>
        /// Показывать ли панель инструментов
        /// </summary>
        public bool ShowToolbar => _arguments.ShowToolbar;

        /// <summary>
        /// Показывать ли статус бар
        /// </summary>
        public bool ShowStatusbar => _arguments.ShowStatusbar;

        /// <summary>
        /// Есть ли файловые операции (для показа разделителя)
        /// </summary>
        public bool HasFileOperations => AllowOpenFiles || AllowSaveFiles || AllowPrint;

        /// <summary>
        /// Режим только чтения
        /// </summary>
        public bool IsReadOnlyMode
        {
            get => _isReadOnlyMode;
            set
            {
                if (SetProperty(ref _isReadOnlyMode, value))
                {
                    UpdateWindowTitle();
                    OnPropertyChanged(nameof(CanEditDocument));
                }
            }
        }

        /// <summary>
        /// Документ изменен
        /// </summary>
        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (SetProperty(ref _isModified, value))
                {
                    UpdateWindowTitle();
                    StatusText = value ? "Изменено" : "Готово";
                }
            }
        }

        /// <summary>
        /// Можно ли редактировать документ
        /// </summary>
        public bool CanEditDocument => !IsReadOnlyMode;

        /// <summary>
        /// Текст статус бара
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Текст позиции курсора
        /// </summary>
        public string LineColumnText
        {
            get => _lineColumnText;
            set => SetProperty(ref _lineColumnText, value);
        }

        /// <summary>
        /// Заголовок окна
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            private set => SetProperty(ref _windowTitle, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Обновляет позицию курсора
        /// </summary>
        public void UpdateCursorPosition(int line, int column)
        {
            LineColumnText = $"Строка {line}, Столбец {column}";
        }

        /// <summary>
        /// Устанавливает путь к файлу
        /// </summary>
        public void SetFilePath(string filePath)
        {
            FilePath = filePath;
            UpdateWindowTitle();
        }

        /// <summary>
        /// Устанавливает состояние изменений
        /// </summary>
        public void SetModified(bool isModified)
        {
            IsModified = isModified;
        }

        /// <summary>
        /// Устанавливает режим только чтения
        /// </summary>
        public void SetReadOnlyMode(bool isReadOnly, string reason = "")
        {
            IsReadOnlyMode = isReadOnly;
            
            if (isReadOnly && !string.IsNullOrEmpty(reason))
            {
                StatusText = reason;
            }
        }

        #endregion

        #region Private Methods

        private string _filePath = string.Empty;
        
        public string FilePath
        {
            get => _filePath;
            private set => SetProperty(ref _filePath, value);
        }

        private void UpdateWindowTitle()
        {
            var fileName = string.IsNullOrEmpty(FilePath) 
                ? "Без имени" 
                : System.IO.Path.GetFileName(FilePath);
            
            var modifiedMark = IsModified ? "*" : "";
            var readOnlyMark = IsReadOnlyMode ? " [Только чтение]" : "";
            
            // Добавляем эмодзи иконку приложения если есть
            var appIcon = !string.IsNullOrEmpty(Application.IconText) 
                ? $"{Application.IconText} " 
                : "";
            
            WindowTitle = $"{appIcon}{fileName}{modifiedMark}{readOnlyMark} — {Application.Name}";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
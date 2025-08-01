// ===== WindowsLauncher.UI/ViewModels/ApplicationViewModel.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ =====
using System;
using System.Collections.Generic;
using System.ComponentModel;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel для приложений с корпоративным дизайном
    /// </summary>
    public class ApplicationViewModel : INotifyPropertyChanged
    {
        private readonly CoreApplication _application; // ✅ Используем CoreApplication

        public ApplicationViewModel(CoreApplication application) // ✅ Используем CoreApplication
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));

            // Подписываемся на изменение языка
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        #region Application Properties

        public int Id => _application.Id;
        public string Name => _application.Name;
        public string Description => GetLocalizedDescription();
        public string DisplayDescription => GetDisplayDescription();
        public string Category => _application.Category;
        public string ExecutablePath => _application.ExecutablePath;
        public bool IsEnabled => _application.IsEnabled;

        #endregion

        #region UI Properties

        /// <summary>
        /// Иконки в корпоративном стиле
        /// </summary>
        public string IconText
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return "📱";

                return Category?.ToLower() switch
                {
                    "system" => "⚙️",
                    "utilities" => "🔧",
                    "development" => "💻",
                    "business" => "💼",
                    "communication" => "💬",
                    "office" => "📊",
                    "web" => "🌐",
                    "tools" => "🛠️",
                    "games" => "🎮",
                    "media" => "🎵",
                    "graphics" => "🎨",
                    "security" => "🔒",
                    _ => Name[0].ToString().ToUpper()
                };
            }
        }

        /// <summary>
        /// Локализованное название категории
        /// </summary>
        public string LocalizedCategory
        {
            get
            {
                if (string.IsNullOrEmpty(Category))
                    return string.Empty;

                return GetLocalizedCategoryName(Category);
            }
        }

        /// <summary>
        /// Корпоративные цвета для категорий
        /// </summary>
        public string CategoryColor
        {
            get
            {
                return Category?.ToLower() switch
                {
                    "system" => "#C41E3A",        // Корпоративный красный
                    "utilities" => "#E8324F",     // Светло-красный
                    "development" => "#A01729",   // Темно-красный
                    "business" => "#2E4B8C",      // Корпоративный синий
                    "communication" => "#4CAF50", // Зеленый
                    "office" => "#FF9800",        // Оранжевый
                    "web" => "#00BCD4",           // Голубой
                    "tools" => "#795548",         // Коричневый
                    "games" => "#9C27B0",         // Фиолетовый
                    "media" => "#673AB7",         // Глубокий фиолетовый
                    "graphics" => "#FF5722",      // Глубокий оранжевый
                    "security" => "#F44336",      // Красный
                    _ => "#666666"                // Серый по умолчанию
                };
            }
        }

        /// <summary>
        /// Иконка категории
        /// </summary>
        public string CategoryIcon
        {
            get
            {
                return Category?.ToLower() switch
                {
                    "system" => "⚙️",
                    "utilities" => "🔧",
                    "development" => "💻",
                    "business" => "💼",
                    "communication" => "💬",
                    "office" => "📊",
                    "web" => "🌐",
                    "tools" => "🛠️",
                    "games" => "🎮",
                    "media" => "🎵",
                    "graphics" => "🎨",
                    "security" => "🔒",
                    _ => "📱"
                };
            }
        }

        /// <summary>
        /// Показывать ли приложение (фильтрация)
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// Адаптивная высота плитки (рассчитывается динамически)
        /// </summary>
        private double _adaptiveHeight = 160; // Дефолтная высота
        public double AdaptiveHeight
        {
            get => _adaptiveHeight;
            set
            {
                if (Math.Abs(_adaptiveHeight - value) > 0.1) // Избегаем лишних уведомлений
                {
                    _adaptiveHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Подсказка для кнопки запуска
        /// </summary>
        public string LaunchTooltip => $"Запустить {Name}";

        /// <summary>
        /// Статус приложения для отображения
        /// </summary>
        public string StatusText
        {
            get
            {
                return _application.Type switch
                {
                    Core.Enums.ApplicationType.Web => "Веб-приложение",
                    Core.Enums.ApplicationType.Folder => "Папка",
                    Core.Enums.ApplicationType.Desktop => "Приложение",
                    _ => "Приложение"
                };
            }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Получить исходную модель Application
        /// </summary>
        public CoreApplication GetApplication() => _application; // ✅ Используем CoreApplication

        /// <summary>
        /// Получить локализованное описание приложения
        /// </summary>
        private string GetLocalizedDescription()
        {
            if (string.IsNullOrEmpty(_application.Description))
                return "";

            // Пытаемся найти локализованное описание для системных приложений
            var descriptionKey = GetDescriptionKey(_application.Name);
            if (!string.IsNullOrEmpty(descriptionKey))
            {
                try
                {
                    var localized = LocalizationManager.GetString(descriptionKey);
                    if (!string.IsNullOrEmpty(localized) && localized != descriptionKey)
                    {
                        return localized;
                    }
                }
                catch
                {
                    // Если ошибка локализации, возвращаем оригинал
                }
            }

            return _application.Description;
        }

        /// <summary>
        /// Получить описание без технического префикса [CACHED_TITLE] для отображения пользователю
        /// </summary>
        private string GetDisplayDescription()
        {
            // Сначала получаем обычное описание (с локализацией)
            var description = GetLocalizedDescription();
            
            if (string.IsNullOrEmpty(description))
                return "";
            
            // Убираем технический префикс если он есть
            if (description.StartsWith("[CACHED_TITLE]"))
                return description.Substring("[CACHED_TITLE]".Length);
            
            return description;
        }

        /// <summary>
        /// Получить ключ локализации для описания приложения
        /// </summary>
        private string GetDescriptionKey(string appName)
        {
            return appName?.ToLower() switch
            {
                "calculator" => "CalculatorDescription",
                "notepad" => "NotepadDescription",
                "google" => "GoogleDescription",
                "control panel" => "ControlPanelDescription",
                "command prompt" => "CommandPromptDescription",
                "registry editor" => "RegistryEditorDescription",
                "task manager" => "TaskManagerDescription",
                "file explorer" => "FileExplorerDescription",
                "paint" => "PaintDescription",
                "wordpad" => "WordpadDescription",
                _ => ""
            };
        }

        /// <summary>
        /// Получить локализованное название категории
        /// </summary>
        private string GetLocalizedCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category))
                return category;

            // Словарь для перевода категорий
            var categoryTranslations = new Dictionary<string, string>
            {
                { "System", "Система" },
                { "Utilities", "Утилиты" },
                { "Development", "Разработка" },
                { "Business", "Бизнес" },
                { "Communication", "Коммуникации" },
                { "Office", "Офис" },
                { "Web", "Веб" },
                { "Tools", "Инструменты" },
                { "Games", "Игры" },
                { "Media", "Медиа" },
                { "Graphics", "Графика" },
                { "Security", "Безопасность" }
            };

            return categoryTranslations.TryGetValue(category, out var translation)
                ? translation
                : category;
        }

        /// <summary>
        /// Обработчик изменения языка
        /// </summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Обновляем локализованные свойства
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(DisplayDescription));
            OnPropertyChanged(nameof(LocalizedCategory));
            OnPropertyChanged(nameof(LaunchTooltip));
            OnPropertyChanged(nameof(StatusText));
        }

        public override string ToString()
        {
            return $"{Name} ({LocalizedCategory})";
        }

        public override bool Equals(object? obj)
        {
            return obj is ApplicationViewModel other && other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region IDisposable Pattern

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    LocalizationManager.LanguageChanged -= OnLanguageChanged;
                }
                _disposed = true;
            }
        }

        ~ApplicationViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}
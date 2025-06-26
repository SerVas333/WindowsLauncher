using System;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.UI.ViewModels
{
    /// <summary>
    /// ViewModel обертка для Application модели с дополнительными UI свойствами
    /// </summary>
    public class ApplicationViewModel
    {
        private readonly Application _application;

        public ApplicationViewModel(Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        #region Application Properties

        public int Id => _application.Id;
        public string Name => _application.Name;
        public string Description => _application.Description;
        public string Category => _application.Category;
        public string ExecutablePath => _application.ExecutablePath;
        public bool IsEnabled => _application.IsEnabled;

        #endregion

        #region UI Properties

        /// <summary>
        /// Текст иконки для отображения (первая буква названия или эмодзи)
        /// </summary>
        public string IconText
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return "📱";

                // Возвращаем эмодзи в зависимости от категории
                return Category?.ToLower() switch
                {
                    "system" => "⚙️",
                    "utilities" => "🔧",
                    "development" => "💻",
                    "business" => "💼",
                    "communication" => "💬",
                    "office" => "📊",
                    _ => Name[0].ToString().ToUpper() // Первая буква названия
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

                // Пытаемся получить локализованную строку
                var key = $"Category_{Category}";
                try
                {
                    var localized = LocalizationManager.GetString(key);

                    // Если локализация не найдена, возвращаем оригинальное название
                    return !string.IsNullOrEmpty(localized) && localized != key ? localized : Category;
                }
                catch
                {
                    // Если ошибка локализации, возвращаем оригинал
                    return Category;
                }
            }
        }

        /// <summary>
        /// Цвет категории для UI
        /// </summary>
        public string CategoryColor
        {
            get
            {
                return Category?.ToLower() switch
                {
                    "system" => "#FF5722",      // Deep Orange
                    "utilities" => "#2196F3",   // Blue  
                    "development" => "#4CAF50", // Green
                    "business" => "#9C27B0",    // Purple
                    "communication" => "#FF9800", // Orange
                    "office" => "#607D8B",      // Blue Grey
                    _ => "#757575"              // Grey
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
                    _ => "📱"
                };
            }
        }

        /// <summary>
        /// Показывать ли приложение (фильтрация)
        /// </summary>
        public bool IsVisible { get; set; } = true;

        #endregion

        #region Methods

        /// <summary>
        /// Получить исходную модель Application
        /// </summary>
        public Application GetApplication() => _application;

        public override string ToString()
        {
            return $"{Name} ({Category})";
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
    }
}
using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models.Configuration
{
    /// <summary>
    /// Настройки системы категорий
    /// </summary>
    public class CategorySettings
    {
        /// <summary>
        /// Разрешить создание динамических категорий
        /// </summary>
        public bool AllowDynamicCategories { get; set; } = true;

        /// <summary>
        /// Автоматически создавать категории из приложений
        /// </summary>
        public bool AutoCreateFromApps { get; set; } = true;

        /// <summary>
        /// Иконка по умолчанию для новых категорий
        /// </summary>
        public string DefaultIcon { get; set; } = "FolderOpen";

        /// <summary>
        /// Цвет по умолчанию для новых категорий
        /// </summary>
        public string DefaultColor { get; set; } = "#666666";

        /// <summary>
        /// Максимальное количество категорий
        /// </summary>
        public int MaxCategories { get; set; } = 50;

        /// <summary>
        /// Скрывать пустые категории
        /// </summary>
        public bool HideEmptyCategories { get; set; } = false;

        /// <summary>
        /// Показывать количество приложений в категории
        /// </summary>
        public bool ShowAppCount { get; set; } = true;
    }

    /// <summary>
    /// Конфигурация категорий из appsettings.json
    /// </summary>
    public class CategoryConfiguration
    {
        /// <summary>
        /// Предустановленные категории
        /// </summary>
        public List<CategoryDefinition> PredefinedCategories { get; set; } = new();

        /// <summary>
        /// Настройки системы категорий
        /// </summary>
        public CategorySettings Settings { get; set; } = new();
    }
}
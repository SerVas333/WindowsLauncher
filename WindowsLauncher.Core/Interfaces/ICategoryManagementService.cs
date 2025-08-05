using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Configuration;

namespace WindowsLauncher.Core.Interfaces
{
    /// <summary>
    /// Сервис управления категориями приложений
    /// </summary>
    public interface ICategoryManagementService
    {
        /// <summary>
        /// Получить все доступные категории (предустановленные + динамические)
        /// </summary>
        Task<List<CategoryDefinition>> GetAllCategoriesAsync();

        /// <summary>
        /// Получить видимые категории с учетом пользовательских настроек
        /// </summary>
        Task<List<CategoryDefinition>> GetVisibleCategoriesAsync(User user);

        /// <summary>
        /// Получить категорию по ключу
        /// </summary>
        Task<CategoryDefinition?> GetCategoryByKeyAsync(string key);

        /// <summary>
        /// Создать или обновить категорию
        /// </summary>
        Task<bool> SaveCategoryAsync(CategoryDefinition category, User user);

        /// <summary>
        /// Удалить категорию (только не системные)
        /// </summary>
        Task<bool> DeleteCategoryAsync(string key, User user);

        /// <summary>
        /// Скрыть/показать категорию для пользователя
        /// </summary>
        Task<bool> SetCategoryVisibilityAsync(string key, bool isVisible, User user);

        /// <summary>
        /// Получить локализованное имя категории
        /// </summary>
        string GetLocalizedCategoryName(CategoryDefinition category, string? languageCode = null);

        /// <summary>
        /// Автоматически создать категории из существующих приложений
        /// </summary>
        Task<List<CategoryDefinition>> AutoGenerateCategoriesAsync();

        /// <summary>
        /// Синхронизировать категории с базой приложений
        /// </summary>
        Task SynchronizeCategoriesAsync();

        /// <summary>
        /// Получить статистику использования категорий
        /// </summary>
        Task<Dictionary<string, int>> GetCategoryStatisticsAsync();

        /// <summary>
        /// Сбросить категории к предустановленным
        /// </summary>
        Task<bool> ResetToDefaultCategoriesAsync(User user);

        /// <summary>
        /// Экспортировать конфигурацию категорий
        /// </summary>
        Task<CategoryConfiguration> ExportCategoriesAsync();

        /// <summary>
        /// Импортировать конфигурацию категорий
        /// </summary>
        Task<bool> ImportCategoriesAsync(CategoryConfiguration configuration, User user);
    }
}
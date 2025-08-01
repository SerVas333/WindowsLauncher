// WindowsLauncher.Core/Interfaces/IApplicationService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IApplicationService
    {
        /// <summary>
        /// Запустить приложение для пользователя
        /// </summary>
        Task<LaunchResult> LaunchApplicationAsync(Application application, User user);

        /// <summary>
        /// Получить все приложения
        /// </summary>
        Task<List<Application>> GetAllApplicationsAsync();

        /// <summary>
        /// Получить приложения по категории
        /// </summary>
        Task<List<Application>> GetApplicationsByCategoryAsync(string category);

        /// <summary>
        /// Найти приложения по поисковому запросу
        /// </summary>
        Task<List<Application>> SearchApplicationsAsync(string searchTerm);

        /// <summary>
        /// Получить все категории приложений
        /// </summary>
        Task<List<string>> GetCategoriesAsync();

        /// <summary>
        /// Добавить новое приложение (только для администраторов)
        /// </summary>
        Task<bool> AddApplicationAsync(Application application, User user);

        /// <summary>
        /// Обновить приложение (только для администраторов)
        /// </summary>
        Task<bool> UpdateApplicationAsync(Application application, User user);

        /// <summary>
        /// Удалить приложение (только для администраторов)
        /// </summary>
        Task<bool> DeleteApplicationAsync(int applicationId, User user);

        /// <summary>
        /// Получить запущенные процессы приложений
        /// </summary>
        Task<List<int>> GetRunningProcessesAsync();

        /// <summary>
        /// Событие запуска приложения
        /// </summary>
        event EventHandler<Application>? ApplicationLaunched;
    }
}
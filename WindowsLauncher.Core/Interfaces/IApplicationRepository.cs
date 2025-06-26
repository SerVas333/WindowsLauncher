// WindowsLauncher.Core/Interfaces/IApplicationRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IApplicationRepository : IRepository<Application>
    {
        /// <summary>
        /// Получить приложения по категории
        /// </summary>
        Task<List<Application>> GetByCategoryAsync(string category);

        /// <summary>
        /// Поиск приложений по имени или описанию
        /// </summary>
        Task<List<Application>> SearchAsync(string searchTerm);

        /// <summary>
        /// Получить только активные приложения
        /// </summary>
        Task<List<Application>> GetActiveApplicationsAsync();

        /// <summary>
        /// Получить все категории
        /// </summary>
        Task<List<string>> GetCategoriesAsync();
    }
}


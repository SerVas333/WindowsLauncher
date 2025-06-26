// WindowsLauncher.Data/Repositories/ApplicationRepository.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Repositories
{
    public class ApplicationRepository : BaseRepository<Application>, IApplicationRepository
    {
        public ApplicationRepository(LauncherDbContext context) : base(context)
        {
        }

        public async Task<List<Application>> GetByCategoryAsync(string category)
        {
            return await _dbSet
                .Where(a => a.Category == category && a.IsEnabled)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<List<Application>> SearchAsync(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            return await _dbSet
                .Where(a => a.IsEnabled &&
                           (a.Name.ToLower().Contains(searchTerm) ||
                            a.Description.ToLower().Contains(searchTerm)))
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<List<Application>> GetActiveApplicationsAsync()
        {
            return await _dbSet
                .Where(a => a.IsEnabled)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _dbSet
                .Where(a => a.IsEnabled)
                .Select(a => a.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}

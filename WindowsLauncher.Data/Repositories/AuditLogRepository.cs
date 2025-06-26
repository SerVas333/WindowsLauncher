// WindowsLauncher.Data/Repositories/AuditLogRepository.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Repositories
{
    public class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(LauncherDbContext context) : base(context)
        {
        }

        public async Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate)
        {
            return await _dbSet
                .Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByUsernameAsync(string username, int limit = 100)
        {
            return await _dbSet
                .Where(l => l.Username == username)
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByActionAsync(string action, int limit = 100)
        {
            return await _dbSet
                .Where(l => l.Action == action)
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task DeleteOldLogsAsync(DateTime beforeDate)
        {
            var oldLogs = await _dbSet
                .Where(l => l.Timestamp < beforeDate)
                .ToListAsync();

            _dbSet.RemoveRange(oldLogs);
            await SaveChangesAsync();
        }

        public async Task<Dictionary<string, int>> GetApplicationUsageStatsAsync(DateTime fromDate, DateTime toDate)
        {
            return await _dbSet
                .Where(l => l.Action == "LaunchApp" &&
                           l.Timestamp >= fromDate &&
                           l.Timestamp <= toDate &&
                           l.Success)
                .GroupBy(l => l.ApplicationName)
                .Select(g => new { AppName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AppName, x => x.Count);
        }
    }
}

// WindowsLauncher.Data/Repositories/AuditLogRepository.cs - ВЕРСИЯ С IDbContextFactory ДЛЯ SINGLETON
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbContextFactory<LauncherDbContext> _contextFactory;

        public AuditLogRepository(IDbContextFactory<LauncherDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs
                .Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByUsernameAsync(string username, int limit = 100)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs
                .Where(l => l.Username == username)
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByActionAsync(string action, int limit = 100)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs
                .Where(l => l.Action == action)
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> DeleteOldLogsAsync(DateTime beforeDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var oldLogs = await context.AuditLogs
                .Where(l => l.Timestamp < beforeDate)
                .ToListAsync();

            var deletedCount = oldLogs.Count;
            context.AuditLogs.RemoveRange(oldLogs);
            await context.SaveChangesAsync();

            return deletedCount;
        }

        public async Task<Dictionary<string, int>> GetApplicationUsageStatsAsync(DateTime fromDate, DateTime toDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs
                .Where(l => l.Action == "LaunchApp" &&
                           l.Timestamp >= fromDate &&
                           l.Timestamp <= toDate &&
                           l.Success)
                .GroupBy(l => l.ApplicationName)
                .Select(g => new { AppName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AppName, x => x.Count);
        }

        // Добавляем стандартные методы из BaseRepository
        public async Task<AuditLog?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.FindAsync(id);
        }

        public async Task<List<AuditLog>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.ToListAsync();
        }

        public async Task<AuditLog> AddAsync(AuditLog auditLog)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.AuditLogs.Add(auditLog);
            await context.SaveChangesAsync();
            return auditLog;
        }

        public async Task<AuditLog> UpdateAsync(AuditLog auditLog)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.AuditLogs.Update(auditLog);
            await context.SaveChangesAsync();
            return auditLog;
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var auditLog = await context.AuditLogs.FindAsync(id);
            if (auditLog != null)
            {
                context.AuditLogs.Remove(auditLog);
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.AnyAsync(a => a.Id == id);
        }

        // Добавляем недостающие методы из IRepository<AuditLog>
        public async Task<List<AuditLog>> FindAsync(Expression<Func<AuditLog, bool>> predicate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.Where(predicate).ToListAsync();
        }

        public async Task<AuditLog?> FirstOrDefaultAsync(Expression<Func<AuditLog, bool>> predicate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.FirstOrDefaultAsync(predicate);
        }

        public async Task<int> CountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.AuditLogs.CountAsync();
        }

        public async Task SaveChangesAsync()
        {
            // В паттерне DbContextFactory каждая операция уже сохраняет изменения в своем контексте
            // Этот метод остается пустым для совместимости с интерфейсом
            await Task.CompletedTask;
        }
    }
}
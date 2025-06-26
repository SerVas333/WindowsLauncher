// WindowsLauncher.Core/Interfaces/IAuditLogRepository.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IAuditLogRepository : IRepository<AuditLog>
    {
        /// <summary>
        /// Получить логи за период
        /// </summary>
        Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Получить логи пользователя
        /// </summary>
        Task<List<AuditLog>> GetLogsByUsernameAsync(string username, int limit = 100);

        /// <summary>
        /// Получить логи по действию
        /// </summary>
        Task<List<AuditLog>> GetLogsByActionAsync(string action, int limit = 100);

        /// <summary>
        /// Удалить старые логи
        /// </summary>
        Task DeleteOldLogsAsync(DateTime beforeDate);

        /// <summary>
        /// Получить статистику по приложениям
        /// </summary>
        Task<Dictionary<string, int>> GetApplicationUsageStatsAsync(DateTime fromDate, DateTime toDate);
    }
}
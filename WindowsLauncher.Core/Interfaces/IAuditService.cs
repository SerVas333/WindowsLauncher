// WindowsLauncher.Core/Interfaces/IAuditService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Core.Interfaces
{
    public interface IAuditService
    {
        /// <summary>
        /// Записать событие аудита
        /// </summary>
        Task LogEventAsync(string username, string action, string details, bool success = true, string errorMessage = "");

        /// <summary>
        /// Записать событие запуска приложения
        /// </summary>
        Task LogApplicationLaunchAsync(int applicationId, string applicationName, string username, bool success, string errorMessage = "");

        /// <summary>
        /// Записать событие входа в систему
        /// </summary>
        Task LogLoginAsync(string username, bool success, string errorMessage = "");

        /// <summary>
        /// Записать событие выхода из системы
        /// </summary>
        Task LogLogoutAsync(string username);

        /// <summary>
        /// Записать событие отказа в доступе
        /// </summary>
        Task LogAccessDeniedAsync(string username, string resource, string reason);

        /// <summary>
        /// Получить логи аудита за период
        /// </summary>
        Task<List<AuditLog>> GetAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username = null);

        /// <summary>
        /// Получить статистику использования приложений
        /// </summary>
        Task<Dictionary<string, int>> GetApplicationUsageStatsAsync(DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Очистить старые логи (старше указанного количества дней)
        /// </summary>
        Task CleanupOldLogsAsync(int daysToKeep = 90);
    }
}


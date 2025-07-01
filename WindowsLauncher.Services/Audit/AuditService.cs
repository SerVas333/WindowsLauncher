// WindowsLauncher.Services/Audit/AuditService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.Audit
{
    /// <summary>
    /// Сервис аудита для логирования действий пользователей
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            IAuditLogRepository auditLogRepository,
            ILogger<AuditService> logger)
        {
            _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Логирование произвольного события
        /// </summary>
        public async Task LogEventAsync(string username, string action, string details, bool success = true, string errorMessage = "")
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Username = username,
                    Action = action,
                    Details = details,
                    Success = success,
                    ErrorMessage = errorMessage,
                    Timestamp = DateTime.UtcNow,
                    ComputerName = Environment.MachineName,
                    IPAddress = GetLocalIPAddress()
                };

                await _auditLogRepository.AddAsync(auditLog);
                await _auditLogRepository.SaveChangesAsync();

                _logger.LogDebug("Audit event logged: {Action} by {Username} - {Success}",
                    action, username, success ? "Success" : "Failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit event {Action} for user {Username}", action, username);
            }
        }

        /// <summary>
        /// Логирование запуска приложения
        /// </summary>
        public async Task LogApplicationLaunchAsync(int applicationId, string applicationName, string username, bool success, string errorMessage = "")
        {
            await LogEventAsync(username, "LaunchApplication",
                $"Application: {applicationName} (ID: {applicationId})", success, errorMessage);
        }

        /// <summary>
        /// Логирование входа в систему
        /// </summary>
        public async Task LogLoginAsync(string username, bool success, string errorMessage = "")
        {
            await LogEventAsync(username, "Login",
                $"User login attempt from {Environment.MachineName}", success, errorMessage);
        }

        /// <summary>
        /// Логирование выхода из системы
        /// </summary>
        public async Task LogLogoutAsync(string username)
        {
            await LogEventAsync(username, "Logout",
                $"User logout from {Environment.MachineName}", true);
        }

        /// <summary>
        /// Логирование отказа в доступе
        /// </summary>
        public async Task LogAccessDeniedAsync(string username, string resource, string reason)
        {
            await LogEventAsync(username, "AccessDenied",
                $"Access denied to {resource}. Reason: {reason}", false, reason);
        }

        /// <summary>
        /// Логирование произвольного AuditLog объекта
        /// </summary>
        public async Task LogAsync(AuditLog auditLog)
        {
            try
            {
                if (auditLog == null)
                    throw new ArgumentNullException(nameof(auditLog));

                // Дополняем недостающие поля
                if (auditLog.Timestamp == default)
                    auditLog.Timestamp = DateTime.UtcNow;

                if (string.IsNullOrEmpty(auditLog.ComputerName))
                    auditLog.ComputerName = Environment.MachineName;

                if (string.IsNullOrEmpty(auditLog.IPAddress))
                    auditLog.IPAddress = GetLocalIPAddress();

                await _auditLogRepository.AddAsync(auditLog);
                await _auditLogRepository.SaveChangesAsync();

                _logger.LogDebug("Audit log entry saved: {Action} by user {UserId}",
                    auditLog.Action, auditLog.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for user {UserId}", auditLog?.UserId);
            }
        }

        /// <summary>
        /// Получение логов аудита за период
        /// </summary>
        public async Task<List<AuditLog>> GetAuditLogsAsync(DateTime fromDate, DateTime toDate, string username = null)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return await _auditLogRepository.GetLogsByDateRangeAsync(fromDate, toDate);
                }
                else
                {
                    var userLogs = await _auditLogRepository.GetLogsByUsernameAsync(username, 1000);
                    return userLogs.Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for period {FromDate} - {ToDate}", fromDate, toDate);
                return new List<AuditLog>();
            }
        }

        /// <summary>
        /// Получение статистики использования приложений
        /// </summary>
        public async Task<Dictionary<string, int>> GetApplicationUsageStatsAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                return await _auditLogRepository.GetApplicationUsageStatsAsync(fromDate, toDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting application usage stats for period {FromDate} - {ToDate}", fromDate, toDate);
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Очистка старых логов
        /// </summary>
        public async Task CleanupOldLogsAsync(int daysToKeep = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var deletedCount = await _auditLogRepository.DeleteOldLogsAsync(cutoffDate);

                _logger.LogInformation("Cleaned up {DeletedCount} audit logs older than {CutoffDate}",
                    deletedCount, cutoffDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
            }
        }

        /// <summary>
        /// Получение локального IP адреса
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting local IP address");
                return "Unknown";
            }
        }
    }
}
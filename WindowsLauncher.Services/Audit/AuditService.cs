// WindowsLauncher.Services/Audit/AuditService.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.Audit
{
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            IAuditLogRepository auditLogRepository,
            ILogger<AuditService> logger)
        {
            _auditLogRepository = auditLogRepository;
            _logger = logger;
        }

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
                    Timestamp = DateTime.Now,
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

        public async Task LogApplicationLaunchAsync(int applicationId, string applicationName, string username, bool success, string errorMessage = "")
        {
            await LogEventAsync(username, "LaunchApp",
                $"Application: {applicationName} (ID: {applicationId})", success, errorMessage);
        }

        public async Task LogLoginAsync(string username, bool success, string errorMessage = "")
        {
            await LogEventAsync(username, "Login",
                $"User login attempt from {Environment.MachineName}", success, errorMessage);
        }

        public async Task LogLogoutAsync(string username)
        {
            await LogEventAsync(username, "Logout",
                $"User logout from {Environment.MachineName}");
        }

        public async Task LogAccessDeniedAsync(string username, string resource, string reason)
        {
            await LogEventAsync(username, "AccessDenied",
                $"Access denied to {resource}. Reason: {reason}", false, reason);
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username = null)
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

        public async Task CleanupOldLogsAsync(int daysToKeep = 90)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                await _auditLogRepository.DeleteOldLogsAsync(cutoffDate);

                _logger.LogInformation("Cleaned up audit logs older than {CutoffDate}", cutoffDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
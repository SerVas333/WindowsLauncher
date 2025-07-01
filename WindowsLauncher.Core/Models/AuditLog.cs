// WindowsLauncher.Core/Models/AuditLog.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using System;

namespace WindowsLauncher.Core.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public Guid UserId { get; set; } = Guid.Empty;
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Login, Logout, LaunchApp, AccessDenied
        public string ApplicationName { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;

        // Системная информация
        public string ComputerName { get; set; } = Environment.MachineName;
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;

        // Свойство для совместимости с существующим кодом
        public string IpAddress
        {
            get => IPAddress;
            set => IPAddress = value;
        }

        // Методы для создания различных типов логов
        public static AuditLog CreateLoginLog(string username, bool success, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = "Login",
                Details = $"User login attempt from {Environment.MachineName}",
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        public static AuditLog CreateLogoutLog(string username)
        {
            return new AuditLog
            {
                Username = username,
                Action = "Logout",
                Details = $"User logout from {Environment.MachineName}",
                Success = true,
                Timestamp = DateTime.UtcNow
            };
        }

        public static AuditLog CreateApplicationLaunchLog(string username, string applicationName, bool success, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = "LaunchApplication",
                ApplicationName = applicationName,
                Details = $"Application launch: {applicationName}",
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        public static AuditLog CreateAccessDeniedLog(string username, string resource, string reason)
        {
            return new AuditLog
            {
                Username = username,
                Action = "AccessDenied",
                Details = $"Access denied to {resource}",
                Success = false,
                ErrorMessage = reason,
                Timestamp = DateTime.UtcNow
            };
        }

        public static AuditLog CreateSystemEventLog(string username, string action, string details, bool success = true, string? errorMessage = null)
        {
            return new AuditLog
            {
                Username = username,
                Action = action,
                Details = details,
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
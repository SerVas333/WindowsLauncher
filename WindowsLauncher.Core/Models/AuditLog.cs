// WindowsLauncher.Core/Models/AuditLog.cs
using System;
using WindowsLauncher.Core.Enums;

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
        public string IpAddress { get; set; } = string.Empty; // Альтернативное имя для совместимости
        public string UserAgent { get; set; } = string.Empty;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Login, Logout, LaunchApp, AccessDenied
        public string ApplicationName { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;

        // Системная информация
        public string ComputerName { get; set; } = Environment.MachineName;
        public string IPAddress { get; set; } = string.Empty;
    }
}
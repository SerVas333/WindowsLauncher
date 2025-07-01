// WindowsLauncher.Core/Models/User.cs
using System;
using System.Collections.Generic;
using System.Linq;
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    public class User
    {
        public int Id { get; set; }
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public UserRole Role { get; set; } = UserRole.Standard;

        // Даты и времена
        public DateTime LastLogin { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? CreatedAt { get; set; }

        // Статус и активность
        public bool IsActive { get; set; } = true;

        // Поля для локальных учетных записей (сервисные администраторы)
        public bool IsServiceAccount { get; set; } = false;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;

        // Блокировка и безопасность
        public int FailedLoginAttempts { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
        public DateTime? LockoutEnd { get; set; }

        // Методы для проверки принадлежности к группе
        public bool IsInGroup(string groupName)
        {
            return Groups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        // Методы для проверки минимальной роли
        public bool HasMinimumRole(UserRole minRole)
        {
            return Role >= minRole;
        }
    }
}
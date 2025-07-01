// WindowsLauncher.Core/Models/User.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
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

        // Объединенные поля дат - используем единые названия
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
        public DateTime? LastActivityAt { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

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

        // Свойства для совместимости (маппинг к основным полям)
        public DateTime? LastLoginAt
        {
            get => LastLogin == default ? null : LastLogin;
            set => LastLogin = value ?? DateTime.UtcNow;
        }

        public DateTime? CreatedAt
        {
            get => CreatedDate == default ? null : CreatedDate;
            set => CreatedDate = value ?? DateTime.UtcNow;
        }

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

        // Метод для обновления времени последнего входа
        public void UpdateLastLogin()
        {
            LastLogin = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
        }

        // Метод для обновления активности
        public void UpdateActivity()
        {
            LastActivityAt = DateTime.UtcNow;
        }

        // Проверка активности сессии
        public bool IsSessionActive(TimeSpan sessionTimeout)
        {
            if (!IsActive || IsLocked)
                return false;

            if (!LastActivityAt.HasValue)
                return false;

            return DateTime.UtcNow - LastActivityAt.Value < sessionTimeout;
        }

        // Метод для сброса блокировки
        public void ResetLockout()
        {
            IsLocked = false;
            FailedLoginAttempts = 0;
            LockoutEnd = null;
        }

        // Метод для записи неудачной попытки входа
        public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
        {
            FailedLoginAttempts++;

            if (FailedLoginAttempts >= maxAttempts)
            {
                IsLocked = true;
                LockoutEnd = DateTime.UtcNow.Add(lockoutDuration);
            }
        }

        // Проверка, можно ли разблокировать пользователя
        public bool CanUnlock()
        {
            return IsLocked &&
                   LockoutEnd.HasValue &&
                   DateTime.UtcNow >= LockoutEnd.Value;
        }
    }
}
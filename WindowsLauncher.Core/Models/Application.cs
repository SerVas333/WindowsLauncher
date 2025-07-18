﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// WindowsLauncher.Core/Models/Application.cs
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    public class Application
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public ApplicationType Type { get; set; } = ApplicationType.Desktop;

        // Права доступа
        public List<string> RequiredGroups { get; set; } = new();
        public UserRole MinimumRole { get; set; } = UserRole.Standard;

        // Настройки отображения
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        // Метаданные
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = string.Empty;

        // Метод для проверки доступа пользователя
        public bool CanUserAccess(User user)
        {
            if (!IsEnabled) return false;
            if (!user.HasMinimumRole(MinimumRole)) return false;

            // Если группы не указаны - доступно всем
            if (!RequiredGroups.Any()) return true;

            // Проверяем пересечение групп
            return RequiredGroups.Any(reqGroup => user.IsInGroup(reqGroup));
        }
    }
}
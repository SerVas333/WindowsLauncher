using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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
        
        /// <summary>
        /// Возвращает Description без технического префикса [CACHED_TITLE] для отображения пользователю
        /// </summary>
        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrEmpty(Description))
                    return string.Empty;
                
                if (Description.StartsWith("[CACHED_TITLE]"))
                    return Description.Substring("[CACHED_TITLE]".Length);
                
                return Description;
            }
        }
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string IconText { get; set; } = "📱"; // Emoji иконка для приложения
        public string Category { get; set; } = "General";
        public ApplicationType Type { get; set; } = ApplicationType.Desktop;

        // APK метаданные (только для Android приложений)
        public string? ApkPackageName { get; set; }
        public int? ApkVersionCode { get; set; }
        public string? ApkVersionName { get; set; }
        public int? ApkMinSdk { get; set; }
        public int? ApkTargetSdk { get; set; }
        public string? ApkFilePath { get; set; }
        public string? ApkFileHash { get; set; }
        public string? ApkInstallStatus { get; set; } = "NotInstalled";

        // Права доступа
        public List<string> RequiredGroups { get; set; } = new();
        public UserRole MinimumRole { get; set; } = UserRole.Standard;

        // Настройки отображения
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        
        /// <summary>
        /// Указывает, активно ли приложение в данный момент (runtime состояние)
        /// Не хранится в базе данных - используется только для логики тестов и лаунчеров
        /// </summary>
        [NotMapped]
        public bool IsActive { get; set; } = true;

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
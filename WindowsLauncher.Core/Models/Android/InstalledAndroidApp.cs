namespace WindowsLauncher.Core.Models.Android
{
    /// <summary>
    /// Информация об установленном Android приложении в WSA
    /// </summary>
    public class InstalledAndroidApp
    {
        /// <summary>
        /// Package name приложения
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя приложения
        /// </summary>
        public string? AppName { get; set; }

        /// <summary>
        /// Версия приложения (строковая)
        /// </summary>
        public string? VersionName { get; set; }

        /// <summary>
        /// Код версии приложения
        /// </summary>
        public int? VersionCode { get; set; }

        /// <summary>
        /// Включено ли приложение (не отключено пользователем)
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Является ли приложение системным
        /// </summary>
        public bool IsSystemApp { get; set; }

        /// <summary>
        /// Размер установленного приложения в байтах
        /// </summary>
        public long? InstalledSizeBytes { get; set; }

        /// <summary>
        /// Время последнего использования приложения
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Время установки приложения
        /// </summary>
        public DateTime? InstalledAt { get; set; }

        /// <summary>
        /// Время установки приложения (alias для совместимости)
        /// </summary>
        public DateTime? InstallDate
        {
            get => InstalledAt;
            set => InstalledAt = value;
        }

        /// <summary>
        /// Время последнего обновления приложения
        /// </summary>
        public DateTime? LastUpdateDate { get; set; }

        /// <summary>
        /// Время последнего запуска приложения (alias для LastUsedAt)
        /// </summary>
        public DateTime? LastLaunchedAt
        {
            get => LastUsedAt;
            set => LastUsedAt = value;
        }

        /// <summary>
        /// Запущено ли приложение в данный момент
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Путь к APK файлу (если известен)
        /// </summary>
        public string? ApkPath { get; set; }

        /// <summary>
        /// Основная Activity для запуска приложения
        /// </summary>
        public string? LauncherActivity { get; set; }

        /// <summary>
        /// Получить отображаемое название приложения
        /// </summary>
        public string GetDisplayName()
        {
            return !string.IsNullOrWhiteSpace(AppName) ? AppName : PackageName;
        }

        /// <summary>
        /// Получить строку версии
        /// </summary>
        public string GetVersionString()
        {
            if (!string.IsNullOrWhiteSpace(VersionName) && VersionCode.HasValue)
            {
                return $"{VersionName} ({VersionCode})";
            }
            else if (!string.IsNullOrWhiteSpace(VersionName))
            {
                return VersionName;
            }
            else if (VersionCode.HasValue)
            {
                return VersionCode.ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// Получить размер приложения в человекочитаемом формате
        /// </summary>
        public string GetFormattedSize()
        {
            if (!InstalledSizeBytes.HasValue)
                return "Unknown";

            var bytes = InstalledSizeBytes.Value;
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024:F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MB";
            else
                return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        /// <summary>
        /// Проверить, является ли приложение пользовательским (не системным)
        /// </summary>
        public bool IsUserApp => !IsSystemApp;

        /// <summary>
        /// Проверить, можно ли запустить приложение
        /// </summary>
        public bool CanLaunch => IsEnabled && !string.IsNullOrEmpty(LauncherActivity);
    }
}
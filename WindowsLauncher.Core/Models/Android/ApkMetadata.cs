using System.ComponentModel.DataAnnotations;

namespace WindowsLauncher.Core.Models.Android
{
    /// <summary>
    /// Метаданные Android APK файла, извлеченные с помощью AAPT
    /// </summary>
    public class ApkMetadata
    {
        /// <summary>
        /// Имя пакета Android приложения (например: com.example.app)
        /// </summary>
        [Required]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Числовой код версии приложения (например: 42)
        /// </summary>
        public int VersionCode { get; set; }

        /// <summary>
        /// Строковое название версии (например: "1.2.3")
        /// </summary>
        public string? VersionName { get; set; }

        /// <summary>
        /// Отображаемое имя приложения
        /// </summary>
        public string? AppName { get; set; }

        /// <summary>
        /// Минимальная версия Android SDK
        /// </summary>
        public int MinSdkVersion { get; set; }

        /// <summary>
        /// Целевая версия Android SDK
        /// </summary>
        public int TargetSdkVersion { get; set; }

        /// <summary>
        /// Путь к извлеченной иконке приложения (если доступна)
        /// </summary>
        public string? IconPath { get; set; }

        /// <summary>
        /// Список разрешений, запрашиваемых приложением
        /// </summary>
        public List<string> Permissions { get; set; } = new List<string>();

        /// <summary>
        /// Размер APK файла в байтах
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// SHA-256 хэш APK файла для проверки целостности
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// Время последней модификации APK файла
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Проверить, что метаданные содержат минимально необходимую информацию
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(PackageName) &&
                   VersionCode > 0 &&
                   MinSdkVersion > 0 &&
                   TargetSdkVersion > 0;
        }

        /// <summary>
        /// Получить отображаемое название приложения или fallback на package name
        /// </summary>
        public string GetDisplayName()
        {
            return !string.IsNullOrWhiteSpace(AppName) ? AppName : PackageName;
        }

        /// <summary>
        /// Получить версию в формате "VersionName (VersionCode)"
        /// </summary>
        public string GetVersionString()
        {
            if (!string.IsNullOrWhiteSpace(VersionName))
            {
                return $"{VersionName} ({VersionCode})";
            }
            return VersionCode.ToString();
        }

        /// <summary>
        /// Проверить совместимость с минимальной версией Android
        /// </summary>
        public bool IsCompatibleWithAndroidVersion(int androidSdkVersion)
        {
            return MinSdkVersion <= androidSdkVersion;
        }
    }
}
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models.Android
{
    /// <summary>
    /// Результат установки APK приложения в WSA
    /// </summary>
    public class ApkInstallResult
    {
        /// <summary>
        /// Успешно ли прошла установка
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Package name установленного приложения
        /// </summary>
        public string? PackageName { get; set; }

        /// <summary>
        /// Метод установки, который был использован
        /// </summary>
        public InstallationMethod InstallationMethod { get; set; } = InstallationMethod.Unknown;

        /// <summary>
        /// Список установленных пакетов (для Split/XAPK установок может быть несколько)
        /// </summary>
        public List<string> InstalledPackages { get; set; } = new();

        /// <summary>
        /// Время выполнения установки в миллисекундах
        /// </summary>
        public long InstallDurationMs { get; set; }

        /// <summary>
        /// Сообщение об ошибке, если установка не удалась
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Код ошибки ADB (если доступен)
        /// </summary>
        public int? ErrorCode { get; set; }

        /// <summary>
        /// Время установки
        /// </summary>
        public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Размер установленного приложения в байтах
        /// </summary>
        public long? InstalledSizeBytes { get; set; }

        /// <summary>
        /// Дополнительная информация от ADB команды
        /// </summary>
        public string? AdditionalInfo { get; set; }

        /// <summary>
        /// Создать результат успешной установки
        /// </summary>
        public static ApkInstallResult CreateSuccess(string packageName, long? installedSize = null)
        {
            return new ApkInstallResult
            {
                Success = true,
                PackageName = packageName,
                InstalledAt = DateTime.UtcNow,
                InstalledSizeBytes = installedSize
            };
        }

        /// <summary>
        /// Создать результат неудачной установки
        /// </summary>
        public static ApkInstallResult CreateFailure(string errorMessage, int? errorCode = null)
        {
            return new ApkInstallResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                InstalledAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Получить детальное описание результата
        /// </summary>
        public string GetDescription()
        {
            if (Success)
            {
                var description = $"Successfully installed {PackageName}";
                if (InstalledSizeBytes.HasValue)
                {
                    var sizeMB = InstalledSizeBytes.Value / (1024 * 1024);
                    description += $" ({sizeMB:F1} MB)";
                }
                return description;
            }
            else
            {
                var description = $"Installation failed: {ErrorMessage}";
                if (ErrorCode.HasValue)
                {
                    description += $" (Error code: {ErrorCode})";
                }
                return description;
            }
        }
    }
}
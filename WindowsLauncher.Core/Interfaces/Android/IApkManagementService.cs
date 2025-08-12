using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Core.Interfaces.Android
{
    /// <summary>
    /// Сервис для управления APK файлами
    /// Отвечает за: валидацию, извлечение метаданных, установку APK/XAPK файлов
    /// </summary>
    public interface IApkManagementService
    {
        /// <summary>
        /// Проверить корректность APK или XAPK файла
        /// </summary>
        /// <param name="apkPath">Путь к APK/XAPK файлу</param>
        /// <returns>True, если файл корректен и может быть установлен</returns>
        Task<bool> ValidateApkFileAsync(string apkPath);

        /// <summary>
        /// Извлечь метаданные из APK файла с fallback стратегиями
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Метаданные APK или null, если извлечение не удалось</returns>
        Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath);

        /// <summary>
        /// Установить APK приложение в WSA с прогрессом
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <param name="progress">Callback для отслеживания прогресса установки</param>
        /// <param name="cancellationToken">Токен для отмены операции</param>
        /// <returns>Результат установки APK</returns>
        Task<ApkInstallResult> InstallApkAsync(string apkPath, IProgress<ApkInstallProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Проверить совместимость APK с текущей версией Android в WSA
        /// </summary>
        /// <param name="apkMetadata">Метаданные APK для проверки</param>
        /// <returns>True, если APK совместим с WSA</returns>
        Task<bool> IsApkCompatibleWithWSAAsync(ApkMetadata apkMetadata);

        /// <summary>
        /// Получить размер APK файла и его хэш для проверки целостности
        /// </summary>
        /// <param name="apkPath">Путь к APK файлу</param>
        /// <returns>Информация о файле или null при ошибке</returns>
        Task<ApkFileInfo?> GetApkFileInfoAsync(string apkPath);

        /// <summary>
        /// Событие прогресса установки APK
        /// </summary>
        event EventHandler<ApkInstallProgressEventArgs>? InstallProgressChanged;
    }

    /// <summary>
    /// Прогресс установки APK файла
    /// </summary>
    public class ApkInstallProgress
    {
        public string Stage { get; set; } = "";
        public int Percent { get; set; }
        public string Details { get; set; } = "";
        public long? BytesTransferred { get; set; }
        public long? TotalBytes { get; set; }
    }

    /// <summary>
    /// Аргументы события прогресса установки APK
    /// </summary>
    public class ApkInstallProgressEventArgs : EventArgs
    {
        public string ApkPath { get; set; } = "";
        public ApkInstallProgress Progress { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Информация о APK файле
    /// </summary>
    public class ApkFileInfo
    {
        public string FilePath { get; set; } = "";
        public long SizeBytes { get; set; }
        public string FileHash { get; set; } = "";
        public DateTime LastModified { get; set; }
        public bool IsXapk { get; set; }
    }
}
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using System.IO;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления данными приложения (БД, конфигурации, логи)
    /// </summary>
    public class ApplicationDataManager
    {
        private readonly ILogger<ApplicationDataManager> _logger;
        private readonly IDatabaseConfigurationService _dbConfigService;
        private readonly string _appDataPath;

        public ApplicationDataManager(
            ILogger<ApplicationDataManager> logger,
            IDatabaseConfigurationService dbConfigService)
        {
            _logger = logger;
            _dbConfigService = dbConfigService;
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsLauncher"
            );
        }

        /// <summary>
        /// Полная очистка всех данных приложения
        /// </summary>
        public async Task ClearAllDataAsync()
        {
            _logger.LogWarning("Starting complete application data cleanup");

            try
            {
                // 1. Удаляем конфигурацию БД
                await _dbConfigService.DeleteConfigurationAsync();

                // 2. Удаляем файлы баз данных
                await DeleteDatabaseFilesAsync();

                // 3. Удаляем логи (опционально)
                DeleteLogFiles();

                // 4. Удаляем другие конфигурационные файлы
                DeleteOtherConfigFiles();

                _logger.LogInformation("Application data cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup application data");
                throw;
            }
        }

        /// <summary>
        /// Удалить только базу данных (сохранить конфигурацию)
        /// </summary>
        public async Task ClearDatabaseOnlyAsync()
        {
            _logger.LogInformation("Starting database cleanup (keeping configuration)");

            try
            {
                await DeleteDatabaseFilesAsync();
                _logger.LogInformation("Database cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup database");
                throw;
            }
        }

        /// <summary>
        /// Получить информацию о данных приложения
        /// </summary>
        public Task<ApplicationDataInfo> GetDataInfoAsync()
        {
            var info = new ApplicationDataInfo
            {
                AppDataPath = _appDataPath,
                ConfigurationExists = _dbConfigService.ConfigurationFileExists(),
                ConfigurationPath = _dbConfigService.GetConfigurationFilePath()
            };

            // Ищем файлы БД
            var dbFiles = new List<string>();
            if (Directory.Exists(_appDataPath))
            {
                var sqliteFiles = Directory.GetFiles(_appDataPath, "*.db", SearchOption.AllDirectories);
                var firebirdFiles = Directory.GetFiles(_appDataPath, "*.fdb", SearchOption.AllDirectories);
                
                dbFiles.AddRange(sqliteFiles);
                dbFiles.AddRange(firebirdFiles);
            }

            info.DatabaseFiles = dbFiles.ToArray();
            info.TotalDataSize = CalculateDirectorySize(_appDataPath);

            return Task.FromResult(info);
        }

        private Task DeleteDatabaseFilesAsync()
        {
            if (!Directory.Exists(_appDataPath))
                return Task.CompletedTask;

            // Удаляем SQLite файлы
            var sqliteFiles = Directory.GetFiles(_appDataPath, "*.db", SearchOption.AllDirectories);
            foreach (var file in sqliteFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted SQLite database: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete database file: {File}", file);
                }
            }

            // Удаляем Firebird файлы
            var firebirdFiles = Directory.GetFiles(_appDataPath, "*.fdb", SearchOption.AllDirectories);
            foreach (var file in firebirdFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted Firebird database: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete database file: {File}", file);
                }
            }
            
            return Task.CompletedTask;
        }

        private void DeleteLogFiles()
        {
            if (!Directory.Exists(_appDataPath))
                return;

            var logFiles = Directory.GetFiles(_appDataPath, "*.log", SearchOption.AllDirectories);
            foreach (var file in logFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogDebug("Deleted log file: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file: {File}", file);
                }
            }
        }

        private void DeleteOtherConfigFiles()
        {
            if (!Directory.Exists(_appDataPath))
                return;

            // Удаляем файлы настроек языка, кэша и т.д.
            var configFiles = new[]
            {
                "language-settings.json",
                "user-preferences.json",
                "cache.json"
            };

            foreach (var fileName in configFiles)
            {
                var filePath = Path.Combine(_appDataPath, fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.LogDebug("Deleted config file: {File}", filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete config file: {File}", filePath);
                    }
                }
            }
        }

        private long CalculateDirectorySize(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return 0;

            try
            {
                return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Информация о данных приложения
    /// </summary>
    public class ApplicationDataInfo
    {
        public string AppDataPath { get; set; } = "";
        public bool ConfigurationExists { get; set; }
        public string ConfigurationPath { get; set; } = "";
        public string[] DatabaseFiles { get; set; } = Array.Empty<string>();
        public long TotalDataSize { get; set; }

        public string FormattedDataSize
        {
            get
            {
                if (TotalDataSize == 0) return "0 B";
                
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = TotalDataSize;
                int order = 0;
                
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }
}
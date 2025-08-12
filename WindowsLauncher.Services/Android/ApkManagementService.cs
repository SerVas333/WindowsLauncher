using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Services.Android
{
    /// <summary>
    /// Сервис для управления APK файлами с поддержкой fallback стратегий и прогресса установки
    /// </summary>
    public class ApkManagementService : IApkManagementService
    {
        private readonly IWSAConnectionService _connectionService;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<ApkManagementService> _logger;
        
        private string? _aaptPath;
        private readonly SemaphoreSlim _installSemaphore; // Ограничиваем одновременные установки

        public event EventHandler<ApkInstallProgressEventArgs>? InstallProgressChanged;

        public ApkManagementService(
            IWSAConnectionService connectionService,
            IProcessExecutor processExecutor,
            ILogger<ApkManagementService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _installSemaphore = new SemaphoreSlim(1, 1); // Только одна установка за раз

            _logger.LogDebug("ApkManagementService initialized");
        }

        public async Task<bool> ValidateApkFileAsync(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
            {
                _logger.LogWarning("APK path is null or empty");
                return false;
            }

            // Проверяем расширение файла
            bool isApk = apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase);
            bool isXapk = apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase);
            
            if (!isApk && !isXapk)
            {
                _logger.LogWarning("File does not have .apk or .xapk extension: {ApkPath}", apkPath);
                return false;
            }

            if (!File.Exists(apkPath))
            {
                _logger.LogWarning("APK file does not exist: {ApkPath}", apkPath);
                return false;
            }

            try
            {
                // Для XAPK файлов проверяем как ZIP архив
                if (isXapk)
                {
                    return await ValidateXapkFileAsync(apkPath);
                }
                
                // Для APK файлов используем каскадную проверку
                return await ValidateApkWithFallbackAsync(apkPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while validating APK: {ApkPath}", apkPath);
                return false;
            }
        }

        public async Task<ApkMetadata?> ExtractApkMetadataAsync(string apkPath)
        {
            try
            {
                if (!await ValidateApkFileAsync(apkPath))
                {
                    _logger.LogWarning("APK validation failed: {ApkPath}", apkPath);
                    return null;
                }

                _logger.LogDebug("Extracting APK metadata: {ApkPath}", apkPath);

                // Для XAPK файлов используем специальную обработку
                if (apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExtractXapkMetadataAsync(apkPath);
                }

                ApkMetadata? metadata = null;
                
                // 1. Приоритет: AAPT (самый точный метод)
                await EnsureAaptAvailableAsync();
                if (!string.IsNullOrEmpty(_aaptPath))
                {
                    metadata = await ExtractMetadataWithAaptAsync(apkPath);
                    if (metadata != null)
                    {
                        _logger.LogDebug("Successfully extracted metadata with AAPT: {PackageName}", metadata.PackageName);
                    }
                }
                
                // 2. Fallback: читаем APK как ZIP архив
                if (metadata == null)
                {
                    _logger.LogInformation("AAPT extraction failed, falling back to ZIP-based extraction: {ApkPath}", apkPath);
                    metadata = await ExtractMetadataFromZipAsync(apkPath);
                    
                    if (metadata != null)
                    {
                        _logger.LogDebug("Successfully extracted metadata via ZIP: {PackageName}", metadata.PackageName);
                    }
                }
                
                // 3. Final fallback: базовые метаданные из имени файла
                if (metadata == null)
                {
                    _logger.LogWarning("All metadata extraction methods failed, using filename-based fallback: {ApkPath}", apkPath);
                    metadata = ExtractBasicMetadataFromFilename(apkPath);
                }
                
                if (metadata != null)
                {
                    // Добавляем информацию о файле
                    var fileInfo = new FileInfo(apkPath);
                    metadata.FileSizeBytes = fileInfo.Length;
                    metadata.LastModified = fileInfo.LastWriteTime;
                    metadata.FileHash = await CalculateFileHashAsync(apkPath);
                    
                    _logger.LogInformation("Successfully extracted metadata for APK: {PackageName} v{Version} ({SizeMB:F1} MB)", 
                        metadata.PackageName, metadata.GetVersionString(), metadata.FileSizeBytes / (1024.0 * 1024.0));
                }
                else
                {
                    _logger.LogError("Failed to extract APK metadata using all available methods: {ApkPath}", apkPath);
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while extracting APK metadata: {ApkPath}", apkPath);
                return null;
            }
        }

        public async Task<ApkInstallResult> InstallApkAsync(string apkPath, IProgress<ApkInstallProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            // Ограничиваем одновременные установки
            await _installSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                _logger.LogInformation("Starting APK installation: {ApkPath}", apkPath);
                
                var fileInfo = new FileInfo(apkPath);
                long totalBytes = fileInfo.Length;
                
                ReportProgress(progress, apkPath, "Validating APK file", 5, bytesTransferred: 0, totalBytes: totalBytes);

                // Проверяем подключение к WSA
                if (!await _connectionService.ConnectToWSAAsync())
                {
                    return ApkInstallResult.CreateFailure("Failed to connect to WSA");
                }

                ReportProgress(progress, apkPath, "Extracting metadata", 15, bytesTransferred: 0, totalBytes: totalBytes);

                // Извлекаем метаданные
                var metadata = await ExtractApkMetadataAsync(apkPath);
                if (metadata == null)
                {
                    return ApkInstallResult.CreateFailure("Invalid APK file or failed to extract metadata");
                }

                ReportProgress(progress, apkPath, "Checking compatibility", 25, bytesTransferred: 0, totalBytes: totalBytes);

                // Проверяем совместимость
                if (!await IsApkCompatibleWithWSAAsync(metadata))
                {
                    return ApkInstallResult.CreateFailure($"APK is not compatible with current WSA version. Min SDK: {metadata.MinSdkVersion}");
                }

                // Проверяем, является ли файл XAPK
                if (apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Installing XAPK file: {XapkPath}", apkPath);
                    return await InstallXapkWithProgressAsync(apkPath, metadata, progress, cancellationToken);
                }

                ReportProgress(progress, apkPath, "Starting installation", 40, bytesTransferred: 0, totalBytes: totalBytes);

                // Установка обычного APK
                var result = await InstallSingleApkWithFallbackAsync(apkPath, metadata, progress, totalBytes, cancellationToken);
                
                if (result.Success)
                {
                    ReportProgress(progress, apkPath, "Installation completed", 100, bytesTransferred: totalBytes, totalBytes: totalBytes);
                    _logger.LogInformation("Successfully installed APK: {PackageName}", metadata.PackageName);
                }
                else
                {
                    ReportProgress(progress, apkPath, "Installation failed", 100, bytesTransferred: 0, totalBytes: totalBytes);
                    _logger.LogError("Failed to install APK: {PackageName}. Error: {Error}", metadata.PackageName, result.ErrorMessage);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("APK installation was cancelled: {ApkPath}", apkPath);
                ReportProgress(progress, apkPath, "Installation cancelled", 100, bytesTransferred: 0, totalBytes: 0);
                return ApkInstallResult.CreateFailure("Installation cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during APK installation: {ApkPath}", apkPath);
                ReportProgress(progress, apkPath, "Installation error", 100, bytesTransferred: 0, totalBytes: 0);
                return ApkInstallResult.CreateFailure($"Installation exception: {ex.Message}");
            }
            finally
            {
                _installSemaphore.Release();
            }
        }

        public async Task<bool> IsApkCompatibleWithWSAAsync(ApkMetadata apkMetadata)
        {
            try
            {
                // Получаем версию Android в WSA
                var androidVersion = await _connectionService.GetAndroidVersionAsync();
                
                if (string.IsNullOrEmpty(androidVersion))
                {
                    _logger.LogWarning("Could not determine Android version in WSA, assuming compatibility");
                    return true; // Если не можем определить версию, считаем совместимым
                }

                // Пытаемся конвертировать версию Android в SDK level
                int wsaSdkLevel = ConvertAndroidVersionToSdkLevel(androidVersion);
                
                bool compatible = apkMetadata.MinSdkVersion <= wsaSdkLevel;
                
                if (!compatible)
                {
                    _logger.LogWarning("APK compatibility check failed: APK requires SDK {MinSdk}, WSA has SDK {WsaSdk} (Android {AndroidVersion})", 
                        apkMetadata.MinSdkVersion, wsaSdkLevel, androidVersion);
                }
                else
                {
                    _logger.LogDebug("APK compatibility check passed: APK requires SDK {MinSdk}, WSA has SDK {WsaSdk}", 
                        apkMetadata.MinSdkVersion, wsaSdkLevel);
                }

                return compatible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during APK compatibility check");
                return true; // В случае ошибки считаем совместимым
            }
        }

        public async Task<ApkFileInfo?> GetApkFileInfoAsync(string apkPath)
        {
            try
            {
                if (!File.Exists(apkPath))
                {
                    _logger.LogWarning("APK file does not exist: {ApkPath}", apkPath);
                    return null;
                }

                var fileInfo = new FileInfo(apkPath);
                
                return new ApkFileInfo
                {
                    FilePath = apkPath,
                    SizeBytes = fileInfo.Length,
                    FileHash = await CalculateFileHashAsync(apkPath),
                    LastModified = fileInfo.LastWriteTime,
                    IsXapk = apkPath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting APK file info: {ApkPath}", apkPath);
                return null;
            }
        }

        #region Private Methods

        private async Task<bool> ValidateApkWithFallbackAsync(string apkPath)
        {
            // 1. Попробуем использовать AAPT
            await EnsureAaptAvailableAsync();
            
            if (!string.IsNullOrEmpty(_aaptPath))
            {
                try
                {
                    var result = await _processExecutor.ExecuteAsync(_aaptPath, $"dump badging \"{apkPath}\"", 15000);
                    
                    bool isValidWithAapt = result.IsSuccess && 
                                          result.StandardOutput.Contains("package:") &&
                                          result.StandardOutput.Contains("versionCode");

                    if (isValidWithAapt)
                    {
                        _logger.LogDebug("APK validated successfully with AAPT: {ApkPath}", apkPath);
                        return true;
                    }
                    else
                    {
                        _logger.LogDebug("AAPT validation failed: {ApkPath}. Error: {Error}", apkPath, result.StandardError);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Exception during AAPT validation, falling back to ZIP validation");
                }
            }

            // 2. Fallback: проверяем как ZIP архив
            try
            {
                using var zip = new ZipArchive(File.OpenRead(apkPath), ZipArchiveMode.Read);
                
                // Ищем AndroidManifest.xml
                var manifestEntry = zip.GetEntry("AndroidManifest.xml");
                bool hasManifest = manifestEntry != null;

                // Ищем classes.dex (основной исполняемый файл Android)
                var classesDexEntry = zip.GetEntry("classes.dex");
                bool hasClassesDex = classesDexEntry != null;

                bool isValid = hasManifest && hasClassesDex;
                
                if (isValid)
                {
                    _logger.LogDebug("APK validated successfully as ZIP archive: {ApkPath}", apkPath);
                }
                else
                {
                    _logger.LogWarning("APK ZIP validation failed: Manifest={HasManifest}, ClassesDex={HasClassesDex}", 
                        hasManifest, hasClassesDex);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate APK as ZIP archive: {ApkPath}", apkPath);
                return false;
            }
        }

        private async Task<bool> ValidateXapkFileAsync(string xapkPath)
        {
            try
            {
                _logger.LogDebug("Validating XAPK file: {XapkPath}", xapkPath);

                using var archive = ZipFile.OpenRead(xapkPath);
                
                // Проверяем наличие manifest.json
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("XAPK validation failed: manifest.json not found in {XapkPath}", xapkPath);
                    return false;
                }

                // Проверяем наличие хотя бы одного APK файла
                bool hasApkFiles = archive.Entries.Any(entry => entry.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
                if (!hasApkFiles)
                {
                    _logger.LogWarning("XAPK validation failed: no APK files found in {XapkPath}", xapkPath);
                    return false;
                }

                _logger.LogDebug("XAPK validation successful: {XapkPath}", xapkPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while validating XAPK: {XapkPath}", xapkPath);
                return false;
            }
        }

        private async Task<ApkMetadata?> ExtractMetadataWithAaptAsync(string apkPath)
        {
            try
            {
                var result = await _processExecutor.ExecuteAsync(_aaptPath!, $"dump badging \"{apkPath}\"", 20000);
                
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("AAPT failed to extract APK metadata: {Error}", result.StandardError);
                    return null;
                }

                return ParseApkMetadata(result.StandardOutput);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during AAPT metadata extraction: {ApkPath}", apkPath);
                return null;
            }
        }

        private async Task<ApkMetadata?> ExtractMetadataFromZipAsync(string apkPath)
        {
            try
            {
                using var zip = new ZipArchive(File.OpenRead(apkPath), ZipArchiveMode.Read);
                
                // Ищем AndroidManifest.xml
                var manifestEntry = zip.GetEntry("AndroidManifest.xml");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("AndroidManifest.xml not found in APK: {ApkPath}", apkPath);
                    return null;
                }
                
                // Создаём базовые метаданные из имени файла
                var metadata = ExtractBasicMetadataFromFilename(apkPath);
                
                _logger.LogInformation("Extracted basic metadata from APK structure: Package={Package}, Version={Version}", 
                    metadata.PackageName, metadata.VersionName);
                    
                return metadata.IsValid() ? metadata : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract metadata from APK as ZIP: {ApkPath}", apkPath);
                return null;
            }
        }

        private ApkMetadata ExtractBasicMetadataFromFilename(string apkPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(apkPath);
            var cleanName = Regex.Replace(fileName, @"[^\w\.]", ".");
            
            var metadata = new ApkMetadata
            {
                PackageName = $"com.unknown.{cleanName.ToLowerInvariant()}",
                MinSdkVersion = 21, // Android 5.0 - разумный минимум
                TargetSdkVersion = 33, // Android 13 - современная цель
                AppName = fileName,
                VersionCode = 1
            };

            // Примитивное извлечение версии из имени файла
            var versionMatch = Regex.Match(fileName, @"(\d+\.)+\d+");
            if (versionMatch.Success)
            {
                metadata.VersionName = versionMatch.Value;
                var versionParts = versionMatch.Value.Split('.');
                if (versionParts.Length > 0 && int.TryParse(string.Join("", versionParts), out var versionCode))
                {
                    metadata.VersionCode = Math.Max(1, versionCode % 1000000); // Ограничиваем размер
                }
            }
            else
            {
                metadata.VersionName = "1.0.0";
            }

            return metadata;
        }

        private ApkMetadata? ParseApkMetadata(string aaptOutput)
        {
            if (string.IsNullOrWhiteSpace(aaptOutput))
                return null;

            try
            {
                var metadata = new ApkMetadata();
                var lines = aaptOutput.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("package:"))
                    {
                        metadata.PackageName = ExtractValue(trimmedLine, "name='", "'") ?? "";
                        if (int.TryParse(ExtractValue(trimmedLine, "versionCode='", "'"), out int versionCode))
                        {
                            metadata.VersionCode = versionCode;
                        }
                        metadata.VersionName = ExtractValue(trimmedLine, "versionName='", "'");
                    }
                    else if (trimmedLine.StartsWith("application-label:"))
                    {
                        var labelValue = trimmedLine.Substring("application-label:".Length).Trim();
                        
                        // Убираем кавычки с начала и конца строки
                        if (labelValue.Length >= 2)
                        {
                            if ((labelValue.StartsWith("'") && labelValue.EndsWith("'")) ||
                                (labelValue.StartsWith("\"") && labelValue.EndsWith("\"")))
                            {
                                labelValue = labelValue.Substring(1, labelValue.Length - 2);
                            }
                        }
                        
                        metadata.AppName = labelValue.Trim('\r', '\n', '\t', ' ');
                    }
                    else if (trimmedLine.StartsWith("sdkVersion:"))
                    {
                        if (int.TryParse(ExtractValue(trimmedLine, "sdkVersion:'", "'"), out int minSdk))
                        {
                            metadata.MinSdkVersion = minSdk;
                        }
                    }
                    else if (trimmedLine.StartsWith("targetSdkVersion:"))
                    {
                        if (int.TryParse(ExtractValue(trimmedLine, "targetSdkVersion:'", "'"), out int targetSdk))
                        {
                            metadata.TargetSdkVersion = targetSdk;
                        }
                    }
                    else if (trimmedLine.StartsWith("uses-permission:"))
                    {
                        var permission = ExtractValue(trimmedLine, "name='", "'");
                        if (!string.IsNullOrEmpty(permission))
                        {
                            metadata.Permissions.Add(permission);
                        }
                    }
                }

                return metadata.IsValid() ? metadata : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse APK metadata from AAPT output");
                return null;
            }
        }

        private static string? ExtractValue(string input, string startPattern, string endPattern)
        {
            var startIndex = input.IndexOf(startPattern);
            if (startIndex == -1) return null;
            
            startIndex += startPattern.Length;
            var endIndex = input.IndexOf(endPattern, startIndex);
            if (endIndex == -1) return null;
            
            return input.Substring(startIndex, endIndex - startIndex).Trim('\r', '\n');
        }

        private async Task<ApkInstallResult> InstallSingleApkWithFallbackAsync(string apkPath, ApkMetadata metadata, IProgress<ApkInstallProgress>? progress, long totalBytes, CancellationToken cancellationToken)
        {
            var adbPath = await GetAdbPathAsync();
            if (string.IsNullOrEmpty(adbPath))
            {
                return ApkInstallResult.CreateFailure("ADB not available");
            }

            ReportProgress(progress, apkPath, "Installing APK", 60, bytesTransferred: totalBytes / 4, totalBytes: totalBytes);

            // 1. Сначала пробуем обычную установку
            var result = await _processExecutor.ExecuteAsync(adbPath, $"install \"{apkPath}\"", 60000);
            
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsSuccess && result.StandardOutput.Contains("Success"))
            {
                ReportProgress(progress, apkPath, "Installation successful", 90, bytesTransferred: totalBytes * 3 / 4, totalBytes: totalBytes);
                return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
            }
            
            // 2. Проверяем специфичные ошибки и пробуем альтернативные методы
            string errorOutput = result.StandardError + " " + result.StandardOutput;
            
            ReportProgress(progress, apkPath, "Retrying with alternative method", 75, bytesTransferred: totalBytes / 2, totalBytes: totalBytes);

            if (errorOutput.Contains("INSTALL_FAILED_MISSING_SPLIT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Standard install failed with MISSING_SPLIT, attempting alternative installation methods");
                
                // Пробуем установку с флагами -t (test APKs) и -r (replace)
                var splitResult = await _processExecutor.ExecuteAsync(adbPath, $"install -t -r \"{apkPath}\"", 60000);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                if (splitResult.IsSuccess && splitResult.StandardOutput.Contains("Success"))
                {
                    ReportProgress(progress, apkPath, "Installation successful (alternative method)", 90, bytesTransferred: totalBytes * 3 / 4, totalBytes: totalBytes);
                    return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
                }
                
                // Финальная попытка с полными разрешениями
                var finalResult = await _processExecutor.ExecuteAsync(adbPath, $"install -g -t -r \"{apkPath}\"", 60000);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                if (finalResult.IsSuccess && finalResult.StandardOutput.Contains("Success"))
                {
                    ReportProgress(progress, apkPath, "Installation successful (forced method)", 90, bytesTransferred: totalBytes * 3 / 4, totalBytes: totalBytes);
                    return ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes);
                }
                
                var splitErrorMessage = ParseAdbInstallError(finalResult.StandardError + " " + finalResult.StandardOutput);
                _logger.LogError("All installation methods failed for APK: {PackageName}. Last error: {Error}", metadata.PackageName, splitErrorMessage);
                
                return ApkInstallResult.CreateFailure($"Installation failed (all methods attempted): {splitErrorMessage}");
            }
            else
            {
                var errorMessage = ParseAdbInstallError(errorOutput);
                _logger.LogError("Failed to install APK: {PackageName}. Error: {Error}", metadata.PackageName, errorMessage);
                return ApkInstallResult.CreateFailure(errorMessage, result.ExitCode);
            }
        }

        private static string ParseAdbInstallError(string errorOutput)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                return "Unknown installation error";

            // Переводим наиболее частые ошибки на понятный язык
            if (errorOutput.Contains("INSTALL_FAILED_ALREADY_EXISTS"))
                return "Приложение уже установлено";
            if (errorOutput.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
                return "Недостаточно места для установки";
            if (errorOutput.Contains("INSTALL_FAILED_INVALID_APK"))
                return "Поврежденный APK файл";
            if (errorOutput.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
                return "Нельзя установить более старую версию";
            if (errorOutput.Contains("INSTALL_FAILED_PERMISSION_MODEL"))
                return "Несовместимая модель разрешений";
            if (errorOutput.Contains("INSTALL_FAILED_MISSING_SPLIT"))
                return "APK требует дополнительных split-файлов";

            return errorOutput.Length > 100 ? errorOutput.Substring(0, 100) + "..." : errorOutput;
        }

        private async Task<ApkMetadata?> ExtractXapkMetadataAsync(string xapkPath)
        {
            try
            {
                _logger.LogDebug("Extracting XAPK metadata: {XapkPath}", xapkPath);

                using var archive = ZipFile.OpenRead(xapkPath);
                
                // Читаем manifest.json
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    _logger.LogWarning("manifest.json not found in XAPK: {XapkPath}", xapkPath);
                    return null;
                }

                using var manifestStream = manifestEntry.Open();
                using var reader = new StreamReader(manifestStream);
                var manifestJson = await reader.ReadToEndAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var xapkMetadata = JsonSerializer.Deserialize<XapkMetadata>(manifestJson, options);
                if (xapkMetadata == null || !xapkMetadata.IsValid())
                {
                    _logger.LogWarning("Invalid XAPK manifest in: {XapkPath}", xapkPath);
                    return null;
                }

                // Конвертируем XAPK метаданные в APK метаданные
                var apkMetadata = xapkMetadata.ToApkMetadata();

                // Добавляем информацию о файле
                var fileInfo = new FileInfo(xapkPath);
                apkMetadata.FileSizeBytes = fileInfo.Length;
                apkMetadata.LastModified = fileInfo.LastWriteTime;

                _logger.LogInformation("Successfully extracted XAPK metadata: {PackageName} v{Version} ({SizeMB:F1} MB)",
                    apkMetadata.PackageName, apkMetadata.GetVersionString(), 
                    apkMetadata.FileSizeBytes / (1024.0 * 1024.0));

                return apkMetadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while extracting XAPK metadata: {XapkPath}", xapkPath);
                return null;
            }
        }

        private async Task<ApkInstallResult> InstallXapkWithProgressAsync(string xapkPath, ApkMetadata metadata, IProgress<ApkInstallProgress>? progress, CancellationToken cancellationToken)
        {
            string? tempDir = null;
            try
            {
                // Создаем временную директорию для распаковки XAPK
                tempDir = Path.Combine(Path.GetTempPath(), $"xapk_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                ReportProgress(progress, xapkPath, "Extracting XAPK", 50, bytesTransferred: 0, totalBytes: metadata.FileSizeBytes);

                _logger.LogDebug("Extracting XAPK to temporary directory: {TempDir}", tempDir);

                // Распаковываем XAPK (это ZIP архив)
                ZipFile.ExtractToDirectory(xapkPath, tempDir);

                cancellationToken.ThrowIfCancellationRequested();

                // Ищем APK файлы
                var apkFiles = Directory.GetFiles(tempDir, "*.apk");
                if (apkFiles.Length == 0)
                {
                    return ApkInstallResult.CreateFailure("No APK files found in XAPK");
                }

                ReportProgress(progress, xapkPath, "Installing APK files", 70, bytesTransferred: metadata.FileSizeBytes / 2, totalBytes: metadata.FileSizeBytes);

                // Если есть несколько APK файлов, используем множественную установку
                if (apkFiles.Length > 1)
                {
                    return await InstallMultipleApksAsync(apkFiles, metadata, progress, cancellationToken);
                }
                else
                {
                    // Один APK файл - обычная установка
                    var singleApkPath = apkFiles[0];
                    _logger.LogDebug("Installing single APK from XAPK: {ApkPath}", singleApkPath);
                    
                    var singleApkMetadata = await ExtractApkMetadataAsync(singleApkPath) ?? metadata;
                    var result = await InstallSingleApkWithFallbackAsync(singleApkPath, singleApkMetadata, progress, metadata.FileSizeBytes, cancellationToken);
                    
                    return result.Success 
                        ? ApkInstallResult.CreateSuccess(metadata.PackageName, metadata.FileSizeBytes)
                        : result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing XAPK: {XapkPath}", xapkPath);
                return ApkInstallResult.CreateFailure($"XAPK installation exception: {ex.Message}");
            }
            finally
            {
                // Очищаем временную директорию
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        _logger.LogDebug("Cleaned up temporary XAPK directory: {TempDir}", tempDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary XAPK directory: {TempDir}", tempDir);
                    }
                }
            }
        }

        private async Task<ApkInstallResult> InstallMultipleApksAsync(string[] apkFiles, ApkMetadata xapkMetadata, IProgress<ApkInstallProgress>? progress, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Installing {Count} APK files from XAPK package: {PackageName}", 
                    apkFiles.Length, xapkMetadata.PackageName);

                var adbPath = await GetAdbPathAsync();
                if (string.IsNullOrEmpty(adbPath))
                {
                    return ApkInstallResult.CreateFailure("ADB not available");
                }

                ReportProgress(progress, string.Join(", ", apkFiles.Select(Path.GetFileName)), "Installing multiple APKs", 80, 
                    bytesTransferred: xapkMetadata.FileSizeBytes * 3 / 4, totalBytes: xapkMetadata.FileSizeBytes);

                // Используем adb install-multiple для установки всех APK файлов одновременно
                var apkPathsQuoted = string.Join(" ", apkFiles.Select(path => $"\"{path}\""));
                var installCommand = $"install-multiple -r {apkPathsQuoted}";

                var result = await _processExecutor.ExecuteAsync(adbPath, installCommand, 120000); // 2 минуты

                cancellationToken.ThrowIfCancellationRequested();

                if (result.IsSuccess && result.StandardOutput.Contains("Success"))
                {
                    _logger.LogInformation("Successfully installed XAPK with multiple APKs: {PackageName}", xapkMetadata.PackageName);
                    return ApkInstallResult.CreateSuccess(xapkMetadata.PackageName, xapkMetadata.FileSizeBytes);
                }
                else
                {
                    // Если пакетная установка не удалась, попробуем установить APK файлы по одному
                    _logger.LogWarning("Multiple APK install failed, trying individual installation for {PackageName}", xapkMetadata.PackageName);

                    foreach (var apkFile in apkFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        _logger.LogDebug("Installing individual APK: {ApkFile}", Path.GetFileName(apkFile));
                        
                        var individualMetadata = await ExtractApkMetadataAsync(apkFile) ?? xapkMetadata;
                        var individualResult = await InstallSingleApkWithFallbackAsync(apkFile, individualMetadata, progress, xapkMetadata.FileSizeBytes, cancellationToken);
                        
                        if (!individualResult.Success)
                        {
                            _logger.LogError("Failed to install APK: {ApkFile}. Error: {Error}", 
                                Path.GetFileName(apkFile), individualResult.ErrorMessage);
                            return individualResult;
                        }
                    }

                    _logger.LogInformation("Successfully installed all APK files individually for XAPK: {PackageName}", xapkMetadata.PackageName);
                    return ApkInstallResult.CreateSuccess(xapkMetadata.PackageName, xapkMetadata.FileSizeBytes);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while installing multiple APKs from XAPK");
                return ApkInstallResult.CreateFailure($"Multiple APK installation exception: {ex.Message}");
            }
        }

        private async Task EnsureAaptAvailableAsync()
        {
            if (!string.IsNullOrEmpty(_aaptPath))
            {
                return;
            }

            // Попробуем найти AAPT в PATH
            _aaptPath = await _processExecutor.GetCommandPathAsync("aapt");
            
            if (string.IsNullOrEmpty(_aaptPath))
            {
                // Поищем AAPT в стандартных локациях WindowsLauncher  
                var androidToolsPath = @"C:\WindowsLauncher\Tools\Android";
                if (Directory.Exists(androidToolsPath))
                {
                    // Ищем aapt.exe во всех подпапках
                    var aaptFiles = Directory.GetFiles(androidToolsPath, "aapt.exe", SearchOption.AllDirectories);
                    if (aaptFiles.Length > 0)
                    {
                        _aaptPath = aaptFiles[0];
                        _logger.LogInformation("Found AAPT at: {AaptPath}", _aaptPath);
                        return;
                    }
                }
                
                // Дополнительные локации для поиска
                var possiblePaths = new[]
                {
                    @"C:\WindowsLauncher\Tools\Android\android-14\aapt.exe",
                    @"C:\WindowsLauncher\Tools\Android\android-13\aapt.exe",
                    @"C:\WindowsLauncher\Tools\Android\aapt.exe", 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        @"WindowsLauncher\Tools\Android\aapt.exe")
                };
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        _aaptPath = path;
                        _logger.LogInformation("Found AAPT at: {AaptPath}", _aaptPath);
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(_aaptPath))
            {
                _logger.LogWarning("AAPT not found in PATH or standard locations. Will use fallback metadata extraction methods.");
            }
            else
            {
                _logger.LogDebug("Using AAPT from: {AaptPath}", _aaptPath);
            }
        }

        private async Task<string?> GetAdbPathAsync()
        {
            var connectionStatus = await _connectionService.GetConnectionStatusAsync();
            return connectionStatus.GetValueOrDefault("ADBPath", null) as string;
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate hash for file: {FilePath}", filePath);
                return "";
            }
        }

        private int ConvertAndroidVersionToSdkLevel(string androidVersion)
        {
            // Упрощенное соответствие версий Android и SDK Level
            return androidVersion switch
            {
                "14" => 34,
                "13" => 33,
                "12" => 31,
                "11" => 30,
                "10" => 29,
                "9" => 28,
                "8.1" => 27,
                "8.0" => 26,
                "7.1" => 25,
                "7.0" => 24,
                _ => 29 // По умолчанию Android 10
            };
        }

        private void ReportProgress(IProgress<ApkInstallProgress>? progress, string apkPath, string stage, int percent, long? bytesTransferred = null, long? totalBytes = null)
        {
            if (progress == null) return;

            var progressData = new ApkInstallProgress
            {
                Stage = stage,
                Percent = percent,
                BytesTransferred = bytesTransferred,
                TotalBytes = totalBytes,
                Details = $"Processing: {Path.GetFileName(apkPath)}"
            };

            progress.Report(progressData);

            // Также уведомляем через событие
            try
            {
                InstallProgressChanged?.Invoke(this, new ApkInstallProgressEventArgs
                {
                    ApkPath = apkPath,
                    Progress = progressData,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error firing InstallProgressChanged event");
            }
        }

        #endregion

        public void Dispose()
        {
            _installSemaphore?.Dispose();
        }
    }
}
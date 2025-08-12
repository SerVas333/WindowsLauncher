using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.IO.Compression;
using System.Text;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Android;
using WindowsLauncher.Services.Android;
using Xunit;

namespace WindowsLauncher.Tests.Services.Android
{
    /// <summary>
    /// Unit тесты для ApkManagementService - специализированного сервиса управления APK/XAPK файлами
    /// </summary>
    public class ApkManagementServiceTests : IDisposable
    {
        private readonly Mock<IWSAConnectionService> _mockConnectionService;
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<ILogger<ApkManagementService>> _mockLogger;
        private readonly ApkManagementService _service;
        private readonly string _tempDirectory;

        public ApkManagementServiceTests()
        {
            _mockConnectionService = new Mock<IWSAConnectionService>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLogger = new Mock<ILogger<ApkManagementService>>();
            _service = new ApkManagementService(_mockConnectionService.Object, _mockProcessExecutor.Object, _mockLogger.Object);
            
            // Создаем временную директорию для тестов
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ApkManagementServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            _service?.Dispose();
            
            // Очищаем временную директорию
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        #region ValidateApkFileAsync Tests

        [Fact]
        public async Task ValidateApkFileAsync_WithValidApkFile_ReturnsTrue()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "valid-app.apk");
            CreateMockApkFile(apkPath);

            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "package: name='com.example.app' versionCode='1' versionName='1.0'",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateApkFileAsync_WithNonExistentFile_ReturnsFalse()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "non-existent.apk");

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("not-an-apk.txt")]
        public async Task ValidateApkFileAsync_WithInvalidInput_ReturnsFalse(string apkPath)
        {
            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateApkFileAsync_WithXapkFile_ReturnsTrue()
        {
            // Arrange
            var xapkPath = Path.Combine(_tempDirectory, "app.xapk");
            CreateMockXapkFile(xapkPath);

            // Act
            var result = await _service.ValidateApkFileAsync(xapkPath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateApkFileAsync_WhenAaptNotAvailable_UsesFallbackValidation()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockProcessExecutor.Setup(x => x.IsCommandAvailableAsync("aapt"))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.True(result); // Fallback validation только проверяет расширение и существование файла
        }

        #endregion

        #region ExtractApkMetadataAsync Tests

        [Fact]
        public async Task ExtractApkMetadataAsync_WithValidApk_ReturnsCorrectMetadata()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            var aaptOutput = "package: name='com.example.testapp' versionCode='42' versionName='1.2.3' platformBuildVersionName='14'\n" +
                            "application-label:'Test Application'\n" +
                            "sdkVersion:'21'\n" +
                            "targetSdkVersion:'34'\n";

            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = aaptOutput,
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("com.example.testapp", result.PackageName);
            Assert.Equal(42, result.VersionCode);
            Assert.Equal("1.2.3", result.VersionName);
            Assert.Equal("Test Application", result.AppName);
            Assert.Equal(21, result.MinSdkVersion);
            Assert.Equal(34, result.TargetSdkVersion);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_WhenAaptFails_ReturnsNull()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "invalid.apk");
            CreateMockApkFile(apkPath);

            var aaptResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "ERROR: dump failed",
                IsSuccess = false
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_WithXapkFile_ExtractsFromMainApk()
        {
            // Arrange
            var xapkPath = Path.Combine(_tempDirectory, "app.xapk");
            CreateMockXapkFile(xapkPath, includeManifest: true, includeApks: true);

            var aaptOutput = "package: name='com.example.xapkapp' versionCode='100' versionName='2.0.0'\n" +
                            "application-label:'XAPK Test App'\n";

            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = aaptOutput,
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ExtractApkMetadataAsync(xapkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("com.example.xapkapp", result.PackageName);
            Assert.Equal("XAPK Test App", result.AppName);
            Assert.True(result.IsSplitApk); // XAPK файлы всегда считаются split APK
        }

        #endregion

        #region GetApkFileInfoAsync Tests

        [Fact]
        public async Task GetApkFileInfoAsync_WithValidApk_ReturnsFileInfo()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath, size: 1024 * 1024); // 1MB

            // Setup metadata extraction
            var metadata = new ApkMetadata
            {
                PackageName = "com.example.app",
                AppName = "Test App",
                VersionName = "1.0",
                VersionCode = 1
            };

            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "package: name='com.example.app' versionCode='1' versionName='1.0'\napplication-label:'Test App'\n",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.GetApkFileInfoAsync(apkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(apkPath, result.FilePath);
            Assert.True(result.FileSizeBytes > 0);
            Assert.Equal("APK", result.FileType);
            Assert.Equal(metadata.PackageName, result.PackageName);
            Assert.Equal(metadata.AppName, result.AppName);
        }

        [Fact]
        public async Task GetApkFileInfoAsync_WithXapkFile_ReturnsFileInfo()
        {
            // Arrange
            var xapkPath = Path.Combine(_tempDirectory, "app.xapk");
            CreateMockXapkFile(xapkPath, includeManifest: true, includeApks: true);

            // Setup metadata extraction
            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "package: name='com.example.xapkapp' versionCode='100' versionName='2.0.0'\napplication-label:'XAPK App'\n",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.GetApkFileInfoAsync(xapkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("XAPK", result.FileType);
            Assert.True(result.IsSplitPackage);
        }

        #endregion

        #region InstallApkAsync Tests

        [Fact]
        public async Task InstallApkAsync_WithValidApk_ReturnsSuccess()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);

            var installResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"install \"{apkPath}\"", It.IsAny<int>(), null))
                .ReturnsAsync(installResult);

            // Act
            var result = await _service.InstallApkAsync(apkPath, null, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("app.apk", result.InstalledPackages);
        }

        [Fact]
        public async Task InstallApkAsync_WithMultipleFallbackMethods_ReturnsSuccess()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);

            var failResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "INSTALL_FAILED_MISSING_SPLIT",
                IsSuccess = false
            };

            var successResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.SetupSequence(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(failResult)   // Первый метод не работает
                .ReturnsAsync(successResult); // Второй метод работает

            // Act
            var result = await _service.InstallApkAsync(apkPath, null, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task InstallApkAsync_WithProgressReporting_ReportsProgress()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);

            var installResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(installResult);

            var progressReports = new List<ApkInstallProgress>();
            var progress = new Progress<ApkInstallProgress>(p => progressReports.Add(p));

            // Act
            var result = await _service.InstallApkAsync(apkPath, progress, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(progressReports);
            Assert.Contains(progressReports, p => p.ProgressPercentage == 100);
        }

        [Fact]
        public async Task InstallApkAsync_WithCancellationToken_CancelsOperation()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Немедленно отменяем

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _service.InstallApkAsync(apkPath, null, cts.Token));
        }

        [Fact]
        public async Task InstallApkAsync_WithXapkFile_InstallsMultipleApks()
        {
            // Arrange
            var xapkPath = Path.Combine(_tempDirectory, "app.xapk");
            CreateMockXapkFile(xapkPath, includeManifest: true, includeApks: true);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);

            var installResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(installResult);

            // Act
            var result = await _service.InstallApkAsync(xapkPath, null, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(InstallationMethod.XAPK, result.InstallationMethod);
        }

        [Fact]
        public async Task InstallApkAsync_WhenConnectionFails_ReturnsFailure()
        {
            // Arrange
            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _service.InstallApkAsync(apkPath, null, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("WSA connection failed", result.ErrorMessage);
        }

        #endregion

        #region IsApkCompatibleWithWSAAsync Tests

        [Fact]
        public async Task IsApkCompatibleWithWSAAsync_WithCompatibleApk_ReturnsTrue()
        {
            // Arrange
            var metadata = new ApkMetadata
            {
                MinSdkVersion = 21,  // Android 5.0
                TargetSdkVersion = 33, // Android 13
                PackageName = "com.example.app"
            };

            _mockConnectionService.Setup(x => x.GetAndroidVersionAsync())
                .ReturnsAsync("13");

            // Act
            var result = await _service.IsApkCompatibleWithWSAAsync(metadata);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsApkCompatibleWithWSAAsync_WithIncompatibleMinSdk_ReturnsFalse()
        {
            // Arrange
            var metadata = new ApkMetadata
            {
                MinSdkVersion = 35,  // Будущая версия Android
                TargetSdkVersion = 35,
                PackageName = "com.example.app"
            };

            _mockConnectionService.Setup(x => x.GetAndroidVersionAsync())
                .ReturnsAsync("13"); // WSA Android 13

            // Act
            var result = await _service.IsApkCompatibleWithWSAAsync(metadata);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsApkCompatibleWithWSAAsync_WhenWSAVersionUnknown_ReturnsTrue()
        {
            // Arrange
            var metadata = new ApkMetadata
            {
                MinSdkVersion = 21,
                TargetSdkVersion = 33,
                PackageName = "com.example.app"
            };

            _mockConnectionService.Setup(x => x.GetAndroidVersionAsync())
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.IsApkCompatibleWithWSAAsync(metadata);

            // Assert
            Assert.True(result); // При неизвестной версии WSA предполагаем совместимость
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task InstallProgressChanged_Event_FiresDuringInstallation()
        {
            // Arrange
            ApkInstallProgressEventArgs? capturedEvent = null;
            _service.InstallProgressChanged += (sender, args) => capturedEvent = args;

            var apkPath = Path.Combine(_tempDirectory, "app.apk");
            CreateMockApkFile(apkPath);

            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);

            var installResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(installResult);

            // Act
            await _service.InstallApkAsync(apkPath, null, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal(apkPath, capturedEvent.ApkPath);
            Assert.Equal(100, capturedEvent.Progress.ProgressPercentage);
        }

        #endregion

        #region Helper Methods

        private void CreateMockApkFile(string path, long size = 1024)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var content = new byte[size];
            // Заполняем случайными данными для имитации реального APK
            new Random().NextBytes(content);
            File.WriteAllBytes(path, content);
        }

        private void CreateMockXapkFile(string path, bool includeManifest = true, bool includeApks = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var fileStream = new FileStream(path, FileMode.Create);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            if (includeManifest)
            {
                var manifestEntry = archive.CreateEntry("manifest.json");
                using var manifestStream = manifestEntry.Open();
                var manifestContent = """
                {
                    "xapk_version": 2,
                    "package_name": "com.example.xapkapp",
                    "name": "XAPK Test App",
                    "version_code": 100,
                    "version_name": "2.0.0"
                }
                """;
                manifestStream.Write(Encoding.UTF8.GetBytes(manifestContent));
            }

            if (includeApks)
            {
                // Создаем mock APK файл в архиве
                var apkEntry = archive.CreateEntry("base.apk");
                using var apkStream = apkEntry.Open();
                var apkContent = new byte[1024];
                new Random().NextBytes(apkContent);
                apkStream.Write(apkContent);

                // Создаем split APK
                var splitEntry = archive.CreateEntry("config.arm64_v8a.apk");
                using var splitStream = splitEntry.Open();
                splitStream.Write(apkContent);
            }
        }

        #endregion
    }
}
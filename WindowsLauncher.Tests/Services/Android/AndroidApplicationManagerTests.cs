using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Services.Android;
using Xunit;

namespace WindowsLauncher.Tests.Services.Android
{
    public class AndroidApplicationManagerTests
    {
        private readonly Mock<ILogger<AndroidApplicationManager>> _mockLogger;
        private readonly Mock<IWSAIntegrationService> _mockWSAService;
        private readonly AndroidApplicationManager _manager;

        public AndroidApplicationManagerTests()
        {
            _mockLogger = new Mock<ILogger<AndroidApplicationManager>>();
            _mockWSAService = new Mock<IWSAIntegrationService>();
            _manager = new AndroidApplicationManager(_mockWSAService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ValidateApkAsync_WithValidApkPath_ReturnsTrue()
        {
            // Arrange
            var validApkPath = "C:\\test\\valid-app.apk";
            _mockWSAService.Setup(x => x.ValidateApkFileAsync(validApkPath))
                          .ReturnsAsync(true);

            // Act
            var result = await _manager.ValidateApkAsync(validApkPath);

            // Assert
            Assert.True(result);
            _mockWSAService.Verify(x => x.ValidateApkFileAsync(validApkPath), Times.Once);
        }

        [Fact]
        public async Task ValidateApkAsync_WithInvalidApkPath_ReturnsFalse()
        {
            // Arrange
            var invalidApkPath = "C:\\test\\invalid.txt";
            _mockWSAService.Setup(x => x.ValidateApkFileAsync(invalidApkPath))
                          .ReturnsAsync(false);

            // Act
            var result = await _manager.ValidateApkAsync(invalidApkPath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task ValidateApkAsync_WithInvalidInput_ReturnsFalse(string apkPath)
        {
            // Act
            var result = await _manager.ValidateApkAsync(apkPath);

            // Assert
            Assert.False(result);
            _mockWSAService.Verify(x => x.ValidateApkFileAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_WithValidApk_ReturnsMetadata()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var expectedMetadata = new ApkMetadata
            {
                PackageName = "com.example.app",
                VersionCode = 42,
                VersionName = "1.2.3",
                AppName = "Test App",
                MinSdkVersion = 21,
                TargetSdkVersion = 34
            };

            _mockWSAService.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                          .ReturnsAsync(expectedMetadata);

            // Act
            var result = await _manager.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMetadata.PackageName, result.PackageName);
            Assert.Equal(expectedMetadata.VersionCode, result.VersionCode);
            Assert.Equal(expectedMetadata.VersionName, result.VersionName);
            Assert.Equal(expectedMetadata.AppName, result.AppName);
            Assert.Equal(expectedMetadata.MinSdkVersion, result.MinSdkVersion);
            Assert.Equal(expectedMetadata.TargetSdkVersion, result.TargetSdkVersion);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_WithInvalidApk_ReturnsNull()
        {
            // Arrange
            var apkPath = "C:\\test\\invalid.apk";
            _mockWSAService.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                          .ReturnsAsync((ApkMetadata)null);

            // Act
            var result = await _manager.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task InstallApkAsync_WithValidApk_ReturnsSuccess()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var packageName = "com.example.app";
            _mockWSAService.Setup(x => x.InstallApkAsync(apkPath))
                          .ReturnsAsync(new ApkInstallResult { Success = true, PackageName = packageName });

            // Act
            var result = await _manager.InstallApkAsync(apkPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(packageName, result.PackageName);
        }

        [Fact]
        public async Task InstallApkAsync_WithInstallationError_ReturnsFailure()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var errorMessage = "Installation failed: insufficient storage";
            _mockWSAService.Setup(x => x.InstallApkAsync(apkPath))
                          .ReturnsAsync(new ApkInstallResult { Success = false, ErrorMessage = errorMessage });

            // Act
            var result = await _manager.InstallApkAsync(apkPath);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(errorMessage, result.ErrorMessage);
        }

        [Fact]
        public async Task LaunchAndroidAppAsync_WithValidPackage_ReturnsSuccess()
        {
            // Arrange
            var packageName = "com.example.app";
            _mockWSAService.Setup(x => x.LaunchAppAsync(packageName))
                          .ReturnsAsync(new AppLaunchResult { Success = true, ProcessId = 1234 });

            // Act
            var result = await _manager.LaunchAndroidAppAsync(packageName);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1234, result.ProcessId);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSAInstalled_ReturnsTrue()
        {
            // Arrange
            _mockWSAService.Setup(x => x.IsWSAAvailableAsync())
                          .ReturnsAsync(true);

            // Act
            var result = await _manager.IsWSAAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSANotInstalled_ReturnsFalse()
        {
            // Arrange
            _mockWSAService.Setup(x => x.IsWSAAvailableAsync())
                          .ReturnsAsync(false);

            // Act
            var result = await _manager.IsWSAAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetInstalledAndroidAppsAsync_ReturnsAppList()
        {
            // Arrange
            var expectedApps = new List<InstalledAndroidApp>
            {
                new InstalledAndroidApp { PackageName = "com.example.app1", AppName = "App 1" },
                new InstalledAndroidApp { PackageName = "com.example.app2", AppName = "App 2" }
            };

            _mockWSAService.Setup(x => x.GetInstalledAppsAsync())
                          .ReturnsAsync(expectedApps);

            // Act
            var result = await _manager.GetInstalledAndroidAppsAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Contains(result, app => app.PackageName == "com.example.app1");
            Assert.Contains(result, app => app.PackageName == "com.example.app2");
        }
    }

    // Временные модели для тестов (будут созданы в следующих задачах)
    public interface IWSAIntegrationService
    {
        Task<bool> IsWSAAvailableAsync();
        Task<bool> ValidateApkFileAsync(string apkPath);
        Task<ApkMetadata> ExtractApkMetadataAsync(string apkPath);
        Task<ApkInstallResult> InstallApkAsync(string apkPath);
        Task<AppLaunchResult> LaunchAppAsync(string packageName);
        Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync();
    }

    public class ApkMetadata
    {
        public string PackageName { get; set; }
        public int VersionCode { get; set; }
        public string VersionName { get; set; }
        public string AppName { get; set; }
        public int MinSdkVersion { get; set; }
        public int TargetSdkVersion { get; set; }
    }

    public class ApkInstallResult
    {
        public bool Success { get; set; }
        public string PackageName { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class AppLaunchResult
    {
        public bool Success { get; set; }
        public int ProcessId { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class InstalledAndroidApp
    {
        public string PackageName { get; set; }
        public string AppName { get; set; }
        public string VersionName { get; set; }
        public bool IsEnabled { get; set; }
    }

    // Временная реализация AndroidApplicationManager для тестов
    public class AndroidApplicationManager
    {
        private readonly IWSAIntegrationService _wsaService;
        private readonly ILogger<AndroidApplicationManager> _logger;

        public AndroidApplicationManager(IWSAIntegrationService wsaService, ILogger<AndroidApplicationManager> logger)
        {
            _wsaService = wsaService ?? throw new ArgumentNullException(nameof(wsaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ValidateApkAsync(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
                return false;

            return await _wsaService.ValidateApkFileAsync(apkPath);
        }

        public async Task<ApkMetadata> ExtractApkMetadataAsync(string apkPath)
        {
            return await _wsaService.ExtractApkMetadataAsync(apkPath);
        }

        public async Task<ApkInstallResult> InstallApkAsync(string apkPath)
        {
            return await _wsaService.InstallApkAsync(apkPath);
        }

        public async Task<AppLaunchResult> LaunchAndroidAppAsync(string packageName)
        {
            return await _wsaService.LaunchAppAsync(packageName);
        }

        public async Task<bool> IsWSAAvailableAsync()
        {
            return await _wsaService.IsWSAAvailableAsync();
        }

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAndroidAppsAsync()
        {
            return await _wsaService.GetInstalledAppsAsync();
        }
    }
}
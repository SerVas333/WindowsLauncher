using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Android;
using WindowsLauncher.Services.Android;
using Xunit;

namespace WindowsLauncher.Tests.Services.Android
{
    /// <summary>
    /// Integration тесты для композитного WSAIntegrationService - проверяют взаимодействие между сервисами
    /// </summary>
    public class WSAIntegrationServiceIntegrationTests : IDisposable
    {
        private readonly Mock<IWSAConnectionService> _mockConnectionService;
        private readonly Mock<IApkManagementService> _mockApkService;
        private readonly Mock<IInstalledAppsService> _mockAppsService;
        private readonly Mock<ILogger<WSAIntegrationService>> _mockLogger;
        private readonly WSAIntegrationService _service;

        public WSAIntegrationServiceIntegrationTests()
        {
            _mockConnectionService = new Mock<IWSAConnectionService>();
            _mockApkService = new Mock<IApkManagementService>();
            _mockAppsService = new Mock<IInstalledAppsService>();
            _mockLogger = new Mock<ILogger<WSAIntegrationService>>();

            _service = new WSAIntegrationService(
                _mockConnectionService.Object,
                _mockApkService.Object,
                _mockAppsService.Object,
                _mockLogger.Object
            );
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        #region WSA Connection Management Integration Tests

        [Fact]
        public async Task IsWSAAvailableAsync_DelegatesToConnectionService()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.IsWSAAvailableAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.True(result);
            _mockConnectionService.Verify(x => x.IsWSAAvailableAsync(), Times.Once);
        }

        [Fact]
        public async Task StartWSAAsync_DelegatesToConnectionService()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.StartWSAAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _service.StartWSAAsync();

            // Assert
            Assert.True(result);
            _mockConnectionService.Verify(x => x.StartWSAAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAndroidVersionAsync_DelegatesToConnectionService()
        {
            // Arrange
            var expectedVersion = "13";
            _mockConnectionService.Setup(x => x.GetAndroidVersionAsync())
                .ReturnsAsync(expectedVersion);

            // Act
            var result = await _service.GetAndroidVersionAsync();

            // Assert
            Assert.Equal(expectedVersion, result);
            _mockConnectionService.Verify(x => x.GetAndroidVersionAsync(), Times.Once);
        }

        #endregion

        #region APK Management Integration Tests

        [Fact]
        public async Task ValidateApkFileAsync_DelegatesToApkService()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            _mockApkService.Setup(x => x.ValidateApkFileAsync(apkPath))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.True(result);
            _mockApkService.Verify(x => x.ValidateApkFileAsync(apkPath), Times.Once);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_DelegatesToApkService()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var expectedMetadata = new ApkMetadata
            {
                PackageName = "com.example.app",
                AppName = "Test App",
                VersionName = "1.0",
                VersionCode = 1
            };

            _mockApkService.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                .ReturnsAsync(expectedMetadata);

            // Act
            var result = await _service.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMetadata.PackageName, result.PackageName);
            Assert.Equal(expectedMetadata.AppName, result.AppName);
            _mockApkService.Verify(x => x.ExtractApkMetadataAsync(apkPath), Times.Once);
        }

        [Fact]
        public async Task InstallApkAsync_DelegatesToApkServiceWithoutProgress()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var expectedResult = new ApkInstallResult
            {
                Success = true,
                PackageName = "com.example.app",
                InstallationMethod = InstallationMethod.Standard
            };

            _mockApkService.Setup(x => x.InstallApkAsync(apkPath, null, CancellationToken.None))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.InstallApkAsync(apkPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedResult.PackageName, result.PackageName);
            _mockApkService.Verify(x => x.InstallApkAsync(apkPath, null, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task InstallApkWithProgressAsync_DelegatesToApkServiceWithProgress()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var progress = new Progress<ApkInstallProgress>();
            var cancellationToken = new CancellationToken();

            var expectedResult = new ApkInstallResult
            {
                Success = true,
                PackageName = "com.example.app"
            };

            _mockApkService.Setup(x => x.InstallApkAsync(apkPath, progress, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.InstallApkWithProgressAsync(apkPath, progress, cancellationToken);

            // Assert
            Assert.True(result.Success);
            _mockApkService.Verify(x => x.InstallApkAsync(apkPath, progress, cancellationToken), Times.Once);
        }

        [Fact]
        public async Task IsApkCompatibleAsync_CombinesApkAndConnectionServices()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var metadata = new ApkMetadata
            {
                PackageName = "com.example.app",
                MinSdkVersion = 21,
                TargetSdkVersion = 33
            };

            _mockApkService.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                .ReturnsAsync(metadata);

            _mockApkService.Setup(x => x.IsApkCompatibleWithWSAAsync(metadata))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsApkCompatibleAsync(apkPath);

            // Assert
            Assert.True(result);
            _mockApkService.Verify(x => x.ExtractApkMetadataAsync(apkPath), Times.Once);
            _mockApkService.Verify(x => x.IsApkCompatibleWithWSAAsync(metadata), Times.Once);
        }

        [Fact]
        public async Task IsApkCompatibleAsync_WhenMetadataExtractionFails_ReturnsFalse()
        {
            // Arrange
            var apkPath = "C:\\test\\invalid.apk";
            
            _mockApkService.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                .ReturnsAsync((ApkMetadata?)null);

            // Act
            var result = await _service.IsApkCompatibleAsync(apkPath);

            // Assert
            Assert.False(result);
            _mockApkService.Verify(x => x.ExtractApkMetadataAsync(apkPath), Times.Once);
            _mockApkService.Verify(x => x.IsApkCompatibleWithWSAAsync(It.IsAny<ApkMetadata>()), Times.Never);
        }

        [Fact]
        public async Task GetApkFileInfoAsync_DelegatesToApkService()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var expectedInfo = new ApkFileInfo
            {
                FilePath = apkPath,
                FileType = "APK",
                PackageName = "com.example.app",
                AppName = "Test App",
                FileSizeBytes = 1024 * 1024
            };

            _mockApkService.Setup(x => x.GetApkFileInfoAsync(apkPath))
                .ReturnsAsync(expectedInfo);

            // Act
            var result = await _service.GetApkFileInfoAsync(apkPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedInfo.PackageName, result.PackageName);
            Assert.Equal(expectedInfo.FileType, result.FileType);
            _mockApkService.Verify(x => x.GetApkFileInfoAsync(apkPath), Times.Once);
        }

        #endregion

        #region Installed Apps Management Integration Tests

        [Fact]
        public async Task GetInstalledAppsAsync_DelegatesToAppsService()
        {
            // Arrange
            var expectedApps = new List<InstalledAndroidApp>
            {
                new() { PackageName = "com.example.app1", AppName = "App 1", IsSystemApp = false },
                new() { PackageName = "com.example.app2", AppName = "App 2", IsSystemApp = false }
            };

            _mockAppsService.Setup(x => x.GetInstalledAppsAsync(false, true))
                .ReturnsAsync(expectedApps);

            // Act
            var result = await _service.GetInstalledAppsAsync(includeSystemApps: false);

            // Assert
            var apps = result.ToList();
            Assert.Equal(2, apps.Count);
            Assert.All(apps, app => Assert.False(app.IsSystemApp));
            _mockAppsService.Verify(x => x.GetInstalledAppsAsync(false, true), Times.Once);
        }

        [Fact]
        public async Task LaunchAppAsync_DelegatesToAppsService()
        {
            // Arrange
            var packageName = "com.example.app";
            var expectedResult = new AppLaunchResult
            {
                Success = true,
                PackageName = packageName,
                ProcessId = 12345
            };

            _mockAppsService.Setup(x => x.LaunchAppAsync(packageName))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedResult.ProcessId, result.ProcessId);
            _mockAppsService.Verify(x => x.LaunchAppAsync(packageName), Times.Once);
        }

        [Fact]
        public async Task UninstallAppAsync_DelegatesToAppsService()
        {
            // Arrange
            var packageName = "com.example.uninstall";
            _mockAppsService.Setup(x => x.UninstallAppAsync(packageName))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UninstallAppAsync(packageName);

            // Assert
            Assert.True(result);
            _mockAppsService.Verify(x => x.UninstallAppAsync(packageName), Times.Once);
        }

        [Fact]
        public async Task IsAppInstalledAsync_DelegatesToAppsService()
        {
            // Arrange
            var packageName = "com.example.checkapp";
            _mockAppsService.Setup(x => x.IsAppInstalledAsync(packageName))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsAppInstalledAsync(packageName);

            // Assert
            Assert.True(result);
            _mockAppsService.Verify(x => x.IsAppInstalledAsync(packageName), Times.Once);
        }

        [Fact]
        public async Task RefreshInstalledAppsAsync_DelegatesToAppsService()
        {
            // Arrange
            _mockAppsService.Setup(x => x.RefreshAppsCache())
                .ReturnsAsync(true);

            // Act
            var result = await _service.RefreshInstalledAppsAsync();

            // Assert
            Assert.True(result);
            _mockAppsService.Verify(x => x.RefreshAppsCache(), Times.Once);
        }

        #endregion

        #region Status and Diagnostics Integration Tests

        [Fact]
        public async Task GetWSAStatusAsync_CombinesMultipleServices()
        {
            // Arrange
            var connectionStatus = new Dictionary<string, object>
            {
                ["WSAAvailable"] = true,
                ["WSARunning"] = true,
                ["ADBAvailable"] = true
            };

            var appsStats = new Dictionary<string, object>
            {
                ["TotalUserApps"] = 5,
                ["SystemApps"] = 10,
                ["RunningApps"] = 2
            };

            _mockConnectionService.Setup(x => x.GetConnectionStatusAsync())
                .ReturnsAsync(connectionStatus);

            _mockAppsService.Setup(x => x.GetAppsUsageStatsAsync())
                .ReturnsAsync(appsStats);

            // Act
            var result = await _service.GetWSAStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("WSAAvailable"));
            Assert.True(result.ContainsKey("Apps_TotalUserApps"));
            Assert.Equal("V2 - Specialized Services", result["ServiceArchitecture"]);
            Assert.True(result.ContainsKey("LastStatusUpdate"));

            _mockConnectionService.Verify(x => x.GetConnectionStatusAsync(), Times.Once);
            _mockAppsService.Verify(x => x.GetAppsUsageStatsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetDetailedUsageStatsAsync_AggregatesFromAllServices()
        {
            // Arrange
            var connectionStatus = new Dictionary<string, object>
            {
                ["TotalConnections"] = 15,
                ["LastConnection"] = DateTime.Now
            };

            var appsStats = new Dictionary<string, object>
            {
                ["TotalInstalls"] = 25,
                ["RecentLaunches"] = 8
            };

            _mockConnectionService.Setup(x => x.GetConnectionStatusAsync())
                .ReturnsAsync(connectionStatus);

            _mockAppsService.Setup(x => x.GetAppsUsageStatsAsync())
                .ReturnsAsync(appsStats);

            // Act
            var result = await _service.GetDetailedUsageStatsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("ConnectionStats"));
            Assert.True(result.ContainsKey("AppsStats"));
            Assert.Equal("Microservices-based V2", result["Architecture"]);

            _mockConnectionService.Verify(x => x.GetConnectionStatusAsync(), Times.Once);
            _mockAppsService.Verify(x => x.GetAppsUsageStatsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetWSAStatusAsync_HandlesExceptions_ReturnsErrorStatus()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.GetConnectionStatusAsync())
                .ThrowsAsync(new Exception("Connection service failed"));

            _mockAppsService.Setup(x => x.GetAppsUsageStatsAsync())
                .ReturnsAsync(new Dictionary<string, object>());

            // Act
            var result = await _service.GetWSAStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("Error"));
            Assert.Contains("Connection service failed", result["Error"].ToString());
        }

        #endregion

        #region Event Subscription Integration Tests

        [Fact]
        public void SubscribeToConnectionEvents_DelegatesToConnectionService()
        {
            // Arrange
            var handler = new EventHandler<WSAConnectionStatusEventArgs>((sender, args) => { });

            // Act
            _service.SubscribeToConnectionEvents(handler);

            // Assert
            _mockConnectionService.VerifyAdd(x => x.ConnectionStatusChanged += handler, Times.Once);
        }

        [Fact]
        public void UnsubscribeFromConnectionEvents_DelegatesToConnectionService()
        {
            // Arrange
            var handler = new EventHandler<WSAConnectionStatusEventArgs>((sender, args) => { });

            // Act
            _service.UnsubscribeFromConnectionEvents(handler);

            // Assert
            _mockConnectionService.VerifyRemove(x => x.ConnectionStatusChanged -= handler, Times.Once);
        }

        [Fact]
        public void SubscribeToAppsEvents_DelegatesToAppsService()
        {
            // Arrange
            var handler = new EventHandler<InstalledAppsChangedEventArgs>((sender, args) => { });

            // Act
            _service.SubscribeToAppsEvents(handler);

            // Assert
            _mockAppsService.VerifyAdd(x => x.InstalledAppsChanged += handler, Times.Once);
        }

        [Fact]
        public void UnsubscribeFromAppsEvents_DelegatesToAppsService()
        {
            // Arrange
            var handler = new EventHandler<InstalledAppsChangedEventArgs>((sender, args) => { });

            // Act
            _service.UnsubscribeFromAppsEvents(handler);

            // Assert
            _mockAppsService.VerifyRemove(x => x.InstalledAppsChanged -= handler, Times.Once);
        }

        [Fact]
        public void SubscribeToInstallProgressEvents_DelegatesToApkService()
        {
            // Arrange
            var handler = new EventHandler<ApkInstallProgressEventArgs>((sender, args) => { });

            // Act
            _service.SubscribeToInstallProgressEvents(handler);

            // Assert
            _mockApkService.VerifyAdd(x => x.InstallProgressChanged += handler, Times.Once);
        }

        [Fact]
        public void UnsubscribeFromInstallProgressEvents_DelegatesToApkService()
        {
            // Arrange
            var handler = new EventHandler<ApkInstallProgressEventArgs>((sender, args) => { });

            // Act
            _service.UnsubscribeFromInstallProgressEvents(handler);

            // Assert
            _mockApkService.VerifyRemove(x => x.InstallProgressChanged -= handler, Times.Once);
        }

        #endregion

        #region End-to-End Integration Scenarios

        [Fact]
        public async Task FullApkInstallationWorkflow_IntegratesAllServices()
        {
            // Arrange
            var apkPath = "C:\\test\\integration-app.apk";
            var packageName = "com.example.integration";

            // Setup connection service
            _mockConnectionService.Setup(x => x.IsWSAAvailableAsync()).ReturnsAsync(true);
            _mockConnectionService.Setup(x => x.ConnectToWSAAsync()).ReturnsAsync(true);

            // Setup APK service
            var metadata = new ApkMetadata { PackageName = packageName };
            _mockApkService.Setup(x => x.ExtractApkMetadataAsync(apkPath)).ReturnsAsync(metadata);
            _mockApkService.Setup(x => x.IsApkCompatibleWithWSAAsync(metadata)).ReturnsAsync(true);
            _mockApkService.Setup(x => x.InstallApkAsync(apkPath, null, CancellationToken.None))
                .ReturnsAsync(new ApkInstallResult { Success = true, PackageName = packageName });

            // Setup apps service
            _mockAppsService.Setup(x => x.IsAppInstalledAsync(packageName)).ReturnsAsync(true);
            _mockAppsService.Setup(x => x.LaunchAppAsync(packageName))
                .ReturnsAsync(new AppLaunchResult { Success = true, PackageName = packageName });

            // Act - полный цикл: проверка совместимости → установка → проверка установки → запуск
            var isCompatible = await _service.IsApkCompatibleAsync(apkPath);
            var installResult = await _service.InstallApkAsync(apkPath);
            var isInstalled = await _service.IsAppInstalledAsync(packageName);
            var launchResult = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.True(isCompatible);
            Assert.True(installResult.Success);
            Assert.True(isInstalled);
            Assert.True(launchResult.Success);

            // Verify all services were called in correct order
            _mockConnectionService.Verify(x => x.IsWSAAvailableAsync(), Times.AtLeastOnce);
            _mockApkService.Verify(x => x.ExtractApkMetadataAsync(apkPath), Times.Once);
            _mockApkService.Verify(x => x.IsApkCompatibleWithWSAAsync(metadata), Times.Once);
            _mockApkService.Verify(x => x.InstallApkAsync(apkPath, null, CancellationToken.None), Times.Once);
            _mockAppsService.Verify(x => x.IsAppInstalledAsync(packageName), Times.Once);
            _mockAppsService.Verify(x => x.LaunchAppAsync(packageName), Times.Once);
        }

        [Fact]
        public async Task ErrorPropagation_FromSpecializedServices_HandledCorrectly()
        {
            // Arrange
            var apkPath = "C:\\test\\problematic-app.apk";

            // Setup connection service to fail
            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(false);

            // Setup APK service to return failure
            _mockApkService.Setup(x => x.InstallApkAsync(apkPath, null, CancellationToken.None))
                .ReturnsAsync(new ApkInstallResult 
                { 
                    Success = false, 
                    ErrorMessage = "WSA connection failed" 
                });

            // Act
            var result = await _service.InstallApkAsync(apkPath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("WSA connection failed", result.ErrorMessage);
        }

        [Fact]
        public async Task ConcurrentOperations_AreHandledCorrectly()
        {
            // Arrange
            var apkPath1 = "C:\\test\\app1.apk";
            var apkPath2 = "C:\\test\\app2.apk";

            _mockApkService.Setup(x => x.ValidateApkFileAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockAppsService.Setup(x => x.IsAppInstalledAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act - запускаем операции параллельно
            var task1 = _service.ValidateApkFileAsync(apkPath1);
            var task2 = _service.ValidateApkFileAsync(apkPath2);
            var task3 = _service.IsAppInstalledAsync("com.example.app1");
            var task4 = _service.IsAppInstalledAsync("com.example.app2");

            var results = await Task.WhenAll(task1, task2, task3, task4);

            // Assert
            Assert.All(results, result => Assert.IsType<bool>(result));
            Assert.True((bool)results[0]); // ValidateApkFileAsync results
            Assert.True((bool)results[1]);
            Assert.False((bool)results[2]); // IsAppInstalledAsync results
            Assert.False((bool)results[3]);

            // Verify all calls were made
            _mockApkService.Verify(x => x.ValidateApkFileAsync(It.IsAny<string>()), Times.Exactly(2));
            _mockAppsService.Verify(x => x.IsAppInstalledAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        #endregion
    }
}
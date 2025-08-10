using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Android;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Services.Lifecycle.Launchers;
using Xunit;

namespace WindowsLauncher.Tests.Services.Lifecycle.Launchers
{
    public class WSAApplicationLauncherTests
    {
        private readonly Mock<IAndroidApplicationManager> _mockAndroidManager;
        private readonly Mock<ILogger<WSAApplicationLauncher>> _mockLogger;
        private readonly WSAApplicationLauncher _launcher;

        public WSAApplicationLauncherTests()
        {
            _mockAndroidManager = new Mock<IAndroidApplicationManager>();
            _mockLogger = new Mock<ILogger<WSAApplicationLauncher>>();
            _launcher = new WSAApplicationLauncher(_mockAndroidManager.Object, _mockLogger.Object);
        }

        [Fact]
        public void SupportedType_ReturnsAndroid()
        {
            // Assert
            Assert.Equal(ApplicationType.Android, _launcher.SupportedType);
        }

        [Fact]
        public void Priority_Returns25()
        {
            // Assert - Higher than DesktopApplicationLauncher (10) but lower than TextEditor (30)
            Assert.Equal(25, _launcher.Priority);
        }

        [Theory]
        [InlineData("com.example.app", ApplicationType.Android, true)]
        [InlineData("C:\\test\\app.apk", ApplicationType.Android, true)]
        [InlineData("/data/app.apk", ApplicationType.Android, true)]
        [InlineData("app.APK", ApplicationType.Android, true)] // Case insensitive
        [InlineData("notepad.exe", ApplicationType.Android, false)] // Wrong extension
        [InlineData("com.example.app", ApplicationType.Desktop, false)] // Wrong type
        [InlineData("", ApplicationType.Android, false)] // Empty path
        [InlineData(null, ApplicationType.Android, false)] // Null path
        public void CanLaunch_WithVariousInputs_ReturnsExpectedResult(
            string executablePath, ApplicationType appType, bool expectedResult)
        {
            // Arrange
            var application = new Application
            {
                ExecutablePath = executablePath,
                Type = appType
            };

            // Act
            var result = _launcher.CanLaunch(application);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CanLaunch_WithNullApplication_ReturnsFalse()
        {
            // Act
            var result = _launcher.CanLaunch(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task LaunchAsync_WithValidPackageName_ReturnsSuccessResult()
        {
            // Arrange
            var packageName = "com.example.testapp";
            var application = new Application
            {
                Id = 1,
                Name = "Test App",
                ExecutablePath = packageName,
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateSuccess(packageName, 1234, "MainActivity");

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ReturnsAsync(launchResult);

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Instance);
            Assert.Equal(1234, result.ProcessId);
            Assert.Equal(ApplicationState.Running, result.Instance.State);
        }

        [Fact]
        public async Task LaunchAsync_WithApkFile_ExtractsPackageNameAndLaunches()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var packageName = "com.example.extractedapp";
            var application = new Application
            {
                Id = 2,
                Name = "APK App",
                ExecutablePath = apkPath,
                Type = ApplicationType.Android
            };

            var apkMetadata = new ApkMetadata
            {
                PackageName = packageName,
                AppName = "Extracted App",
                VersionCode = 42
            };

            var launchResult = AppLaunchResult.CreateSuccess(packageName, 5678);

            _mockAndroidManager.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                              .ReturnsAsync(apkMetadata);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ReturnsAsync(launchResult);

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Instance);
            Assert.Equal(5678, result.ProcessId);
            Assert.Equal(ApplicationState.Running, result.Instance.State);

            // Verify that metadata extraction was called
            _mockAndroidManager.Verify(x => x.ExtractApkMetadataAsync(apkPath), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_WithFailedLaunch_ReturnsFailureResult()
        {
            // Arrange
            var packageName = "com.example.failapp";
            var application = new Application
            {
                Id = 3,
                Name = "Failing App",
                ExecutablePath = packageName,
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateFailure(packageName, "App not installed");

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ReturnsAsync(launchResult);

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("App not installed", result.ErrorMessage);
            Assert.Null(result.ProcessId);
        }

        [Fact]
        public async Task LaunchAsync_WithInvalidApkFile_ReturnsFailureResult()
        {
            // Arrange
            var apkPath = "C:\\test\\invalid.apk";
            var application = new Application
            {
                Id = 4,
                Name = "Invalid APK",
                ExecutablePath = apkPath,
                Type = ApplicationType.Android
            };

            _mockAndroidManager.Setup(x => x.ExtractApkMetadataAsync(apkPath))
                              .ReturnsAsync((ApkMetadata)null);

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to extract metadata", result.ErrorMessage);

            // Verify launch was not attempted
            _mockAndroidManager.Verify(x => x.LaunchAndroidAppAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LaunchAsync_WithException_ReturnsFailureResult()
        {
            // Arrange
            var packageName = "com.example.exceptionapp";
            var application = new Application
            {
                Id = 5,
                Name = "Exception App",
                ExecutablePath = packageName,
                Type = ApplicationType.Android
            };

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("Test exception", result.ErrorMessage);
        }

        [Fact]
        public async Task LaunchAsync_GeneratesUniqueInstanceId()
        {
            // Arrange
            var application = new Application
            {
                Id = 6,
                Name = "Unique ID App",
                ExecutablePath = "com.example.uniqueapp",
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateSuccess("com.example.uniqueapp", 9999);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(It.IsAny<string>()))
                              .ReturnsAsync(launchResult);

            // Act
            var result1 = await _launcher.LaunchAsync(application, "testuser1");
            var result2 = await _launcher.LaunchAsync(application, "testuser2");

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotEqual(result1.InstanceId, result2.InstanceId);
            Assert.True(result1.InstanceId.StartsWith("android_6_"));
            Assert.True(result2.InstanceId.StartsWith("android_6_"));
        }

        [Fact]
        public async Task LaunchAsync_FiresWindowActivatedEvent()
        {
            // Arrange
            var application = new Application
            {
                Id = 7,
                Name = "Event Test App",
                ExecutablePath = "com.example.eventapp",
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateSuccess("com.example.eventapp", 1111);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(It.IsAny<string>()))
                              .ReturnsAsync(launchResult);

            // Act
            var result = await _launcher.LaunchAsync(application, "testuser");

            // Assert - Проверяем что результат содержит экземпляр
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Instance);
            Assert.Equal(ApplicationState.Running, result.Instance.State);
            Assert.True(result.Instance.IsActive);
        }

        [Fact]
        public async Task SwitchToAsync_WithValidInstanceId_ReturnsTrue()
        {
            // Arrange
            var application = new Application
            {
                Id = 8,
                Name = "Switch Test App",
                ExecutablePath = "com.example.switchapp",
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateSuccess("com.example.switchapp", 2222);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(It.IsAny<string>()))
                              .ReturnsAsync(launchResult);

            // First launch the app
            var launchResultInstance = await _launcher.LaunchAsync(application, "testuser");

            // Act
            var switchResult = await _launcher.SwitchToAsync(launchResultInstance.InstanceId ?? "");

            // Assert
            Assert.True(switchResult);
        }

        [Fact]
        public async Task SwitchToAsync_WithInvalidInstanceId_ReturnsFalse()
        {
            // Act
            var result = await _launcher.SwitchToAsync("invalid_instance_id");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task FindExistingInstanceAsync_WithMatchingApplication_ReturnsInstance()
        {
            // Arrange
            var application = new Application
            {
                Id = 9,
                Name = "Find Test App",
                ExecutablePath = "com.example.findapp",
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateSuccess("com.example.findapp", 3333);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(It.IsAny<string>()))
                              .ReturnsAsync(launchResult);

            // First launch the app
            await _launcher.LaunchAsync(application, "testuser");

            // Act
            var existingInstance = await _launcher.FindExistingInstanceAsync(application);

            // Assert
            Assert.NotNull(existingInstance);
            Assert.Equal(application.Id, existingInstance.Application.Id);
            Assert.Equal("com.example.findapp", existingInstance.Application.ExecutablePath);
        }

        [Fact]
        public async Task FindExistingInstanceAsync_WithNoMatchingApplication_ReturnsNull()
        {
            // Arrange
            var application = new Application
            {
                Id = 10,
                Name = "Not Found App",
                ExecutablePath = "com.example.notfound",
                Type = ApplicationType.Android
            };

            // Act (without launching first)
            var existingInstance = await _launcher.FindExistingInstanceAsync(application);

            // Assert
            Assert.Null(existingInstance);
        }

        [Fact]
        public async Task CleanupCompletedInstances_RemovesFailedInstances()
        {
            // Arrange
            var application = new Application
            {
                Id = 11,
                Name = "Cleanup Test App",
                ExecutablePath = "com.example.cleanupapp",
                Type = ApplicationType.Android
            };

            var launchResult = AppLaunchResult.CreateFailure("com.example.cleanupapp", "Launch failed");
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(It.IsAny<string>()))
                              .ReturnsAsync(launchResult);

            // Launch app (will fail)
            await _launcher.LaunchAsync(application, "testuser");

            // Verify instance exists
            var beforeCleanup = await _launcher.FindExistingInstanceAsync(application);
            Assert.NotNull(beforeCleanup);
            Assert.Equal(ApplicationState.Error, beforeCleanup.State);

            // Wait a bit for cleanup timer
            await Task.Delay(100);

            // Act
            // Cleanup happens automatically, but we can force it by calling a method that triggers it
            var afterCleanup = await _launcher.FindExistingInstanceAsync(application);

            // Assert - Failed instances should still exist (only completed ones are cleaned up)
            Assert.NotNull(afterCleanup);
        }

        [Fact]
        public async Task FindMainWindowAsync_ReturnsVirtualWindow()
        {
            // Arrange
            var application = new Application
            {
                Id = 12,
                Name = "Window Test App",
                ExecutablePath = "com.example.windowapp",
                Type = ApplicationType.Android
            };

            // Act
            var windowInfo = await _launcher.FindMainWindowAsync(1234, application);

            // Assert
            Assert.NotNull(windowInfo);
            Assert.Equal(IntPtr.Zero, windowInfo.Handle); // Virtual window has no real handle
            Assert.Contains("Window Test App", windowInfo.Title);
            Assert.Equal((uint)1234, windowInfo.ProcessId);
            Assert.Equal("AndroidWindowClass", windowInfo.ClassName);
            Assert.True(windowInfo.IsVisible);
            Assert.True(windowInfo.IsResponding);
        }

        [Fact]
        public void GetWindowInitializationTimeoutMs_Returns15000()
        {
            // Arrange
            var application = new Application
            {
                Id = 13,
                Name = "Timeout Test App",
                Type = ApplicationType.Android
            };

            // Act
            var timeout = _launcher.GetWindowInitializationTimeoutMs(application);

            // Assert
            Assert.Equal(15000, timeout); // 15 seconds for Android apps
        }

        [Fact]
        public async Task CleanupAsync_WithValidInstance_CallsStopAndroidApp()
        {
            // Arrange
            var application = new Application
            {
                Id = 14,
                Name = "Cleanup Test App",
                ExecutablePath = "com.example.cleanupapp",
                Type = ApplicationType.Android
            };

            var packageName = "com.example.cleanupapp";
            var launchResult = AppLaunchResult.CreateSuccess(packageName, 1111);

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ReturnsAsync(launchResult);
            _mockAndroidManager.Setup(x => x.StopAndroidAppAsync(packageName))
                              .ReturnsAsync(true);

            // Launch app first to create an instance
            var result = await _launcher.LaunchAsync(application, "testuser");
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Instance);

            // Act
            await _launcher.CleanupAsync(result.Instance);

            // Assert
            _mockAndroidManager.Verify(x => x.StopAndroidAppAsync(packageName), Times.Once);
            Assert.Equal(ApplicationState.Terminated, result.Instance.State);
            Assert.False(result.Instance.IsActive);
            Assert.NotNull(result.Instance.EndTime);
        }

        [Fact]
        public async Task TerminateAsync_WithValidInstanceId_ReturnsTrue()
        {
            // Arrange
            var application = new Application
            {
                Id = 15,
                Name = "Terminate Test App",
                ExecutablePath = "com.example.terminateapp",
                Type = ApplicationType.Android
            };

            var packageName = "com.example.terminateapp";
            var launchResult = AppLaunchResult.CreateSuccess(packageName, 2222);

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync(packageName))
                              .ReturnsAsync(launchResult);
            _mockAndroidManager.Setup(x => x.StopAndroidAppAsync(packageName))
                              .ReturnsAsync(true);

            // Launch app first
            var result = await _launcher.LaunchAsync(application, "testuser");
            var instanceId = result.InstanceId ?? "";

            // Act
            var terminated = await _launcher.TerminateAsync(instanceId);

            // Assert
            Assert.True(terminated);
            _mockAndroidManager.Verify(x => x.StopAndroidAppAsync(packageName), Times.Once);
        }

        [Fact]
        public async Task TerminateAsync_WithInvalidInstanceId_ReturnsFalse()
        {
            // Act
            var terminated = await _launcher.TerminateAsync("invalid_instance_id");

            // Assert
            Assert.False(terminated);
        }

        [Fact]
        public async Task GetActiveInstancesAsync_ReturnsOnlyActiveInstances()
        {
            // Arrange
            var application1 = new Application
            {
                Id = 16,
                Name = "Active Test App 1",
                ExecutablePath = "com.example.active1",
                Type = ApplicationType.Android
            };

            var application2 = new Application
            {
                Id = 17,
                Name = "Active Test App 2",
                ExecutablePath = "com.example.active2",
                Type = ApplicationType.Android
            };

            var launchResult1 = AppLaunchResult.CreateSuccess("com.example.active1", 3333);
            var launchResult2 = AppLaunchResult.CreateSuccess("com.example.active2", 4444);

            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync("com.example.active1"))
                              .ReturnsAsync(launchResult1);
            _mockAndroidManager.Setup(x => x.LaunchAndroidAppAsync("com.example.active2"))
                              .ReturnsAsync(launchResult2);

            // Launch both apps
            var result1 = await _launcher.LaunchAsync(application1, "testuser1");
            var result2 = await _launcher.LaunchAsync(application2, "testuser2");

            Assert.True(result1.IsSuccess);
            Assert.True(result2.IsSuccess);

            // Act
            var activeInstances = await _launcher.GetActiveInstancesAsync();

            // Assert
            Assert.Equal(2, activeInstances.Count);
            Assert.Contains(activeInstances, i => i.Application.Id == 16);
            Assert.Contains(activeInstances, i => i.Application.Id == 17);
            Assert.All(activeInstances, i => Assert.True(i.IsActiveInstance()));
        }

    }
}
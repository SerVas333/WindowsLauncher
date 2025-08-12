using System.IO;
using WindowsLauncher.Tests.Services.Android.Fixtures;
using Xunit;

namespace WindowsLauncher.Tests.Services.Android
{
    /// <summary>
    /// Простые smoke tests для проверки Windows/WPF совместимости Android тестов
    /// </summary>
    [Collection("AndroidServices")]
    public class AndroidSmokeTests : AndroidServiceTestsBase
    {
        private readonly AndroidServicesFixture _fixture;

        public AndroidSmokeTests(AndroidServicesFixture fixture)
        {
            _fixture = fixture;
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void WindowsEnvironment_IsDetected()
        {
            // Arrange & Act
            var isWindows = AndroidTestUtilities.IsRunningOnWindows();

            // Assert
            Assert.True(isWindows, "Tests must run on Windows for WPF compatibility");
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void TempDirectory_IsCreated()
        {
            // Assert
            Assert.True(Directory.Exists(TempDirectory));
            Assert.Contains("WindowsLauncherTests", TempDirectory);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void AndroidTestUtilities_CanCreateMockFiles()
        {
            // Act
            var apkPath = CreateTestApkFile("smoke-test.apk");
            var xapkPath = CreateTestXapkFile("smoke-test.xapk");

            // Assert
            Assert.True(File.Exists(apkPath));
            Assert.True(File.Exists(xapkPath));
            
            var apkInfo = new FileInfo(apkPath);
            var xapkInfo = new FileInfo(xapkPath);
            
            Assert.True(apkInfo.Length > 0);
            Assert.True(xapkInfo.Length > 0);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void AndroidTestUtilities_CanCreateTestMetadata()
        {
            // Act
            var metadata = AndroidTestUtilities.CreateTestApkMetadata(
                "com.example.smoketest", 
                "Smoke Test App");

            // Assert
            Assert.Equal("com.example.smoketest", metadata.PackageName);
            Assert.Equal("Smoke Test App", metadata.AppName);
            Assert.Equal("1.0.0", metadata.VersionName);
            Assert.Equal(1, metadata.VersionCode);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void AndroidTestUtilities_CanCreateProcessResults()
        {
            // Act
            var successResult = AndroidTestUtilities.CreateSuccessfulProcessResult("Test output");
            var failResult = AndroidTestUtilities.CreateFailedProcessResult("Test error", 1);

            // Assert
            Assert.True(successResult.IsSuccess);
            Assert.Equal(0, successResult.ExitCode);
            Assert.Equal("Test output", successResult.StandardOutput);
            
            Assert.False(failResult.IsSuccess);
            Assert.Equal(1, failResult.ExitCode);
            Assert.Equal("Test error", failResult.StandardError);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void AndroidTestUtilities_CanCreateInstallResults()
        {
            // Act
            var successResult = AndroidTestUtilities.CreateSuccessfulInstallResult(
                "com.example.success");
            
            var failResult = AndroidTestUtilities.CreateFailedInstallResult(
                "Installation failed");

            // Assert
            Assert.True(successResult.Success);
            Assert.Equal("com.example.success", successResult.PackageName);
            
            Assert.False(failResult.Success);
            Assert.Equal("Installation failed", failResult.ErrorMessage);
        }
    }
}
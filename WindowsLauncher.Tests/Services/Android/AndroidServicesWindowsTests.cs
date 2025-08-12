using System.IO;
using WindowsLauncher.Tests.Services.Android.Fixtures;
using Xunit;

namespace WindowsLauncher.Tests.Services.Android
{
    /// <summary>
    /// Демонстрационные тесты показывающие использование Android тестовых утилит на Windows
    /// </summary>
    [Collection("AndroidServices")]
    public class AndroidServicesWindowsTests : AndroidServiceTestsBase
    {
        private readonly AndroidServicesFixture _fixture;

        public AndroidServicesWindowsTests(AndroidServicesFixture fixture)
        {
            _fixture = fixture;
            _fixture.SetupSharedTestFiles();
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void WindowsEnvironment_ShouldBeDetected()
        {
            // Arrange & Act
            var isWindows = AndroidTestUtilities.IsRunningOnWindows();

            // Assert
            Assert.True(isWindows, "Tests should run on Windows for WPF compatibility");
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void CreateMockApkFile_ShouldCreateValidFile()
        {
            // Arrange
            var fileName = "windows-test.apk";

            // Act
            var filePath = CreateTestApkFile(fileName);

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.True(filePath.EndsWith(fileName));
            
            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 0);
            
            // Проверяем ZIP signature (APK это ZIP архив)
            var bytes = File.ReadAllBytes(filePath);
            Assert.Equal(0x50, bytes[0]); // 'P'
            Assert.Equal(0x4B, bytes[1]); // 'K'
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void CreateMockXapkFile_ShouldCreateValidZipArchive()
        {
            // Arrange
            var fileName = "windows-test.xapk";

            // Act
            var filePath = CreateTestXapkFile(fileName);

            // Assert
            Assert.True(File.Exists(filePath));
            
            // Проверяем что можем открыть как ZIP архив
            using var archive = new System.IO.Compression.ZipArchive(File.OpenRead(filePath));
            
            Assert.Contains(archive.Entries, entry => entry.Name == "manifest.json");
            Assert.Contains(archive.Entries, entry => entry.Name == "base.apk");
            Assert.Contains(archive.Entries, entry => entry.Name == "config.arm64_v8a.apk");
        }

        [AndroidTestUtilities.WindowsOnlyTheory]
        [InlineData("com.example.app1", "Test App 1")]
        [InlineData("com.company.game", "Super Game")]
        [InlineData("org.opensource.tool", "Open Tool")]
        public void CreateTestApkMetadata_ShouldGenerateValidMetadata(string packageName, string appName)
        {
            // Act
            var metadata = AndroidTestUtilities.CreateTestApkMetadata(packageName, appName);

            // Assert
            Assert.Equal(packageName, metadata.PackageName);
            Assert.Equal(appName, metadata.AppName);
            Assert.Equal("1.0.0", metadata.VersionName);
            Assert.Equal(1, metadata.VersionCode);
            Assert.Equal(21, metadata.MinSdkVersion);
            Assert.Equal(33, metadata.TargetSdkVersion);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void GenerateAaptOutput_ShouldCreateRealisticOutput()
        {
            // Act
            var aaptOutput = AndroidTestUtilities.GenerateAaptOutput(
                "com.example.testapp",
                "Test Application",
                "2.1.0",
                210);

            // Assert
            Assert.Contains("package: name='com.example.testapp'", aaptOutput);
            Assert.Contains("versionCode='210'", aaptOutput);
            Assert.Contains("versionName='2.1.0'", aaptOutput);
            Assert.Contains("application-label:'Test Application'", aaptOutput);
            Assert.Contains("launchable-activity:", aaptOutput);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void GenerateAdbPackagesOutput_ShouldFormatCorrectly()
        {
            // Act
            var packagesOutput = AndroidTestUtilities.GenerateAdbPackagesOutput(
                "com.example.app1",
                "com.example.app2",
                "com.company.game");

            // Assert
            Assert.Contains("package:com.example.app1", packagesOutput);
            Assert.Contains("package:com.example.app2", packagesOutput);
            Assert.Contains("package:com.company.game", packagesOutput);
            
            var lines = packagesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void TempDirectory_ShouldBeCreatedAndAccessible()
        {
            // Assert
            Assert.True(Directory.Exists(TempDirectory));
            Assert.True(TempDirectory.Contains("WindowsLauncherTests"));
            
            // Проверяем что можем создавать файлы
            var testFile = Path.Combine(TempDirectory, "access-test.txt");
            File.WriteAllText(testFile, "test content");
            
            Assert.True(File.Exists(testFile));
            Assert.Equal("test content", File.ReadAllText(testFile));
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void SharedTestFiles_ShouldBeAccessible()
        {
            // Assert
            var sharedApkPath = Path.Combine(_fixture.SharedTempDirectory, "shared-test.apk");
            var sharedXapkPath = Path.Combine(_fixture.SharedTempDirectory, "shared-test.xapk");
            
            Assert.True(File.Exists(sharedApkPath));
            Assert.True(File.Exists(sharedXapkPath));
            
            // Проверяем размеры файлов
            var apkInfo = new FileInfo(sharedApkPath);
            var xapkInfo = new FileInfo(sharedXapkPath);
            
            Assert.Equal(2048, apkInfo.Length);
            Assert.True(xapkInfo.Length > 0);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void CreateSuccessfulConnectionServiceMock_ShouldReturnConfiguredMock()
        {
            // Act
            var mock = AndroidTestUtilities.CreateSuccessfulConnectionServiceMock();

            // Assert - проверяем все настроенные методы
            Assert.NotNull(mock);
            
            // Эти вызовы должны работать без дополнительной настройки
            var wsaAvailable = mock.Object.IsWSAAvailableAsync().Result;
            var wsaRunning = mock.Object.IsWSARunningAsync().Result;
            var adbAvailable = mock.Object.IsAdbAvailableAsync().Result;
            var connected = mock.Object.ConnectToWSAAsync().Result;
            var version = mock.Object.GetAndroidVersionAsync().Result;
            var status = mock.Object.GetConnectionStatusAsync().Result;

            Assert.True(wsaAvailable);
            Assert.True(wsaRunning);
            Assert.True(adbAvailable);
            Assert.True(connected);
            Assert.Equal("13", version);
            Assert.NotNull(status);
            Assert.True(status.ContainsKey("WSAAvailable"));
            Assert.Equal("adb", status["ADBPath"]);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void CreateProcessExecutorMock_ShouldHandleWindowsCommands()
        {
            // Act
            var mock = AndroidTestUtilities.CreateProcessExecutorMock();

            // Assert - проверяем Windows PowerShell команды
            var wsaCheck = mock.Object.ExecutePowerShellAsync(
                "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 5000).Result;
            
            Assert.True(wsaCheck.IsSuccess);
            Assert.Contains("Windows Subsystem for Android", wsaCheck.StandardOutput);

            // Проверяем ADB команды
            var adbAvailable = mock.Object.IsCommandAvailableAsync("adb").Result;
            Assert.True(adbAvailable);

            var adbVersion = mock.Object.ExecuteAsync("adb", "version", 5000, null).Result;
            Assert.True(adbVersion.IsSuccess);
            Assert.Contains("Android Debug Bridge", adbVersion.StandardOutput);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void ProcessResults_ShouldBeCreatedCorrectly()
        {
            // Act
            var successResult = AndroidTestUtilities.CreateSuccessfulProcessResult("Command executed successfully");
            var failResult = AndroidTestUtilities.CreateFailedProcessResult("Access denied", 5);

            // Assert
            Assert.True(successResult.IsSuccess);
            Assert.Equal(0, successResult.ExitCode);
            Assert.Equal("Command executed successfully", successResult.StandardOutput);
            Assert.Empty(successResult.StandardError);

            Assert.False(failResult.IsSuccess);
            Assert.Equal(5, failResult.ExitCode);
            Assert.Equal("Access denied", failResult.StandardError);
            Assert.Empty(failResult.StandardOutput);
        }

        [AndroidTestUtilities.WindowsOnlyFact]
        public void InstallResults_ShouldBeConfiguredCorrectly()
        {
            // Act
            var successResult = AndroidTestUtilities.CreateSuccessfulInstallResult(
                "com.example.success", 
                Core.Models.Android.InstallationMethod.XAPK);
            
            var failResult = AndroidTestUtilities.CreateFailedInstallResult(
                "Installation timeout",
                "com.example.timeout");

            // Assert
            Assert.True(successResult.Success);
            Assert.Equal("com.example.success", successResult.PackageName);
            Assert.Equal(Core.Models.Android.InstallationMethod.XAPK, successResult.InstallationMethod);
            Assert.Contains("com.example.success", successResult.InstalledPackages);
            Assert.Null(successResult.ErrorMessage);

            Assert.False(failResult.Success);
            Assert.Equal("com.example.timeout", failResult.PackageName);
            Assert.Equal("Installation timeout", failResult.ErrorMessage);
            Assert.Empty(failResult.InstalledPackages);
        }

        // Cleanup метод вызывается автоматически благодаря IDisposable
        public new void Dispose()
        {
            // Дополнительная очистка если нужна
            base.Dispose();
        }
    }
}
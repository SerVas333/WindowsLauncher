using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Services.Android;
using Xunit;
using System.Diagnostics;

namespace WindowsLauncher.Tests.Services.Android
{
    public class WSAIntegrationServiceTests
    {
        private readonly Mock<ILogger<WSAIntegrationService>> _mockLogger;
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly WSAIntegrationService _service;

        public WSAIntegrationServiceTests()
        {
            _mockLogger = new Mock<ILogger<WSAIntegrationService>>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _service = new WSAIntegrationService(_mockLogger.Object, _mockProcessExecutor.Object);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSAInstalled_ReturnsTrue()
        {
            // Arrange
            var wsaCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "WSA is running",
                StandardError = ""
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("powershell", It.IsAny<string>(), It.IsAny<int>()))
                               .ReturnsAsync(wsaCheckResult);

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSANotInstalled_ReturnsFalse()
        {
            // Arrange
            var wsaCheckResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "WSA not found"
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("powershell", It.IsAny<string>(), It.IsAny<int>()))
                               .ReturnsAsync(wsaCheckResult);

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateApkFileAsync_WithValidApkFile_ReturnsTrue()
        {
            // Arrange
            var apkPath = "C:\\test\\valid-app.apk";
            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "package: name='com.example.app' versionCode='1' versionName='1.0'",
                StandardError = ""
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>()))
                               .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateApkFileAsync_WithInvalidApkFile_ReturnsFalse()
        {
            // Arrange
            var apkPath = "C:\\test\\invalid.apk";
            var aaptResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "ERROR: dump failed because the resource table is invalid"
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>()))
                               .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExtractApkMetadataAsync_WithValidApk_ReturnsCorrectMetadata()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var aaptOutput = @"
package: name='com.example.testapp' versionCode='42' versionName='1.2.3' platformBuildVersionName='14' platformBuildVersionCode='34' compileSdkVersion='34' compileSdkVersionCodename='14'
application-label:'Test Application'
sdkVersion:'21'
targetSdkVersion:'34'
";

            var aaptResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = aaptOutput,
                StandardError = ""
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>()))
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
        public async Task ExtractApkMetadataAsync_WithInvalidApk_ReturnsNull()
        {
            // Arrange
            var apkPath = "C:\\test\\invalid.apk";
            var aaptResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "ERROR: dump failed"
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"", It.IsAny<int>()))
                               .ReturnsAsync(aaptResult);

            // Act
            var result = await _service.ExtractApkMetadataAsync(apkPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task InstallApkAsync_WithValidApk_ReturnsSuccess()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var packageName = "com.example.app";

            // Setup metadata extraction
            var metadata = new ApkMetadata { PackageName = packageName };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("aapt", It.IsAny<string>(), It.IsAny<int>()))
                               .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = $"package: name='{packageName}'" });

            // Setup ADB install
            var adbResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = ""
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"install \"{apkPath}\"", It.IsAny<int>()))
                               .ReturnsAsync(adbResult);

            // Act
            var result = await _service.InstallApkAsync(apkPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(packageName, result.PackageName);
        }

        [Fact]
        public async Task InstallApkAsync_WithAdbError_ReturnsFailure()
        {
            // Arrange
            var apkPath = "C:\\test\\app.apk";
            var adbResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "INSTALL_FAILED_INSUFFICIENT_STORAGE"
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"install \"{apkPath}\"", It.IsAny<int>()))
                               .ReturnsAsync(adbResult);

            // Act
            var result = await _service.InstallApkAsync(apkPath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE", result.ErrorMessage);
        }

        [Fact]
        public async Task LaunchAppAsync_WithValidPackage_ReturnsSuccess()
        {
            // Arrange
            var packageName = "com.example.app";
            var adbResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Starting: Intent { act=android.intent.action.MAIN cat=[android.intent.category.LAUNCHER] cmp=com.example.app/.MainActivity }",
                StandardError = ""
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1", It.IsAny<int>()))
                               .ReturnsAsync(adbResult);

            // Act
            var result = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetInstalledAppsAsync_ReturnsAppList()
        {
            // Arrange
            var adbOutput = @"
package:com.android.systemui
package:com.example.app1
package:com.example.app2
package:com.android.settings
";

            var adbResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = adbOutput,
                StandardError = ""
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages", It.IsAny<int>()))
                               .ReturnsAsync(adbResult);

            // Act
            var result = await _service.GetInstalledAppsAsync();

            // Assert
            var apps = result.ToList();
            Assert.Equal(4, apps.Count);
            Assert.Contains(apps, app => app.PackageName == "com.example.app1");
            Assert.Contains(apps, app => app.PackageName == "com.example.app2");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("not-an-apk-file.txt")]
        public async Task ValidateApkFileAsync_WithInvalidInput_ReturnsFalse(string apkPath)
        {
            // Act
            var result = await _service.ValidateApkFileAsync(apkPath);

            // Assert
            Assert.False(result);
        }
    }

    // Временные интерфейсы и классы для тестов (будут перенесены в основной код)
    public interface IProcessExecutor
    {
        Task<ProcessResult> ExecuteAsync(string fileName, string arguments, int timeoutMs = 30000);
    }

    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }

    // Временная реализация WSAIntegrationService для тестов
    public class WSAIntegrationService : IWSAIntegrationService
    {
        private readonly ILogger<WSAIntegrationService> _logger;
        private readonly IProcessExecutor _processExecutor;

        public WSAIntegrationService(ILogger<WSAIntegrationService> logger, IProcessExecutor processExecutor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
        }

        public async Task<bool> IsWSAAvailableAsync()
        {
            try
            {
                var result = await _processExecutor.ExecuteAsync("powershell", 
                    "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 10000);
                return result.ExitCode == 0 && !string.IsNullOrEmpty(result.StandardOutput);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ValidateApkFileAsync(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath) || !apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                return false;

            var result = await _processExecutor.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"");
            return result.ExitCode == 0;
        }

        public async Task<ApkMetadata> ExtractApkMetadataAsync(string apkPath)
        {
            var result = await _processExecutor.ExecuteAsync("aapt", $"dump badging \"{apkPath}\"");
            
            if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput))
                return null;

            return ParseApkMetadata(result.StandardOutput);
        }

        public async Task<ApkInstallResult> InstallApkAsync(string apkPath)
        {
            var result = await _processExecutor.ExecuteAsync("adb", $"install \"{apkPath}\"");
            
            if (result.ExitCode == 0)
            {
                var metadata = await ExtractApkMetadataAsync(apkPath);
                return new ApkInstallResult { Success = true, PackageName = metadata?.PackageName };
            }
            else
            {
                return new ApkInstallResult { Success = false, ErrorMessage = result.StandardError };
            }
        }

        public async Task<AppLaunchResult> LaunchAppAsync(string packageName)
        {
            var result = await _processExecutor.ExecuteAsync("adb", 
                $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
            
            return new AppLaunchResult { Success = result.ExitCode == 0 };
        }

        public async Task<IEnumerable<InstalledAndroidApp>> GetInstalledAppsAsync()
        {
            var result = await _processExecutor.ExecuteAsync("adb", "shell pm list packages");
            
            if (result.ExitCode != 0)
                return Enumerable.Empty<InstalledAndroidApp>();

            return ParseInstalledApps(result.StandardOutput);
        }

        private ApkMetadata ParseApkMetadata(string aaptOutput)
        {
            var lines = aaptOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var metadata = new ApkMetadata();

            foreach (var line in lines)
            {
                if (line.StartsWith("package:"))
                {
                    metadata.PackageName = ExtractValue(line, "name=");
                    metadata.VersionCode = int.Parse(ExtractValue(line, "versionCode=") ?? "0");
                    metadata.VersionName = ExtractValue(line, "versionName=");
                }
                else if (line.StartsWith("application-label:"))
                {
                    metadata.AppName = line.Substring("application-label:".Length).Trim('\'');
                }
                else if (line.StartsWith("sdkVersion:"))
                {
                    metadata.MinSdkVersion = int.Parse(ExtractValue(line, "sdkVersion:") ?? "0");
                }
                else if (line.StartsWith("targetSdkVersion:"))
                {
                    metadata.TargetSdkVersion = int.Parse(ExtractValue(line, "targetSdkVersion:") ?? "0");
                }
            }

            return metadata;
        }

        private IEnumerable<InstalledAndroidApp> ParseInstalledApps(string adbOutput)
        {
            return adbOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                           .Where(line => line.StartsWith("package:"))
                           .Select(line => new InstalledAndroidApp 
                           { 
                               PackageName = line.Substring("package:".Length).Trim() 
                           });
        }

        private string ExtractValue(string line, string key)
        {
            var start = line.IndexOf(key);
            if (start == -1) return null;
            
            start += key.Length;
            if (start >= line.Length) return null;
            
            var quote = line[start] == '\'' ? '\'' : ' ';
            if (quote == '\'') start++;
            
            var end = line.IndexOf(quote, start);
            if (end == -1) end = line.Length;
            
            return line.Substring(start, end - start);
        }
    }
}
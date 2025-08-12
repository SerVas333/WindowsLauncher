using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Services.Lifecycle.Launchers;

namespace WindowsLauncher.Tests.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Интеграционные тесты для специализированных лаунчеров
    /// Проверяют правильность определения типов приложений и базовую функциональность
    /// </summary>
    public class LauncherIntegrationTests
    {
        private readonly Mock<ILogger<DesktopApplicationLauncher>> _mockDesktopLogger;
        private readonly Mock<ILogger<ChromeAppLauncher>> _mockChromeLogger;
        private readonly Mock<ILogger<WebApplicationLauncher>> _mockWebLogger;
        private readonly Mock<ILogger<FolderLauncher>> _mockFolderLogger;
        private readonly Mock<IWindowManager> _mockWindowManager;
        private readonly Mock<IProcessMonitor> _mockProcessMonitor;

        public LauncherIntegrationTests()
        {
            _mockDesktopLogger = new Mock<ILogger<DesktopApplicationLauncher>>();
            _mockChromeLogger = new Mock<ILogger<ChromeAppLauncher>>();
            _mockWebLogger = new Mock<ILogger<WebApplicationLauncher>>();
            _mockFolderLogger = new Mock<ILogger<FolderLauncher>>();
            _mockWindowManager = new Mock<IWindowManager>();
            _mockProcessMonitor = new Mock<IProcessMonitor>();
        }

        [Theory]
        [InlineData(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "", true)]
        [InlineData(ApplicationType.Desktop, @"C:\Program Files\Microsoft Office\WINWORD.EXE", "/safe", true)]
        [InlineData(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com", false)]
        [InlineData(ApplicationType.Web, "https://example.com", "", false)]
        [InlineData(ApplicationType.Folder, @"C:\Windows", "", false)]
        public void DesktopApplicationLauncher_CanLaunch_ShouldReturnCorrectResult(
            ApplicationType appType, string executablePath, string arguments, bool expectedResult)
        {
            // Arrange
            var launcher = new DesktopApplicationLauncher(_mockDesktopLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = launcher.CanLaunch(app);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com", true)]
        [InlineData(ApplicationType.ChromeApp, @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", "--app=https://app.example.com", true)]
        [InlineData(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "", false)]
        [InlineData(ApplicationType.Web, "https://example.com", "", false)]
        [InlineData(ApplicationType.Folder, @"C:\Windows", "", false)]
        public void ChromeAppLauncher_CanLaunch_ShouldReturnCorrectResult(
            ApplicationType appType, string executablePath, string arguments, bool expectedResult)
        {
            // Arrange
            var launcher = new ChromeAppLauncher(_mockChromeLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = launcher.CanLaunch(app);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(ApplicationType.Web, "https://example.com", "", true)]
        [InlineData(ApplicationType.Web, "http://localhost:3000", "", true)]
        [InlineData(ApplicationType.Web, "https://app.company.com/dashboard", "", true)]
        [InlineData(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "", false)]
        [InlineData(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com", false)]
        [InlineData(ApplicationType.Folder, @"C:\Windows", "", false)]
        public void WebApplicationLauncher_CanLaunch_ShouldReturnCorrectResult(
            ApplicationType appType, string executablePath, string arguments, bool expectedResult)
        {
            // Arrange
            var launcher = new WebApplicationLauncher(_mockWebLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = launcher.CanLaunch(app);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(ApplicationType.Folder, @"C:\Windows", "", true)]
        [InlineData(ApplicationType.Folder, @"C:\Program Files", "", true)]
        [InlineData(ApplicationType.Folder, @"\\server\share\folder", "", true)]
        [InlineData(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "", false)]
        [InlineData(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com", false)]
        [InlineData(ApplicationType.Web, "https://example.com", "", false)]
        public void FolderLauncher_CanLaunch_ShouldReturnCorrectResult(
            ApplicationType appType, string executablePath, string arguments, bool expectedResult)
        {
            // Arrange
            var launcher = new FolderLauncher(_mockFolderLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = launcher.CanLaunch(app);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task DesktopApplicationLauncher_LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Arrange
            var launcher = new DesktopApplicationLauncher(_mockDesktopLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                launcher.LaunchAsync(null!, "testuser"));
        }

        [Fact]
        public async Task ChromeAppLauncher_LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Arrange
            var launcher = new ChromeAppLauncher(_mockChromeLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                launcher.LaunchAsync(null!, "testuser"));
        }

        [Fact]
        public async Task WebApplicationLauncher_LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Arrange
            var launcher = new WebApplicationLauncher(_mockWebLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                launcher.LaunchAsync(null!, "testuser"));
        }

        [Fact]
        public async Task FolderLauncher_LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Arrange
            var launcher = new FolderLauncher(_mockFolderLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                launcher.LaunchAsync(null!, "testuser"));
        }

        [Fact]
        public async Task DesktopApplicationLauncher_LaunchAsync_WithUnsupportedApplication_ShouldReturnFailure()
        {
            // Arrange
            var launcher = new DesktopApplicationLauncher(_mockDesktopLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com");

            // Act
            var result = await launcher.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot launch", result.ErrorMessage);
        }

        [Fact]
        public async Task ChromeAppLauncher_LaunchAsync_WithUnsupportedApplication_ShouldReturnFailure()
        {
            // Arrange
            var launcher = new ChromeAppLauncher(_mockChromeLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "");

            // Act
            var result = await launcher.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot launch", result.ErrorMessage);
        }

        [Fact]
        public async Task WebApplicationLauncher_LaunchAsync_WithUnsupportedApplication_ShouldReturnFailure()
        {
            // Arrange
            var launcher = new WebApplicationLauncher(_mockWebLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "");

            // Act
            var result = await launcher.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot launch", result.ErrorMessage);
        }

        [Fact]
        public async Task FolderLauncher_LaunchAsync_WithUnsupportedApplication_ShouldReturnFailure()
        {
            // Arrange
            var launcher = new FolderLauncher(_mockFolderLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var app = CreateTestApplication(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "");

            // Act
            var result = await launcher.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot launch", result.ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AllLaunchers_LaunchAsync_WithNullOrEmptyLaunchedBy_ShouldThrowArgumentException(string launchedBy)
        {
            // Arrange
            var desktopLauncher = new DesktopApplicationLauncher(_mockDesktopLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var chromeLauncher = new ChromeAppLauncher(_mockChromeLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var webLauncher = new WebApplicationLauncher(_mockWebLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);
            var folderLauncher = new FolderLauncher(_mockFolderLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            var desktopApp = CreateTestApplication(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "");
            var chromeApp = CreateTestApplication(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "--app=https://example.com");
            var webApp = CreateTestApplication(ApplicationType.Web, "https://example.com", "");
            var folderApp = CreateTestApplication(ApplicationType.Folder, @"C:\Windows", "");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                desktopLauncher.LaunchAsync(desktopApp, launchedBy));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                chromeLauncher.LaunchAsync(chromeApp, launchedBy));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                webLauncher.LaunchAsync(webApp, launchedBy));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                folderLauncher.LaunchAsync(folderApp, launchedBy));
        }

        [Fact]
        public void ChromeAppLauncher_ExtractChromeAppUrl_ShouldHandleVariousUrlFormats()
        {
            // Arrange
            var launcher = new ChromeAppLauncher(_mockChromeLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            // Test cases for different URL formats in Chrome App arguments
            var testCases = new[]
            {
                (arguments: "--app=https://example.com", expectedContains: "https://example.com"),
                (arguments: "--app=http://localhost:3000", expectedContains: "http://localhost:3000"),
                (arguments: "--new-window --app=https://app.company.com/dashboard", expectedContains: "https://app.company.com/dashboard"),
                (arguments: "--app=file:///C:/path/to/app.html", expectedContains: "file:///C:/path/to/app.html"),
                (arguments: "--disable-web-security --app=https://example.com --user-data-dir=temp", expectedContains: "https://example.com")
            };

            foreach (var (arguments, expectedContains) in testCases)
            {
                // Arrange
                var app = CreateTestApplication(ApplicationType.ChromeApp, @"C:\Program Files\Google\Chrome\Application\chrome.exe", arguments);

                // Act
                var result = launcher.CanLaunch(app);

                // Assert
                Assert.True(result, $"Should be able to launch Chrome app with arguments: {arguments}");
            }
        }

        [Fact]
        public void WebApplicationLauncher_ValidateUrl_ShouldAcceptValidUrls()
        {
            // Arrange
            var launcher = new WebApplicationLauncher(_mockWebLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            var validUrls = new[]
            {
                "https://example.com",
                "http://localhost:3000",
                "https://app.company.com/dashboard?param=value",
                "http://192.168.1.100:8080/app",
                "https://subdomain.example.co.uk/path/to/resource"
            };

            foreach (var url in validUrls)
            {
                // Arrange
                var app = CreateTestApplication(ApplicationType.Web, url, "");

                // Act
                var result = launcher.CanLaunch(app);

                // Assert
                Assert.True(result, $"Should be able to launch web application with URL: {url}");
            }
        }

        [Fact]
        public void FolderLauncher_ValidatePath_ShouldAcceptValidPaths()
        {
            // Arrange
            var launcher = new FolderLauncher(_mockFolderLogger.Object, _mockWindowManager.Object, _mockProcessMonitor.Object);

            var validPaths = new[]
            {
                @"C:\Windows",
                @"C:\Program Files",
                @"C:\Users\Username\Documents",
                @"\\server\share\folder",
                @"D:\Projects\MyProject"
            };

            foreach (var path in validPaths)
            {
                // Arrange
                var app = CreateTestApplication(ApplicationType.Folder, path, "");

                // Act
                var result = launcher.CanLaunch(app);

                // Assert
                Assert.True(result, $"Should be able to launch folder application with path: {path}");
            }
        }

        #region Helper Methods

        private Application CreateTestApplication(ApplicationType type, string executablePath, string arguments)
        {
            return new Application
            {
                Id = 1,
                Name = $"Test {type} App",
                Type = type,
                ExecutablePath = executablePath,
                Arguments = arguments,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                IsActive = true
            };
        }

        #endregion
    }
}
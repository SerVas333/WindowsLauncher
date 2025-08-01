using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.UI.Services;
using WindowsLauncher.UI.Components.WebView2;

namespace WindowsLauncher.Tests.Services.Lifecycle.Launchers
{
    /// <summary>
    /// Тесты для WebView2ApplicationLauncher
    /// Проверяют логику определения совместимости и базовую функциональность
    /// </summary>
    public class WebView2ApplicationLauncherTests : IDisposable
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<WebView2ApplicationLauncher>> _mockLogger;
        private readonly Mock<ILogger<WebView2ApplicationWindow>> _mockWindowLogger;
        private readonly WebView2ApplicationLauncher _launcher;

        public WebView2ApplicationLauncherTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<WebView2ApplicationLauncher>>();
            _mockWindowLogger = new Mock<ILogger<WebView2ApplicationWindow>>();

            // Настраиваем ServiceProvider для возврата window logger
            _mockServiceProvider.Setup(sp => sp.GetRequiredService<ILogger<WebView2ApplicationWindow>>())
                .Returns(_mockWindowLogger.Object);

            _launcher = new WebView2ApplicationLauncher(_mockServiceProvider.Object, _mockLogger.Object);
        }

        [Theory]
        [InlineData(ApplicationType.ChromeApp, "https://example.com", "--app=https://example.com", true)]
        [InlineData(ApplicationType.ChromeApp, "chrome.exe", "--app=http://localhost:3000", true)]
        [InlineData(ApplicationType.Web, "https://app.company.com", "", true)]
        [InlineData(ApplicationType.Web, "http://localhost:8080", "", true)]
        [InlineData(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "", false)]
        [InlineData(ApplicationType.Folder, @"C:\Windows", "", false)]
        public void CanLaunch_WithVariousApplicationTypes_ShouldReturnCorrectResult(
            ApplicationType appType, string executablePath, string arguments, bool expectedResult)
        {
            // Arrange
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = _launcher.CanLaunch(app);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CanLaunch_WithNullApplication_ShouldReturnFalse()
        {
            // Act
            var result = _launcher.CanLaunch(null);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(ApplicationType.ChromeApp, "", "")] // Пустой URL
        [InlineData(ApplicationType.ChromeApp, "chrome.exe", "--app=invalid-url")] // Невалидный URL
        [InlineData(ApplicationType.Web, "not-a-url", "")] // Невалидный URL
        [InlineData(ApplicationType.Web, "file:///local/file.html", "")] // Не HTTP/HTTPS
        public void CanLaunch_WithInvalidUrls_ShouldReturnFalse(
            ApplicationType appType, string executablePath, string arguments)
        {
            // Arrange
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = _launcher.CanLaunch(app);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _launcher.LaunchAsync(null!, "testuser"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task LaunchAsync_WithNullOrEmptyLaunchedBy_ShouldThrowArgumentException(string launchedBy)
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Web, "https://example.com", "");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _launcher.LaunchAsync(app, launchedBy));
        }

        [Fact]
        public async Task LaunchAsync_WithUnsupportedApplication_ShouldReturnFailure()
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Desktop, @"C:\Windows\System32\notepad.exe", "");

            // Act
            var result = await _launcher.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot launch", result.ErrorMessage);
        }

        [Fact]
        public async Task SwitchToAsync_WithNonExistentInstance_ShouldReturnFalse()
        {
            // Act
            var result = await _launcher.SwitchToAsync("non-existent-instance");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TerminateAsync_WithNonExistentInstance_ShouldReturnFalse()
        {
            // Act
            var result = await _launcher.TerminateAsync("non-existent-instance");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetActiveInstancesAsync_WithNoActiveWindows_ShouldReturnEmptyList()
        {
            // Act
            var result = await _launcher.GetActiveInstancesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(ApplicationType.ChromeApp, "chrome.exe", "--app=https://example.com", "https://example.com")]
        [InlineData(ApplicationType.ChromeApp, "chrome.exe", "--new-window --app=http://localhost:3000", "http://localhost:3000")]
        [InlineData(ApplicationType.Web, "https://app.company.com", "", "https://app.company.com")]
        public void CanLaunch_WithValidWebUrls_ShouldReturnTrue(
            ApplicationType appType, string executablePath, string arguments, string expectedUrl)
        {
            // Arrange
            var app = CreateTestApplication(appType, executablePath, arguments);

            // Act
            var result = _launcher.CanLaunch(app);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void WebView2ApplicationLauncher_Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new WebView2ApplicationLauncher(null!, _mockLogger.Object));
        }

        [Fact]
        public void WebView2ApplicationLauncher_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new WebView2ApplicationLauncher(_mockServiceProvider.Object, null!));
        }

        [Fact]
        public async Task TerminateAsync_WithForceFlag_ShouldAcceptParameter()
        {
            // Act - должно корректно принимать параметр force
            var resultNormal = await _launcher.TerminateAsync("test-instance", false);
            var resultForce = await _launcher.TerminateAsync("test-instance", true);

            // Assert - оба вызова должны завершиться без исключений
            Assert.False(resultNormal); // Экземпляр не существует
            Assert.False(resultForce);  // Экземпляр не существует
        }

        [Theory]
        [InlineData("https://example.com")]
        [InlineData("http://localhost:3000")]
        [InlineData("https://subdomain.example.co.uk/path?param=value")]
        public void CanLaunch_WithValidHttpUrls_ShouldReturnTrue(string url)
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Web, url, "");

            // Act
            var result = _launcher.CanLaunch(app);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("ftp://example.com")]
        [InlineData("file:///local/path")]
        [InlineData("javascript:alert('test')")]
        [InlineData("data:text/html,<h1>Test</h1>")]
        public void CanLaunch_WithNonHttpUrls_ShouldReturnFalse(string url)
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Web, url, "");

            // Act
            var result = _launcher.CanLaunch(app);

            // Assert
            Assert.False(result);
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

        public void Dispose()
        {
            _launcher?.Dispose();
        }
    }
}
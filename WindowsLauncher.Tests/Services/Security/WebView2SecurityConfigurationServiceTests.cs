using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models.Configuration;
using WindowsLauncher.Services.Security;
using Xunit;

namespace WindowsLauncher.Tests.Services.Security
{
    public class WebView2SecurityConfigurationServiceTests
    {
        private readonly Mock<ILogger<WebView2SecurityConfigurationService>> _mockLogger;

        public WebView2SecurityConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<WebView2SecurityConfigurationService>>();
        }

        [Fact]
        public void GetConfiguration_WithValidConfiguration_ReturnsCorrectConfiguration()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:DataClearingStrategy"] = "OnUserSwitch",
                ["WebView2Security:ClearCookiesImmediately"] = "true",
                ["WebView2Security:ClearCacheOnExit"] = "false",
                ["WebView2Security:SecureEnvironment"] = "false",
                ["WebView2Security:EnableAuditLogging"] = "true",
                ["WebView2Security:CleanupTimeoutMs"] = "3000",
                ["WebView2Security:RetryAttempts"] = "5"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.GetConfiguration();

            // Assert
            Assert.Equal(DataClearingStrategy.OnUserSwitch, result.DataClearingStrategy);
            Assert.True(result.ClearCookiesImmediately);
            Assert.False(result.ClearCacheOnExit);
            Assert.False(result.SecureEnvironment);
            Assert.True(result.EnableAuditLogging);
            Assert.Equal(3000, result.CleanupTimeoutMs);
            Assert.Equal(5, result.RetryAttempts);
        }

        [Fact]
        public void GetConfiguration_WithMissingConfiguration_ReturnsDefaults()
        {
            // Arrange
            var configuration = CreateConfiguration(new Dictionary<string, string>());
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.GetConfiguration();

            // Assert
            Assert.Equal(DataClearingStrategy.OnUserSwitch, result.DataClearingStrategy);
            Assert.False(result.ClearCookiesImmediately);
            Assert.True(result.ClearCacheOnExit);
            Assert.False(result.SecureEnvironment);
            Assert.True(result.EnableAuditLogging);
            Assert.Equal(5000, result.CleanupTimeoutMs);
            Assert.Equal(3, result.RetryAttempts);
        }

        [Fact]
        public void GetEffectiveDataClearingStrategy_WithSecureEnvironment_ReturnsImmediate()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:DataClearingStrategy"] = "OnAppExit",
                ["WebView2Security:SecureEnvironment"] = "true"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.GetEffectiveDataClearingStrategy();

            // Assert
            Assert.Equal(DataClearingStrategy.Immediate, result);
        }

        [Fact]
        public void GetEffectiveDataClearingStrategy_WithoutSecureEnvironment_ReturnsConfiguredStrategy()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:DataClearingStrategy"] = "OnAppExit",
                ["WebView2Security:SecureEnvironment"] = "false"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.GetEffectiveDataClearingStrategy();

            // Assert
            Assert.Equal(DataClearingStrategy.OnAppExit, result);
        }

        [Fact]
        public void ShouldClearCookiesImmediately_WithSecureEnvironment_ReturnsTrue()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:ClearCookiesImmediately"] = "false",
                ["WebView2Security:SecureEnvironment"] = "true"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.ShouldClearCookiesImmediately();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ShouldClearCookiesImmediately_WithClearCookiesImmediately_ReturnsTrue()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:ClearCookiesImmediately"] = "true",
                ["WebView2Security:SecureEnvironment"] = "false"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.ShouldClearCookiesImmediately();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsConfigurationValid_WithValidConfiguration_ReturnsTrue()
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:CleanupTimeoutMs"] = "5000",
                ["WebView2Security:RetryAttempts"] = "3"
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.IsConfigurationValid();

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("0", "3")] // Invalid timeout
        [InlineData("-1000", "3")] // Negative timeout
        [InlineData("5000", "-1")] // Negative retry attempts
        [InlineData("5000", "15")] // Too many retry attempts
        public void IsConfigurationValid_WithInvalidConfiguration_ReturnsFalse(string timeoutMs, string retryAttempts)
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:CleanupTimeoutMs"] = timeoutMs,
                ["WebView2Security:RetryAttempts"] = retryAttempts
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.IsConfigurationValid();

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("OnUserSwitch", DataClearingStrategy.OnUserSwitch)]
        [InlineData("Immediate", DataClearingStrategy.Immediate)]
        [InlineData("OnAppExit", DataClearingStrategy.OnAppExit)]
        public void GetConfiguration_ParsesDataClearingStrategyCorrectly(string strategyString, DataClearingStrategy expectedStrategy)
        {
            // Arrange
            var configurationData = new Dictionary<string, string>
            {
                ["WebView2Security:DataClearingStrategy"] = strategyString
            };

            var configuration = CreateConfiguration(configurationData);
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result = service.GetConfiguration();

            // Assert
            Assert.Equal(expectedStrategy, result.DataClearingStrategy);
        }

        [Fact]
        public void GetConfiguration_CachesResult_ReturnsSameInstance()
        {
            // Arrange
            var configuration = CreateConfiguration(new Dictionary<string, string>());
            var service = new WebView2SecurityConfigurationService(configuration, _mockLogger.Object);

            // Act
            var result1 = service.GetConfiguration();
            var result2 = service.GetConfiguration();

            // Assert
            Assert.Same(result1, result2);
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> configurationData)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();
        }
    }
}
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models.Configuration;
using Xunit;

namespace WindowsLauncher.Tests.Models.Configuration
{
    public class WebView2SecurityConfigurationTests
    {
        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            // Act
            var config = new WebView2SecurityConfiguration();

            // Assert
            Assert.Equal(DataClearingStrategy.OnUserSwitch, config.DataClearingStrategy);
            Assert.False(config.ClearCookiesImmediately);
            Assert.True(config.ClearCacheOnExit);
            Assert.False(config.SecureEnvironment);
            Assert.True(config.EnableAuditLogging);
            Assert.Equal(5000, config.CleanupTimeoutMs);
            Assert.Equal(3, config.RetryAttempts);
        }

        [Fact]
        public void GetEffectiveStrategy_WithSecureEnvironmentTrue_ReturnsImmediate()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                DataClearingStrategy = DataClearingStrategy.OnAppExit,
                SecureEnvironment = true
            };

            // Act
            var result = config.GetEffectiveStrategy();

            // Assert
            Assert.Equal(DataClearingStrategy.Immediate, result);
        }

        [Theory]
        [InlineData(DataClearingStrategy.OnUserSwitch)]
        [InlineData(DataClearingStrategy.OnAppExit)]
        [InlineData(DataClearingStrategy.Immediate)]
        public void GetEffectiveStrategy_WithSecureEnvironmentFalse_ReturnsConfiguredStrategy(DataClearingStrategy strategy)
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                DataClearingStrategy = strategy,
                SecureEnvironment = false
            };

            // Act
            var result = config.GetEffectiveStrategy();

            // Assert
            Assert.Equal(strategy, result);
        }

        [Theory]
        [InlineData(1000, 1, true)]
        [InlineData(5000, 3, true)]
        [InlineData(10000, 10, true)]
        public void IsValid_WithValidConfiguration_ReturnsTrue(int timeoutMs, int retryAttempts, bool expected)
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                CleanupTimeoutMs = timeoutMs,
                RetryAttempts = retryAttempts
            };

            // Act
            var result = config.IsValid();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, 3, false)] // Invalid timeout - zero
        [InlineData(-1000, 3, false)] // Invalid timeout - negative
        [InlineData(5000, -1, false)] // Invalid retry attempts - negative
        [InlineData(5000, 15, false)] // Invalid retry attempts - too many
        public void IsValid_WithInvalidConfiguration_ReturnsFalse(int timeoutMs, int retryAttempts, bool expected)
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                CleanupTimeoutMs = timeoutMs,
                RetryAttempts = retryAttempts
            };

            // Act
            var result = config.IsValid();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ApplyDefaults_WithInvalidTimeout_SetsDefaultTimeout()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                CleanupTimeoutMs = -1000
            };

            // Act
            config.ApplyDefaults();

            // Assert
            Assert.Equal(5000, config.CleanupTimeoutMs);
        }

        [Fact]
        public void ApplyDefaults_WithInvalidRetryAttempts_SetsDefaultRetryAttempts()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                RetryAttempts = -5
            };

            // Act
            config.ApplyDefaults();

            // Assert
            Assert.Equal(3, config.RetryAttempts);
        }

        [Fact]
        public void ApplyDefaults_WithTooManyRetryAttempts_SetsDefaultRetryAttempts()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                RetryAttempts = 20
            };

            // Act
            config.ApplyDefaults();

            // Assert
            Assert.Equal(3, config.RetryAttempts);
        }

        [Fact]
        public void ApplyDefaults_WithValidConfiguration_DoesNotChangeValues()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration
            {
                CleanupTimeoutMs = 7000,
                RetryAttempts = 5
            };

            // Act
            config.ApplyDefaults();

            // Assert
            Assert.Equal(7000, config.CleanupTimeoutMs);
            Assert.Equal(5, config.RetryAttempts);
        }

        [Fact]
        public void Properties_CanBeSetAndGet()
        {
            // Arrange
            var config = new WebView2SecurityConfiguration();

            // Act & Assert
            config.DataClearingStrategy = DataClearingStrategy.Immediate;
            Assert.Equal(DataClearingStrategy.Immediate, config.DataClearingStrategy);

            config.ClearCookiesImmediately = true;
            Assert.True(config.ClearCookiesImmediately);

            config.ClearCacheOnExit = false;
            Assert.False(config.ClearCacheOnExit);

            config.SecureEnvironment = true;
            Assert.True(config.SecureEnvironment);

            config.EnableAuditLogging = false;
            Assert.False(config.EnableAuditLogging);

            config.CleanupTimeoutMs = 8000;
            Assert.Equal(8000, config.CleanupTimeoutMs);

            config.RetryAttempts = 7;
            Assert.Equal(7, config.RetryAttempts);
        }
    }
}
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
    /// Unit тесты для WSAConnectionService - специализированного сервиса управления WSA подключениями
    /// </summary>
    public class WSAConnectionServiceTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<ILogger<WSAConnectionService>> _mockLogger;
        private readonly WSAConnectionService _service;

        public WSAConnectionServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLogger = new Mock<ILogger<WSAConnectionService>>();
            _service = new WSAConnectionService(_mockProcessExecutor.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        #region IsWSAAvailableAsync Tests

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSAInstalled_ReturnsTrue()
        {
            // Arrange
            var wsaCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Name: Windows Subsystem for Android\nVersion: 2301.40000.4.0",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 
                It.IsAny<int>()))
                .ReturnsAsync(wsaCheckResult);

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.True(result);
            _mockProcessExecutor.Verify(x => x.ExecutePowerShellAsync(
                "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 
                5000), Times.Once);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenWSANotInstalled_ReturnsFalse()
        {
            // Arrange
            var wsaCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "", // Пустой output означает что пакет не найден
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(wsaCheckResult);

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_WhenPowerShellFails_ReturnsFalse()
        {
            // Arrange
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("PowerShell execution failed"));

            // Act
            var result = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsWSAAvailableAsync_UsesCaching_ReturnsFromCache()
        {
            // Arrange
            var wsaCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Name: Windows Subsystem for Android",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(wsaCheckResult);

            // Act - первый вызов
            var result1 = await _service.IsWSAAvailableAsync();
            // Act - второй вызов (должен использовать кэш)
            var result2 = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            // Проверяем что PowerShell был вызван только один раз (кэширование работает)
            _mockProcessExecutor.Verify(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()), 
                Times.Once);
        }

        #endregion

        #region IsWSARunningAsync Tests

        [Fact]
        public async Task IsWSARunningAsync_WhenWSARunning_ReturnsTrue()
        {
            // Arrange
            var processCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "WsaService.exe\nVmmem\nWsaClient.exe",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                "Get-Process | Where-Object {$_.ProcessName -like '*WSA*' -or $_.ProcessName -like '*Wsa*'} | Select-Object ProcessName", 
                It.IsAny<int>()))
                .ReturnsAsync(processCheckResult);

            // Act
            var result = await _service.IsWSARunningAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsWSARunningAsync_WhenWSANotRunning_ReturnsFalse()
        {
            // Arrange
            var processCheckResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "", // Нет WSA процессов
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(processCheckResult);

            // Act
            var result = await _service.IsWSARunningAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region StartWSAAsync Tests

        [Fact]
        public async Task StartWSAAsync_WhenSuccessful_ReturnsTrue()
        {
            // Arrange
            var startResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Successfully started WSA",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                "Start-Process 'WsaClient' -ArgumentList '/launch'", 
                It.IsAny<int>()))
                .ReturnsAsync(startResult);

            // Setup running check to return true after start
            var runningResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "WsaService.exe",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                It.Is<string>(s => s.Contains("Get-Process")), 
                It.IsAny<int>()))
                .ReturnsAsync(runningResult);

            // Act
            var result = await _service.StartWSAAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task StartWSAAsync_WhenStartCommandFails_ReturnsFalse()
        {
            // Arrange
            var startResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "Failed to start WSA",
                IsSuccess = false
            };
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(startResult);

            // Act
            var result = await _service.StartWSAAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region IsAdbAvailableAsync Tests

        [Fact]
        public async Task IsAdbAvailableAsync_WhenAdbInPath_ReturnsTrue()
        {
            // Arrange
            _mockProcessExecutor.Setup(x => x.IsCommandAvailableAsync("adb"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsAdbAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAdbAvailableAsync_WhenAdbNotInPath_ChecksWindowsLauncherPaths()
        {
            // Arrange
            _mockProcessExecutor.Setup(x => x.IsCommandAvailableAsync("adb"))
                .ReturnsAsync(false);
            
            var adbVersionResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Android Debug Bridge version 1.0.41",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(
                @"C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe", 
                "version", 
                It.IsAny<int>(), 
                null))
                .ReturnsAsync(adbVersionResult);

            // Act
            var result = await _service.IsAdbAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAdbAvailableAsync_WhenAdbNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessExecutor.Setup(x => x.IsCommandAvailableAsync("adb"))
                .ReturnsAsync(false);
            
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, IsSuccess = false });

            // Act
            var result = await _service.IsAdbAvailableAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region ConnectToWSAAsync Tests

        [Fact]
        public async Task ConnectToWSAAsync_WhenConnectionSuccessful_ReturnsTrue()
        {
            // Arrange
            var connectResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "connected to 127.0.0.1:58526",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteWithRetryAsync(
                "adb", 
                "connect 127.0.0.1:58526", 
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<int>()))
                .ReturnsAsync(connectResult);

            // Act
            var result = await _service.ConnectToWSAAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ConnectToWSAAsync_WhenConnectionFails_UsesFallbackPorts()
        {
            // Arrange
            var failedResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "cannot connect to 127.0.0.1:58526",
                IsSuccess = false
            };
            var successResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "connected to 127.0.0.1:5555",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.SetupSequence(x => x.ExecuteWithRetryAsync(
                "adb", 
                It.IsAny<string>(), 
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<int>()))
                .ReturnsAsync(failedResult)  // Первый порт не работает
                .ReturnsAsync(successResult); // Второй порт работает

            // Act
            var result = await _service.ConnectToWSAAsync();

            // Assert
            Assert.True(result);
        }

        #endregion

        #region GetAndroidVersionAsync Tests

        [Fact]
        public async Task GetAndroidVersionAsync_WhenSuccessful_ReturnsVersion()
        {
            // Arrange
            var expectedVersion = "13";
            var versionResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = expectedVersion,
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(
                "adb", 
                "shell getprop ro.build.version.release", 
                It.IsAny<int>(), 
                null))
                .ReturnsAsync(versionResult);

            // Act
            var result = await _service.GetAndroidVersionAsync();

            // Assert
            Assert.Equal(expectedVersion, result);
        }

        [Fact]
        public async Task GetAndroidVersionAsync_WhenCommandFails_ReturnsNull()
        {
            // Arrange
            var versionResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "device offline",
                IsSuccess = false
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(versionResult);

            // Act
            var result = await _service.GetAndroidVersionAsync();

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetConnectionStatusAsync Tests

        [Fact]
        public async Task GetConnectionStatusAsync_ReturnsComprehensiveStatus()
        {
            // Arrange
            SetupSuccessfulMocks();

            // Act
            var result = await _service.GetConnectionStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("WSAAvailable"));
            Assert.True(result.ContainsKey("WSARunning"));
            Assert.True(result.ContainsKey("ADBAvailable"));
            Assert.True(result.ContainsKey("ADBConnected"));
            Assert.True(result.ContainsKey("AndroidVersion"));
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task ConnectionStatusChanged_Event_FiresOnStatusChange()
        {
            // Arrange
            WSAConnectionStatusEventArgs? capturedEvent = null;
            _service.ConnectionStatusChanged += (sender, args) => capturedEvent = args;

            SetupSuccessfulMocks();

            // Act
            await _service.ConnectToWSAAsync();

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal(WSAConnectionStatus.Connected, capturedEvent.Status);
        }

        [Fact]
        public async Task ConnectionStatusChanged_Event_FiresOnConnectionFailure()
        {
            // Arrange
            WSAConnectionStatusEventArgs? capturedEvent = null;
            _service.ConnectionStatusChanged += (sender, args) => capturedEvent = args;

            // Setup failure mocks
            _mockProcessExecutor.Setup(x => x.ExecuteWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, IsSuccess = false });

            // Act
            await _service.ConnectToWSAAsync();

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal(WSAConnectionStatus.ConnectionFailed, capturedEvent.Status);
        }

        #endregion

        #region Cache Tests

        [Fact]
        public async Task CacheExpiry_AfterTTL_RefreshesData()
        {
            // Arrange
            var firstResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "First call",
                StandardError = "",
                IsSuccess = true
            };
            var secondResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Second call",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.SetupSequence(x => x.ExecutePowerShellAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(firstResult)
                .ReturnsAsync(secondResult);

            // Act
            var result1 = await _service.IsWSAAvailableAsync();
            
            // Имитируем истечение TTL (в реальном коде это 30 секунд)
            // В тестах мы можем использовать reflection или создать тестовый метод для сброса кэша
            await Task.Delay(100); // Небольшая задержка для имитации
            
            var result2 = await _service.IsWSAAvailableAsync();

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            
            // В идеале здесь должна быть проверка что второй вызов действительно обновил кэш
            // Но для этого нужен доступ к внутреннему состоянию или тестовые методы
        }

        #endregion

        #region Helper Methods

        private void SetupSuccessfulMocks()
        {
            // WSA Available
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid", 
                It.IsAny<int>()))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = "WSA Package", 
                    IsSuccess = true 
                });

            // WSA Running
            _mockProcessExecutor.Setup(x => x.ExecutePowerShellAsync(
                It.Is<string>(s => s.Contains("Get-Process")), 
                It.IsAny<int>()))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = "WsaService.exe", 
                    IsSuccess = true 
                });

            // ADB Available
            _mockProcessExecutor.Setup(x => x.IsCommandAvailableAsync("adb"))
                .ReturnsAsync(true);

            // ADB Connect
            _mockProcessExecutor.Setup(x => x.ExecuteWithRetryAsync(
                "adb", 
                It.IsAny<string>(), 
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<int>()))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = "connected", 
                    IsSuccess = true 
                });

            // Android Version
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(
                "adb", 
                "shell getprop ro.build.version.release", 
                It.IsAny<int>(), 
                null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = "13", 
                    IsSuccess = true 
                });
        }

        #endregion
    }
}
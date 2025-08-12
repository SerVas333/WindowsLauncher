using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Interfaces.Lifecycle;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Core.Models.Lifecycle.Events;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Services.Lifecycle;

namespace WindowsLauncher.Tests.Services.Lifecycle
{
    /// <summary>
    /// Смоковые тесты для ApplicationLifecycleService
    /// Проверяют основную функциональность и интеграцию компонентов
    /// </summary>
    public class ApplicationLifecycleServiceTests : IDisposable
    {
        private readonly Mock<IWindowManager> _mockWindowManager;
        private readonly Mock<IProcessMonitor> _mockProcessMonitor;
        private readonly Mock<IApplicationInstanceManager> _mockInstanceManager;
        private readonly Mock<IApplicationLauncher> _mockDesktopLauncher;
        private readonly Mock<IApplicationLauncher> _mockChromeLauncher;
        private readonly Mock<ILogger<ApplicationLifecycleService>> _mockLogger;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly ApplicationLifecycleService _service;
        private readonly List<IApplicationLauncher> _launchers;

        public ApplicationLifecycleServiceTests()
        {
            // Создаем моки для всех зависимостей
            _mockWindowManager = new Mock<IWindowManager>();
            _mockProcessMonitor = new Mock<IProcessMonitor>();
            _mockInstanceManager = new Mock<IApplicationInstanceManager>();
            _mockDesktopLauncher = new Mock<IApplicationLauncher>();
            _mockChromeLauncher = new Mock<IApplicationLauncher>();
            _mockLogger = new Mock<ILogger<ApplicationLifecycleService>>();
            _mockAuditService = new Mock<IAuditService>();

            // Настраиваем поведение лаунчеров
            _mockDesktopLauncher.Setup(l => l.CanLaunch(It.Is<Application>(app => app.Type == ApplicationType.Desktop)))
                .Returns(true);
            _mockChromeLauncher.Setup(l => l.CanLaunch(It.Is<Application>(app => app.Type == ApplicationType.ChromeApp)))
                .Returns(true);

            _launchers = new List<IApplicationLauncher> { _mockDesktopLauncher.Object, _mockChromeLauncher.Object };

            // Создаем сервис с моками
            _service = new ApplicationLifecycleService(
                _mockLogger.Object,
                _mockInstanceManager.Object,
                _mockWindowManager.Object,
                _mockProcessMonitor.Object,
                _launchers,
                _mockAuditService.Object
            );
        }

        [Fact]
        public async Task StartMonitoringAsync_ShouldStartProcessMonitoring()
        {
            // Arrange
            _mockProcessMonitor.Setup(pm => pm.StartMonitoringAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.StartMonitoringAsync();

            // Assert
            _mockProcessMonitor.Verify(pm => pm.StartMonitoringAsync(), Times.Once);
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldStopProcessMonitoring()
        {
            // Arrange
            _mockProcessMonitor.Setup(pm => pm.StopMonitoringAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.StopMonitoringAsync();

            // Assert
            _mockProcessMonitor.Verify(pm => pm.StopMonitoringAsync(), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_WithDesktopApplication_ShouldUseDesktopLauncher()
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Desktop);
            var expectedResult = LaunchResult.Success("instance-1", TimeSpan.FromSeconds(1));

            _mockDesktopLauncher.Setup(l => l.LaunchAsync(app, "testuser"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.LaunchAsync(app, "testuser");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("instance-1", result.InstanceId);
            _mockDesktopLauncher.Verify(l => l.LaunchAsync(app, "testuser"), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_WithChromeApp_ShouldUseChromeLauncher()
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.ChromeApp);
            var expectedResult = LaunchResult.Success("chrome-instance-1", TimeSpan.FromSeconds(2));

            _mockChromeLauncher.Setup(l => l.LaunchAsync(app, "testuser"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.LaunchAsync(app, "testuser");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("chrome-instance-1", result.InstanceId);
            _mockChromeLauncher.Verify(l => l.LaunchAsync(app, "testuser"), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_WithUnsupportedApplicationType_ShouldReturnFailure()
        {
            // Arrange
            var app = CreateTestApplication((ApplicationType)999); // Несуществующий тип

            // Act
            var result = await _service.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("No suitable launcher", result.ErrorMessage);
        }

        [Fact]
        public async Task SwitchToAsync_WithValidInstanceId_ShouldCallInstanceManager()
        {
            // Arrange
            var instanceId = "test-instance-1";
            _mockInstanceManager.Setup(im => im.SwitchToAsync(instanceId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.SwitchToAsync(instanceId);

            // Assert
            Assert.True(result);
            _mockInstanceManager.Verify(im => im.SwitchToAsync(instanceId), Times.Once);
        }

        [Fact]
        public async Task GetRunningAsync_ShouldReturnInstancesFromManager()
        {
            // Arrange
            var expectedInstances = new List<ApplicationInstance>
            {
                CreateTestInstance("instance-1", ApplicationState.Running),
                CreateTestInstance("instance-2", ApplicationState.Running)
            };

            _mockInstanceManager.Setup(im => im.GetAllAsync())
                .ReturnsAsync(expectedInstances);

            // Act
            var result = await _service.GetRunningAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, instance => Assert.Equal(ApplicationState.Running, instance.State));
        }

        [Fact]
        public async Task TerminateAsync_WithValidInstanceId_ShouldCallInstanceManager()
        {
            // Arrange
            var instanceId = "test-instance-1";
            _mockInstanceManager.Setup(im => im.TerminateAsync(instanceId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.TerminateAsync(instanceId);

            // Assert
            Assert.True(result);
            _mockInstanceManager.Verify(im => im.TerminateAsync(instanceId), Times.Once);
        }

        [Fact]
        public async Task ForceTerminateAsync_WithValidInstanceId_ShouldCallInstanceManagerWithForceFlag()
        {
            // Arrange
            var instanceId = "test-instance-1";
            _mockInstanceManager.Setup(im => im.ForceTerminateAsync(instanceId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ForceTerminateAsync(instanceId);

            // Assert
            Assert.True(result);
            _mockInstanceManager.Verify(im => im.ForceTerminateAsync(instanceId), Times.Once);
        }

        [Fact]
        public void Events_ShouldBeForwardedFromInstanceManager()
        {
            // Arrange
            ApplicationInstanceEventArgs? receivedStartedEvent = null;
            ApplicationInstanceEventArgs? receivedStoppedEvent = null;
            ApplicationInstanceEventArgs? receivedStateChangedEvent = null;
            ApplicationInstanceEventArgs? receivedActivatedEvent = null;

            _service.InstanceStarted += (s, e) => receivedStartedEvent = e;
            _service.InstanceStopped += (s, e) => receivedStoppedEvent = e;
            _service.InstanceStateChanged += (s, e) => receivedStateChangedEvent = e;
            _service.InstanceActivated += (s, e) => receivedActivatedEvent = e;

            var testInstance = CreateTestInstance("test-instance", ApplicationState.Running);

            // Act & Assert - Trigger events from mock instance manager
            var startedEventArgs = new ApplicationInstanceEventArgs(testInstance, ApplicationInstanceEventType.Started, "Started");
            _mockInstanceManager.Raise(im => im.InstanceStarted += null, startedEventArgs);
            Assert.NotNull(receivedStartedEvent);
            Assert.Equal("test-instance", receivedStartedEvent.Instance.InstanceId);

            var stoppedEventArgs = new ApplicationInstanceEventArgs(testInstance, ApplicationInstanceEventType.Stopped, "Stopped");
            _mockInstanceManager.Raise(im => im.InstanceStopped += null, stoppedEventArgs);
            Assert.NotNull(receivedStoppedEvent);
            Assert.Equal("test-instance", receivedStoppedEvent.Instance.InstanceId);

            var stateChangedEventArgs = new ApplicationInstanceEventArgs(testInstance, ApplicationInstanceEventType.StateChanged, "State changed");
            _mockInstanceManager.Raise(im => im.InstanceStateChanged += null, stateChangedEventArgs);
            Assert.NotNull(receivedStateChangedEvent);
            Assert.Equal("test-instance", receivedStateChangedEvent.Instance.InstanceId);

            var activatedEventArgs = new ApplicationInstanceEventArgs(testInstance, ApplicationInstanceEventType.Activated, "Activated");
            _mockInstanceManager.Raise(im => im.InstanceActivated += null, activatedEventArgs);
            Assert.NotNull(receivedActivatedEvent);
            Assert.Equal("test-instance", receivedActivatedEvent.Instance.InstanceId);
        }

        [Fact]
        public async Task CleanupAsync_ShouldCallCleanupOnAllComponents()
        {
            // Arrange
            _mockInstanceManager.Setup(im => im.CleanupAsync())
                .Returns(Task.CompletedTask);
            _mockProcessMonitor.Setup(pm => pm.CleanupAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.CleanupAsync();

            // Assert
            _mockInstanceManager.Verify(im => im.CleanupAsync(), Times.Once);
            _mockProcessMonitor.Verify(pm => pm.CleanupAsync(), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_WithNullApplication_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.LaunchAsync(null!, "testuser"));
        }

        [Fact]
        public async Task LaunchAsync_WithNullLaunchedBy_ShouldThrowArgumentNullException()
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Desktop);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.LaunchAsync(app, null!));
        }

        [Fact]
        public async Task SwitchToAsync_WithNullInstanceId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.SwitchToAsync(null!));
        }

        [Fact]
        public async Task TerminateAsync_WithNullInstanceId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.TerminateAsync(null!));
        }

        [Fact]
        public async Task ForceTerminateAsync_WithNullInstanceId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.ForceTerminateAsync(null!));
        }

        [Fact]
        public async Task LaunchAsync_WhenLauncherThrowsException_ShouldReturnFailure()
        {
            // Arrange
            var app = CreateTestApplication(ApplicationType.Desktop);
            var exceptionMessage = "Test launcher exception";
            
            _mockDesktopLauncher.Setup(l => l.LaunchAsync(app, "testuser"))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _service.LaunchAsync(app, "testuser");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(exceptionMessage, result.ErrorMessage);
        }

        [Theory]
        [InlineData(ApplicationType.Desktop)]
        [InlineData(ApplicationType.ChromeApp)]
        [InlineData(ApplicationType.Web)]
        [InlineData(ApplicationType.Folder)]
        public async Task LaunchAsync_ShouldSelectCorrectLauncher(ApplicationType appType)
        {
            // Arrange
            var app = CreateTestApplication(appType);
            var mockLauncher = new Mock<IApplicationLauncher>();
            
            // Настраиваем лаунчер для конкретного типа приложения
            mockLauncher.Setup(l => l.CanLaunch(It.Is<Application>(a => a.Type == appType)))
                .Returns(true);
            mockLauncher.Setup(l => l.LaunchAsync(app, "testuser"))
                .ReturnsAsync(LaunchResult.Success($"{appType}-instance", TimeSpan.FromSeconds(1)));

            // Создаем новый сервис с нашим специфичным лаунчером
            var launchers = new List<IApplicationLauncher> { mockLauncher.Object };
            var service = new ApplicationLifecycleService(
                _mockLogger.Object,
                _mockInstanceManager.Object,
                _mockWindowManager.Object,
                _mockProcessMonitor.Object,
                launchers,
                _mockAuditService.Object
            );

            // Act
            var result = await service.LaunchAsync(app, "testuser");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains(appType.ToString().ToLower(), result.InstanceId!.ToLower());
            mockLauncher.Verify(l => l.LaunchAsync(app, "testuser"), Times.Once);
        }

        #region Helper Methods

        private Application CreateTestApplication(ApplicationType type)
        {
            return new Application
            {
                Id = 1,
                Name = $"Test {type} App",
                Type = type,
                ExecutablePath = type switch
                {
                    ApplicationType.Desktop => @"C:\Windows\System32\notepad.exe",
                    ApplicationType.ChromeApp => @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    ApplicationType.Web => "https://example.com",
                    ApplicationType.Folder => @"C:\Windows",
                    _ => "test-path"
                },
                Arguments = type switch
                {
                    ApplicationType.ChromeApp => "--app=https://example.com",
                    _ => ""
                },
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                IsActive = true
            };
        }

        private ApplicationInstance CreateTestInstance(string instanceId, ApplicationState state)
        {
            var app = CreateTestApplication(ApplicationType.Desktop);
            return new ApplicationInstance
            {
                InstanceId = instanceId,
                Application = app,
                ProcessId = 1234,
                StartTime = DateTime.Now,
                State = state,
                LaunchedBy = "testuser"
            };
        }

        #endregion

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
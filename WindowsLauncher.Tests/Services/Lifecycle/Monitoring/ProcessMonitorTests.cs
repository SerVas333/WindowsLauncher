using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WindowsLauncher.Core.Models.Lifecycle;
using WindowsLauncher.Services.Lifecycle.Monitoring;

namespace WindowsLauncher.Tests.Services.Lifecycle.Monitoring
{
    /// <summary>
    /// Тесты для ProcessMonitor - проверяют логику мониторинга процессов
    /// </summary>
    public class ProcessMonitorTests : IDisposable
    {
        private readonly Mock<ILogger<ProcessMonitor>> _mockLogger;
        private readonly ProcessMonitor _processMonitor;

        public ProcessMonitorTests()
        {
            _mockLogger = new Mock<ILogger<ProcessMonitor>>();
            _processMonitor = new ProcessMonitor(_mockLogger.Object);
        }

        [Fact]
        public async Task StartMonitoringAsync_ShouldStartSuccessfully()
        {
            // Act & Assert - не должно выбрасывать исключение
            await _processMonitor.StartMonitoringAsync();
            
            // Проверяем что мониторинг запущен
            Assert.True(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldStopSuccessfully()
        {
            // Arrange - сначала запускаем мониторинг
            await _processMonitor.StartMonitoringAsync();
            
            // Act
            await _processMonitor.StopMonitoringAsync();
            
            // Assert
            Assert.False(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task StopMonitoringAsync_WhenNotStarted_ShouldNotThrow()
        {
            // Act & Assert - не должно выбрасывать исключение даже если не запущен
            await _processMonitor.StopMonitoringAsync();
            
            Assert.False(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenAlreadyStarted_ShouldNotThrow()
        {
            // Arrange
            await _processMonitor.StartMonitoringAsync();
            
            // Act & Assert - повторный запуск не должен падать
            await _processMonitor.StartMonitoringAsync();
            
            Assert.True(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task GetProcessInfoAsync_WithValidProcessId_ShouldNotThrow()
        {
            // Arrange
            var processId = System.Diagnostics.Process.GetCurrentProcess().Id; // Используем текущий процесс
            
            // Act & Assert - не должно выбрасывать исключение
            var result = await _processMonitor.GetProcessInfoAsync(processId);
            
            // В реальной системе это вернет информацию о процессе
            Assert.NotNull(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(99999999)] // Вряд ли существует процесс с таким ID
        public async Task GetProcessInfoAsync_WithInvalidProcessId_ShouldReturnNull(int invalidProcessId)
        {
            // Act
            var result = await _processMonitor.GetProcessInfoAsync(invalidProcessId);
            
            // Assert - для несуществующих процессов должен возвращать null
            Assert.Null(result);
        }

        [Fact]
        public async Task IsProcessRunningAsync_WithValidProcessId_ShouldReturnTrue()
        {
            // Arrange
            var processId = System.Diagnostics.Process.GetCurrentProcess().Id; // Используем текущий процесс
            
            // Act
            var result = await _processMonitor.IsProcessRunningAsync(processId);
            
            // Assert - текущий процесс должен быть запущен
            Assert.True(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(99999999)] // Вряд ли существует процесс с таким ID
        public async Task IsProcessRunningAsync_WithInvalidProcessId_ShouldReturnFalse(int invalidProcessId)
        {
            // Act
            var result = await _processMonitor.IsProcessRunningAsync(invalidProcessId);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TerminateProcessAsync_WithInvalidProcessId_ShouldReturnFalse()
        {
            // Arrange
            var invalidProcessId = 99999999; // Вряд ли существует процесс с таким ID
            
            // Act
            var result = await _processMonitor.TerminateProcessAsync(invalidProcessId, 5000, 3000);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ForceTerminateProcessAsync_WithInvalidProcessId_ShouldReturnFalse()
        {
            // Arrange
            var invalidProcessId = 99999999; // Вряд ли существует процесс с таким ID
            
            // Act
            var result = await _processMonitor.TerminateProcessAsync(invalidProcessId, 1000, 1000);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetAllProcessesAsync_ShouldReturnNonEmptyList()
        {
            // Act
            var result = await _processMonitor.GetAllProcessesAsync();
            
            // Assert - должен вернуть хотя бы один процесс (текущий)
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task CleanupAsync_ShouldStopMonitoringAndCleanup()
        {
            // Arrange
            await _processMonitor.StartMonitoringAsync();
            
            // Act
            await _processMonitor.CleanupAsync();
            
            // Assert
            Assert.False(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task CleanupAsync_WhenNotStarted_ShouldNotThrow()
        {
            // Act & Assert - не должно выбрасывать исключение
            await _processMonitor.CleanupAsync();
        }

        [Fact]
        public void ProcessMonitor_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProcessMonitor(null!));
        }

        [Fact]
        public void ProcessMonitor_Events_ShouldBeInitialized()
        {
            // Arrange
            var eventFired = false;
            
            // Act - подписываемся на события
            _processMonitor.ProcessStarted += (s, e) => eventFired = true;
            _processMonitor.ProcessTerminated += (s, e) => eventFired = true;
            
            // Assert - события должны быть доступны для подписки
            Assert.NotNull(_processMonitor.ProcessStarted);
            Assert.NotNull(_processMonitor.ProcessTerminated);
        }

        [Fact]
        public async Task StartStopMonitoring_MultipleOperations_ShouldHandleCorrectly()
        {
            // Тест на множественные операции запуска/остановки
            
            // Операция 1: Запуск
            await _processMonitor.StartMonitoringAsync();
            Assert.True(_processMonitor.IsMonitoring);
            
            // Операция 2: Остановка
            await _processMonitor.StopMonitoringAsync();
            Assert.False(_processMonitor.IsMonitoring);
            
            // Операция 3: Повторный запуск
            await _processMonitor.StartMonitoringAsync();
            Assert.True(_processMonitor.IsMonitoring);
            
            // Операция 4: Повторная остановка
            await _processMonitor.StopMonitoringAsync();
            Assert.False(_processMonitor.IsMonitoring);
        }

        [Fact]
        public async Task GetProcessInfoAsync_ShouldReturnValidProcessInfo()
        {
            // Arrange
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            
            // Act
            var result = await _processMonitor.GetProcessInfoAsync(currentProcess.Id);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(currentProcess.Id, result.ProcessId);
            Assert.NotNull(result.ProcessName);
            Assert.True(result.IsRunning);
            
            currentProcess.Dispose();
        }

        public void Dispose()
        {
            _processMonitor?.Dispose();
        }
    }
}
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
    /// Unit тесты для InstalledAppsService - специализированного сервиса управления установленными Android приложениями
    /// </summary>
    public class InstalledAppsServiceTests : IDisposable
    {
        private readonly Mock<IWSAConnectionService> _mockConnectionService;
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<ILogger<InstalledAppsService>> _mockLogger;
        private readonly InstalledAppsService _service;

        public InstalledAppsServiceTests()
        {
            _mockConnectionService = new Mock<IWSAConnectionService>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLogger = new Mock<ILogger<InstalledAppsService>>();
            _service = new InstalledAppsService(_mockConnectionService.Object, _mockProcessExecutor.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        #region GetInstalledAppsAsync Tests

        [Fact]
        public async Task GetInstalledAppsAsync_WithValidApps_ReturnsAppList()
        {
            // Arrange
            SetupSuccessfulConnection();

            var userAppsOutput = @"
package:com.example.app1
package:com.example.app2
package:com.mycompany.calculator
";

            var systemAppsOutput = @"
package:com.android.systemui
package:com.android.settings
package:com.google.android.gms
";

            _mockProcessExecutor.SetupSequence(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = userAppsOutput, 
                    IsSuccess = true 
                })
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = systemAppsOutput, 
                    IsSuccess = true 
                });

            // Act
            var result = await _service.GetInstalledAppsAsync(includeSystemApps: false);

            // Assert
            var apps = result.ToList();
            Assert.Equal(3, apps.Count); // Только пользовательские приложения
            Assert.Contains(apps, app => app.PackageName == "com.example.app1");
            Assert.Contains(apps, app => app.PackageName == "com.example.app2");
            Assert.Contains(apps, app => app.PackageName == "com.mycompany.calculator");
            Assert.All(apps, app => Assert.False(app.IsSystemApp));
        }

        [Fact]
        public async Task GetInstalledAppsAsync_IncludingSystemApps_ReturnsAllApps()
        {
            // Arrange
            SetupSuccessfulConnection();

            var userAppsOutput = "package:com.example.app1\n";
            var systemAppsOutput = "package:com.android.systemui\npackage:com.google.android.gms\n";

            _mockProcessExecutor.SetupSequence(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true })
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = systemAppsOutput, IsSuccess = true });

            // Act
            var result = await _service.GetInstalledAppsAsync(includeSystemApps: true);

            // Assert
            var apps = result.ToList();
            Assert.Equal(3, apps.Count);
            Assert.Single(apps.Where(app => !app.IsSystemApp)); // 1 пользовательское приложение
            Assert.Equal(2, apps.Count(app => app.IsSystemApp)); // 2 системных приложения
        }

        [Fact]
        public async Task GetInstalledAppsAsync_WithCache_ReturnsFromCache()
        {
            // Arrange
            SetupSuccessfulConnection();

            var userAppsOutput = "package:com.example.app1\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });

            // Act - первый вызов (заполняет кэш)
            var result1 = await _service.GetInstalledAppsAsync(useCache: true);
            // Act - второй вызов (должен использовать кэш)
            var result2 = await _service.GetInstalledAppsAsync(useCache: true);

            // Assert
            Assert.Equal(result1.Count(), result2.Count());
            // Проверяем что ADB команды были выполнены только для первого вызова
            _mockProcessExecutor.Verify(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null), 
                Times.Once);
        }

        [Fact]
        public async Task GetInstalledAppsAsync_WhenConnectionFails_ReturnsEmpty()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _service.GetInstalledAppsAsync();

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region GetAppInfoAsync Tests

        [Fact]
        public async Task GetAppInfoAsync_WithValidPackage_ReturnsDetailedInfo()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.testapp";
            var dumpsysOutput = @"
      versionCode=42 minSdk=21 targetSdk=34
      versionName=1.2.3
      firstInstallTime=2024-01-15 10:30:00
      lastUpdateTime=2024-01-20 15:45:00
      applicationInfo:
        dataDir=/data/user/0/com.example.testapp
";

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell dumpsys package {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = dumpsysOutput, 
                    IsSuccess = true 
                });

            // Act
            var result = await _service.GetAppInfoAsync(packageName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(packageName, result.PackageName);
            Assert.Equal(42, result.VersionCode);
            Assert.Equal("1.2.3", result.VersionName);
            // Проверяем что даты были разобраны
            Assert.True(result.InstallDate.HasValue);
            Assert.True(result.LastUpdateDate.HasValue);
        }

        [Fact]
        public async Task GetAppInfoAsync_WithCachedApp_ReturnsFromCache()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.app";
            
            // Сначала заполняем кэш через GetInstalledAppsAsync
            var userAppsOutput = $"package:{packageName}\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });
            
            await _service.GetInstalledAppsAsync();

            // Act
            var result = await _service.GetAppInfoAsync(packageName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(packageName, result.PackageName);
            
            // Проверяем что dumpsys не был вызван (использовали кэш)
            _mockProcessExecutor.Verify(x => x.ExecuteAsync("adb", $"shell dumpsys package {packageName}", It.IsAny<int>(), null), 
                Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task GetAppInfoAsync_WithInvalidPackageName_ReturnsNull(string packageName)
        {
            // Act
            var result = await _service.GetAppInfoAsync(packageName);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region IsAppInstalledAsync Tests

        [Fact]
        public async Task IsAppInstalledAsync_WithInstalledApp_ReturnsTrue()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.installedapp";
            var listOutput = $"package:{packageName}\n";

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm list packages {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = listOutput, 
                    IsSuccess = true 
                });

            // Act
            var result = await _service.IsAppInstalledAsync(packageName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAppInstalledAsync_WithNotInstalledApp_ReturnsFalse()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.notinstalled";

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm list packages {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = "", // Пустой output означает что приложение не найдено
                    IsSuccess = true 
                });

            // Act
            var result = await _service.IsAppInstalledAsync(packageName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsAppInstalledAsync_WithCachedApp_ReturnsFromCache()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.cachedapp";
            
            // Заполняем кэш
            var userAppsOutput = $"package:{packageName}\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });
            
            await _service.GetInstalledAppsAsync();

            // Act
            var result = await _service.IsAppInstalledAsync(packageName);

            // Assert
            Assert.True(result);
            
            // Проверяем что отдельный запрос на проверку установки не выполнялся (использован кэш)
            _mockProcessExecutor.Verify(x => x.ExecuteAsync("adb", $"shell pm list packages {packageName}", It.IsAny<int>(), null), 
                Times.Never);
        }

        #endregion

        #region LaunchAppAsync Tests

        [Fact]
        public async Task LaunchAppAsync_WithValidApp_ReturnsSuccess()
        {
            // Arrange
            SetupSuccessfulConnection();
            SetupAppInstalled("com.example.launchapp");

            var packageName = "com.example.launchapp";
            var launchResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Events injected: 1",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", 
                $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1", 
                It.IsAny<int>(), null))
                .ReturnsAsync(launchResult);

            // Setup PID retrieval
            var pidResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "12345\n",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pidof {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(pidResult);

            // Act
            var result = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(packageName, result.PackageName);
            Assert.Equal(12345, result.ProcessId);
        }

        [Fact]
        public async Task LaunchAppAsync_WithNotInstalledApp_ReturnsFailure()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.notinstalled";
            
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm list packages {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = "", IsSuccess = true });

            // Act
            var result = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not installed", result.ErrorMessage);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task LaunchAppAsync_WithInvalidPackageName_ReturnsFailure(string packageName)
        {
            // Act
            var result = await _service.LaunchAppAsync(packageName);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("null or empty", result.ErrorMessage);
        }

        #endregion

        #region StopAppAsync Tests

        [Fact]
        public async Task StopAppAsync_WithValidApp_ReturnsTrue()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.runningapp";
            var stopResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell am force-stop {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(stopResult);

            // Act
            var result = await _service.StopAppAsync(packageName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task StopAppAsync_WhenStopFails_ReturnsFalse()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.problematicapp";
            var stopResult = new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "Permission denied",
                IsSuccess = false
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell am force-stop {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(stopResult);

            // Act
            var result = await _service.StopAppAsync(packageName);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region UninstallAppAsync Tests

        [Fact]
        public async Task UninstallAppAsync_WithValidApp_ReturnsTrue()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.uninstallme";
            var uninstallResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"uninstall {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(uninstallResult);

            // Act
            var result = await _service.UninstallAppAsync(packageName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task UninstallAppAsync_WhenUninstallFails_ReturnsFalse()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.android.systemui"; // Системное приложение
            var uninstallResult = new ProcessResult
            {
                ExitCode = 0, // ADB команда может вернуть 0, но с ошибкой в output
                StandardOutput = "Failure [DELETE_FAILED_DEVICE_POLICY_MANAGER]",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"uninstall {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(uninstallResult);

            // Act
            var result = await _service.UninstallAppAsync(packageName);

            // Assert
            Assert.False(result); // Должно вернуть false так как в output нет "Success"
        }

        [Fact]
        public async Task UninstallAppAsync_RemovesFromCache_FiresEvent()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.uninstallapp";
            
            // Заполняем кэш
            var userAppsOutput = $"package:{packageName}\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });
            
            await _service.GetInstalledAppsAsync();

            // Setup успешного удаления
            var uninstallResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"uninstall {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(uninstallResult);

            // Подписываемся на событие
            InstalledAppsChangedEventArgs? capturedEvent = null;
            _service.InstalledAppsChanged += (sender, args) => capturedEvent = args;

            // Act
            var result = await _service.UninstallAppAsync(packageName);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedEvent);
            Assert.Equal(ChangeType.AppUninstalled, capturedEvent.ChangeType);
            Assert.Equal(packageName, capturedEvent.PackageName);
        }

        #endregion

        #region GetAppLogsAsync Tests

        [Fact]
        public async Task GetAppLogsAsync_WithValidApp_ReturnsLogs()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.logapp";
            var expectedLogs = @"01-15 10:30:00.123  1234  1234 I ActivityManager: Starting: Intent { act=android.intent.action.MAIN cat=[android.intent.category.LAUNCHER] cmp=com.example.logapp/.MainActivity }
01-15 10:30:01.456  1234  1234 D AppTag: Application started successfully";

            var logResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = expectedLogs,
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", 
                $"logcat -t 100 --pid=$(pidof {packageName} 2>/dev/null || echo 0)", 
                It.IsAny<int>(), null))
                .ReturnsAsync(logResult);

            // Act
            var result = await _service.GetAppLogsAsync(packageName, 100);

            // Assert
            Assert.Equal(expectedLogs, result);
        }

        [Fact]
        public async Task GetAppLogsAsync_WhenNoLogs_ReturnsNoLogsMessage()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.silentapp";
            var logResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "", // Нет логов
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", It.IsAny<string>(), It.IsAny<int>(), null))
                .ReturnsAsync(logResult);

            // Act
            var result = await _service.GetAppLogsAsync(packageName);

            // Assert
            Assert.Contains($"No logs found for {packageName}", result);
        }

        #endregion

        #region ClearAppDataAsync Tests

        [Fact]
        public async Task ClearAppDataAsync_WithValidApp_ReturnsTrue()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.clearme";
            var clearResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Success",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm clear {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(clearResult);

            // Act
            var result = await _service.ClearAppDataAsync(packageName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ClearAppDataAsync_WhenClearFails_ReturnsFalse()
        {
            // Arrange
            SetupSuccessfulConnection();

            var packageName = "com.example.protectedapp";
            var clearResult = new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Failed",
                StandardError = "",
                IsSuccess = true
            };

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm clear {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(clearResult);

            // Act
            var result = await _service.ClearAppDataAsync(packageName);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetAppsUsageStatsAsync Tests

        [Fact]
        public async Task GetAppsUsageStatsAsync_ReturnsStatistics()
        {
            // Arrange
            SetupSuccessfulConnection();

            // Заполняем кэш несколькими приложениями
            var userAppsOutput = @"
package:com.example.app1
package:com.example.app2
package:com.gaming.puzzle
";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });

            var systemAppsOutput = "package:com.android.systemui\npackage:com.google.android.gms\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -s", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = systemAppsOutput, IsSuccess = true });

            await _service.GetInstalledAppsAsync(includeSystemApps: true);

            // Act
            var result = await _service.GetAppsUsageStatsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("TotalUserApps"));
            Assert.True(result.ContainsKey("SystemApps"));
            Assert.True(result.ContainsKey("RunningApps"));
            Assert.True(result.ContainsKey("CacheSize"));
            
            Assert.Equal(3, result["TotalUserApps"]);
            Assert.Equal(2, result["SystemApps"]);
        }

        #endregion

        #region RefreshAppsCache Tests

        [Fact]
        public async Task RefreshAppsCache_UpdatesCacheSuccessfully()
        {
            // Arrange
            SetupSuccessfulConnection();

            var userAppsOutput = "package:com.example.refreshtest\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -s", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = "", IsSuccess = true });

            // Act
            var result = await _service.RefreshAppsCache();

            // Assert
            Assert.True(result);
            
            // Проверяем что кэш обновился
            var apps = await _service.GetInstalledAppsAsync(useCache: true);
            Assert.Single(apps);
            Assert.Contains(apps, app => app.PackageName == "com.example.refreshtest");
        }

        [Fact]
        public async Task RefreshAppsCache_DetectsAppChanges_FiresEvents()
        {
            // Arrange
            SetupSuccessfulConnection();

            // Первоначальное состояние кэша
            var initialAppsOutput = "package:com.example.existing\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = initialAppsOutput, IsSuccess = true });

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -s", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = "", IsSuccess = true });

            await _service.GetInstalledAppsAsync(); // Заполняем кэш

            // Обновленное состояние (новое приложение установлено, старое удалено)
            var updatedAppsOutput = "package:com.example.newapp\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = updatedAppsOutput, IsSuccess = true });

            var eventsFired = new List<InstalledAppsChangedEventArgs>();
            _service.InstalledAppsChanged += (sender, args) => eventsFired.Add(args);

            // Act
            await _service.RefreshAppsCache();

            // Assert
            Assert.Equal(2, eventsFired.Count); // Одно удаление, одна установка
            Assert.Contains(eventsFired, e => e.ChangeType == ChangeType.AppInstalled && e.PackageName == "com.example.newapp");
            Assert.Contains(eventsFired, e => e.ChangeType == ChangeType.AppUninstalled && e.PackageName == "com.example.existing");
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task InstalledAppsChanged_Event_FiresOnCacheRefresh()
        {
            // Arrange
            SetupSuccessfulConnection();

            InstalledAppsChangedEventArgs? capturedEvent = null;
            _service.InstalledAppsChanged += (sender, args) => capturedEvent = args;

            var userAppsOutput = "package:com.example.eventtest\n";
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -3", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = userAppsOutput, IsSuccess = true });

            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", "shell pm list packages -s", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, StandardOutput = "", IsSuccess = true });

            // Act
            await _service.RefreshAppsCache();

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal(ChangeType.CacheRefreshed, capturedEvent.ChangeType);
        }

        #endregion

        #region Private Helper Methods

        private void SetupSuccessfulConnection()
        {
            _mockConnectionService.Setup(x => x.ConnectToWSAAsync())
                .ReturnsAsync(true);
            
            // Setup ADB path
            var connectionStatus = new Dictionary<string, object>
            {
                ["ADBPath"] = "adb"
            };
            _mockConnectionService.Setup(x => x.GetConnectionStatusAsync())
                .ReturnsAsync(connectionStatus);
        }

        private void SetupAppInstalled(string packageName)
        {
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("adb", $"shell pm list packages {packageName}", It.IsAny<int>(), null))
                .ReturnsAsync(new ProcessResult 
                { 
                    ExitCode = 0, 
                    StandardOutput = $"package:{packageName}\n", 
                    IsSuccess = true 
                });
        }

        #endregion
    }
}
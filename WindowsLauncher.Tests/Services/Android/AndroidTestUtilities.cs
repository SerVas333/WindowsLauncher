using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Core.Models.Android;

namespace WindowsLauncher.Tests.Services.Android
{
    /// <summary>
    /// Утилиты для тестирования Android-подсистемы на Windows
    /// </summary>
    public static class AndroidTestUtilities
    {
        #region Windows Environment Detection

        /// <summary>
        /// Проверяет что тесты запущены на Windows (обязательно для WPF проектов)
        /// </summary>
        public static bool IsRunningOnWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        /// <summary>
        /// Пропускает тест если он запущен не на Windows
        /// </summary>
        public static void SkipIfNotWindows()
        {
            if (!IsRunningOnWindows())
            {
                throw new SkipException("This test requires Windows environment for WPF compatibility");
            }
        }

        #endregion

        #region Mock Creation Helpers

        /// <summary>
        /// Создает базовые моки для WSAConnectionService с успешными результатами
        /// </summary>
        public static Mock<IWSAConnectionService> CreateSuccessfulConnectionServiceMock()
        {
            var mock = new Mock<IWSAConnectionService>();
            
            mock.Setup(x => x.IsWSAAvailableAsync()).ReturnsAsync(true);
            mock.Setup(x => x.IsWSARunningAsync()).ReturnsAsync(true);
            mock.Setup(x => x.IsAdbAvailableAsync()).ReturnsAsync(true);
            mock.Setup(x => x.ConnectToWSAAsync()).ReturnsAsync(true);
            mock.Setup(x => x.GetAndroidVersionAsync()).ReturnsAsync("13");
            
            var connectionStatus = new Dictionary<string, object>
            {
                ["WSAAvailable"] = true,
                ["WSARunning"] = true,
                ["ADBAvailable"] = true,
                ["ADBConnected"] = true,
                ["AndroidVersion"] = "13",
                ["ADBPath"] = "adb"
            };
            mock.Setup(x => x.GetConnectionStatusAsync()).ReturnsAsync(connectionStatus);

            return mock;
        }

        /// <summary>
        /// Создает моки для ProcessExecutor с предустановленными Windows-совместимыми ответами
        /// </summary>
        public static Mock<IProcessExecutor> CreateProcessExecutorMock()
        {
            var mock = new Mock<IProcessExecutor>();

            // Windows PowerShell команды для WSA
            mock.Setup(x => x.ExecutePowerShellAsync(
                    "Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid",
                    It.IsAny<int>()))
                .ReturnsAsync(ProcessResult.Success("Name: Windows Subsystem for Android\nVersion: 2301.40000.4.0"));

            // Windows процессы WSA
            mock.Setup(x => x.ExecutePowerShellAsync(
                    It.Is<string>(s => s.Contains("Get-Process") && s.Contains("WSA")),
                    It.IsAny<int>()))
                .ReturnsAsync(ProcessResult.Success("WsaService\nWsaClient"));

            // ADB команды
            mock.Setup(x => x.IsCommandAvailableAsync("adb")).ReturnsAsync(true);
            mock.Setup(x => x.ExecuteAsync("adb", "version", It.IsAny<int>(), null))
                .ReturnsAsync(ProcessResult.Success("Android Debug Bridge version 1.0.41"));

            // AAPT команды
            mock.Setup(x => x.IsCommandAvailableAsync("aapt")).ReturnsAsync(true);

            return mock;
        }

        /// <summary>
        /// Создает Mock для ILogger с возможностью проверки логирования
        /// </summary>
        public static Mock<ILogger<T>> CreateLoggerMock<T>()
        {
            return new Mock<ILogger<T>>();
        }

        #endregion

        #region Windows File System Helpers

        /// <summary>
        /// Создает временную директорию для тестов в Windows Temp
        /// </summary>
        public static string CreateTempDirectory()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "WindowsLauncherTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Очищает временную директорию (безопасно для Windows)
        /// </summary>
        public static void CleanupTempDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    // Windows требует особого подхода к удалению файлов
                    SetDirectoryAccessible(path);
                    Directory.Delete(path, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    // Повторная попытка после короткой задержки (Windows file locking)
                    Thread.Sleep(100);
                    try
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    catch
                    {
                        // Игнорируем ошибки очистки в тестах
                    }
                }
                catch
                {
                    // Игнорируем другие ошибки очистки
                }
            }
        }

        /// <summary>
        /// Устанавливает атрибуты директории для безопасного удаления в Windows
        /// </summary>
        private static void SetDirectoryAccessible(string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                dirInfo.Attributes = FileAttributes.Normal;

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
            }
            catch
            {
                // Игнорируем ошибки изменения атрибутов
            }
        }

        /// <summary>
        /// Создает mock APK файл с правильной структурой для Windows
        /// </summary>
        public static string CreateMockApkFile(string directory, string fileName = "test.apk", long size = 1024)
        {
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, fileName);
            
            var content = new byte[size];
            // Заполняем данными, имитирующими структуру APK (ZIP archive)
            content[0] = 0x50; // ZIP signature "PK"
            content[1] = 0x4B;
            content[2] = 0x03;
            content[3] = 0x04;
            
            File.WriteAllBytes(filePath, content);
            return filePath;
        }

        /// <summary>
        /// Создает mock XAPK файл с правильной структурой ZIP архива для Windows
        /// </summary>
        public static string CreateMockXapkFile(string directory, string fileName = "test.xapk")
        {
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            // Добавляем manifest.json
            var manifestEntry = archive.CreateEntry("manifest.json");
            using var manifestStream = manifestEntry.Open();
            var manifestContent = @"{
                ""xapk_version"": 2,
                ""package_name"": ""com.example.testapp"",
                ""name"": ""Test XAPK App"",
                ""version_code"": 100,
                ""version_name"": ""2.0.0"",
                ""min_sdk_version"": 21,
                ""target_sdk_version"": 33
            }";
            var manifestBytes = Encoding.UTF8.GetBytes(manifestContent);
            manifestStream.Write(manifestBytes, 0, manifestBytes.Length);

            // Добавляем base APK
            var baseApkEntry = archive.CreateEntry("base.apk");
            using var baseApkStream = baseApkEntry.Open();
            var apkContent = new byte[2048];
            new Random().NextBytes(apkContent);
            baseApkStream.Write(apkContent, 0, apkContent.Length);

            // Добавляем split APK для архитектуры
            var splitEntry = archive.CreateEntry("config.arm64_v8a.apk");
            using var splitStream = splitEntry.Open();
            splitStream.Write(apkContent, 0, 1024); // Меньший размер для split

            return filePath;
        }

        #endregion

        #region Test Data Factories

        /// <summary>
        /// Создает тестовые метаданные APK
        /// </summary>
        public static ApkMetadata CreateTestApkMetadata(
            string packageName = "com.example.testapp",
            string appName = "Test Application",
            string versionName = "1.0.0",
            int versionCode = 1,
            int minSdkVersion = 21,
            int targetSdkVersion = 33)
        {
            return new ApkMetadata
            {
                PackageName = packageName,
                AppName = appName,
                VersionName = versionName,
                VersionCode = versionCode,
                MinSdkVersion = minSdkVersion,
                TargetSdkVersion = targetSdkVersion
            };
        }

        /// <summary>
        /// Создает тестовое установленное Android приложение
        /// </summary>
        public static InstalledAndroidApp CreateTestInstalledApp(
            string packageName = "com.example.installedapp",
            string appName = "Installed App",
            bool isSystemApp = false,
            bool isRunning = false)
        {
            return new InstalledAndroidApp
            {
                PackageName = packageName,
                AppName = appName,
                IsSystemApp = isSystemApp,
                IsRunning = isRunning,
                IsEnabled = true,
                InstalledAt = DateTime.Now.AddDays(-7), // Неделю назад
                LastUsedAt = isRunning ? DateTime.Now.AddMinutes(-30) : null,
                VersionName = "1.0.0",
                VersionCode = 1
            };
        }

        /// <summary>
        /// Создает успешный результат установки APK
        /// </summary>
        public static ApkInstallResult CreateSuccessfulInstallResult(
            string packageName = "com.example.testapp",
            InstallationMethod method = InstallationMethod.Standard)
        {
            var result = ApkInstallResult.CreateSuccess(packageName, 1024 * 1024); // 1MB
            result.InstallationMethod = method;
            result.InstalledPackages = new List<string> { packageName };
            result.InstallDurationMs = 5000;
            return result;
        }

        /// <summary>
        /// Создает результат неудачной установки APK
        /// </summary>
        public static ApkInstallResult CreateFailedInstallResult(
            string errorMessage = "Installation failed",
            string packageName = "com.example.failedapp")
        {
            var result = ApkInstallResult.CreateFailure(errorMessage, 1);
            result.PackageName = packageName;
            result.InstallationMethod = InstallationMethod.Unknown;
            return result;
        }

        /// <summary>
        /// Создает успешный результат запуска приложения
        /// </summary>
        public static AppLaunchResult CreateSuccessfulLaunchResult(
            string packageName = "com.example.testapp",
            int? processId = 12345)
        {
            return AppLaunchResult.CreateSuccess(packageName, processId);
        }

        /// <summary>
        /// Создает результат неудачного запуска приложения
        /// </summary>
        public static AppLaunchResult CreateFailedLaunchResult(
            string packageName = "com.example.testapp",
            string errorMessage = "Launch failed")
        {
            return AppLaunchResult.CreateFailure(packageName, errorMessage);
        }

        /// <summary>
        /// Создает информацию о APK файле
        /// </summary>
        public static ApkFileInfo CreateTestApkFileInfo(
            string filePath = "C:\\test\\app.apk",
            long sizeBytes = 1024 * 1024,
            bool isXapk = false)
        {
            return new ApkFileInfo
            {
                FilePath = filePath,
                SizeBytes = sizeBytes,
                FileHash = "abc123",
                LastModified = DateTime.Now.AddDays(-1),
                IsXapk = isXapk
            };
        }

        /// <summary>
        /// Создает ProcessResult для успешных команд Windows
        /// </summary>
        public static ProcessResult CreateSuccessfulProcessResult(string output = "Success")
        {
            return ProcessResult.Success(output);
        }

        /// <summary>
        /// Создает ProcessResult для неудачных команд Windows
        /// </summary>
        public static ProcessResult CreateFailedProcessResult(
            string errorMessage = "Command failed",
            int exitCode = 1)
        {
            return ProcessResult.Failure(exitCode, errorMessage);
        }

        #endregion

        #region AAPT Output Generators (Windows-specific)

        /// <summary>
        /// Генерирует реалистичный вывод AAPT для Windows тестирования
        /// </summary>
        public static string GenerateAaptOutput(
            string packageName = "com.example.testapp",
            string appName = "Test Application",
            string versionName = "1.0.0",
            int versionCode = 1,
            int minSdkVersion = 21,
            int targetSdkVersion = 33)
        {
            return $@"package: name='{packageName}' versionCode='{versionCode}' versionName='{versionName}' platformBuildVersionName='{targetSdkVersion}' platformBuildVersionCode='{targetSdkVersion}' compileSdkVersion='{targetSdkVersion}' compileSdkVersionCodename='{targetSdkVersion}'
sdkVersion:'{minSdkVersion}'
targetSdkVersion:'{targetSdkVersion}'
application-label:'{appName}'
application-label-en:'{appName}'
application-icon-160:'res/drawable/ic_launcher.png'
application: label='{appName}' icon='res/drawable/ic_launcher.png'
launchable-activity: name='{packageName}.MainActivity'  label='{appName}' icon=''
feature-group: label=''
  uses-feature: name='android.hardware.screen.portrait'
  uses-feature: name='android.hardware.faketouch'
main
supports-screens: 'small' 'normal' 'large' 'xlarge'
supports-any-density: 'true'
locales: '--_--'
densities: '160' '240' '320' '480' '640'";
        }

        /// <summary>
        /// Генерирует вывод ADB для списка пакетов Android приложений
        /// </summary>
        public static string GenerateAdbPackagesOutput(params string[] packageNames)
        {
            var sb = new StringBuilder();
            foreach (var packageName in packageNames)
            {
                sb.AppendLine($"package:{packageName}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Генерирует вывод dumpsys для детальной информации о пакете
        /// </summary>
        public static string GenerateDumpsysOutput(
            string packageName = "com.example.testapp",
            string versionName = "1.0.0",
            int versionCode = 1)
        {
            var installTime = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss");
            var updateTime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss");

            return $@"Packages:
  Package [{packageName}] ({Guid.NewGuid()}):
    userId=10123
    pkg=Package{{...}}
    codePath=/data/app/{packageName}
    resourcePath=/data/app/{packageName}
    legacyNativeLibraryDir=/data/app/{packageName}/lib
    primaryCpuAbi=arm64-v8a
    secondaryCpuAbi=null
    versionCode={versionCode} minSdk=21 targetSdk=33
    versionName={versionName}
    splits=[base]
    apkSigningVersion=2
    applicationInfo=ApplicationInfo{{...}}
    flags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA ALLOW_BACKUP ]
    privateFlags=[ PRIVATE_FLAG_ACTIVITIES_RESIZE_MODE_RESIZEABLE ]
    dataDir=/data/user/0/{packageName}
    supportsScreens=[small, normal, large, xlarge, resizeable, anyDensity]
    timeStamp={installTime}
    firstInstallTime={installTime}
    lastUpdateTime={updateTime}
    installerPackageName=null";
        }

        #endregion

        #region Windows-Specific Test Attributes

        /// <summary>
        /// Атрибут для пропуска тестов на не-Windows платформах
        /// </summary>
        public class WindowsOnlyFactAttribute : FactAttribute
        {
            public WindowsOnlyFactAttribute()
            {
                if (!IsRunningOnWindows())
                {
                    Skip = "This test requires Windows environment for WPF compatibility";
                }
            }
        }

        /// <summary>
        /// Атрибут для теории, которая выполняется только на Windows
        /// </summary>
        public class WindowsOnlyTheoryAttribute : TheoryAttribute
        {
            public WindowsOnlyTheoryAttribute()
            {
                if (!IsRunningOnWindows())
                {
                    Skip = "This test requires Windows environment for WPF compatibility";
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Исключение для пропуска тестов (аналог xUnit Skip)
    /// </summary>
    public class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}

namespace WindowsLauncher.Tests.Services.Android.Fixtures
{
    /// <summary>
    /// Базовый класс для тестовых фикстур Android сервисов на Windows
    /// </summary>
    public abstract class AndroidServiceTestsBase : IDisposable
    {
        protected string TempDirectory { get; private set; }

        protected AndroidServiceTestsBase()
        {
            // Проверяем что мы на Windows
            AndroidTestUtilities.SkipIfNotWindows();
            
            // Создаем временную директорию для тестов
            TempDirectory = AndroidTestUtilities.CreateTempDirectory();
        }

        public virtual void Dispose()
        {
            // Очищаем временную директорию
            AndroidTestUtilities.CleanupTempDirectory(TempDirectory);
        }

        /// <summary>
        /// Создает временный APK файл для тестирования
        /// </summary>
        protected string CreateTestApkFile(string fileName = "test.apk")
        {
            return AndroidTestUtilities.CreateMockApkFile(TempDirectory, fileName);
        }

        /// <summary>
        /// Создает временный XAPK файл для тестирования
        /// </summary>
        protected string CreateTestXapkFile(string fileName = "test.xapk")
        {
            return AndroidTestUtilities.CreateMockXapkFile(TempDirectory, fileName);
        }
    }

    /// <summary>
    /// Collection фикстура для совместного использования ресурсов между тестами
    /// </summary>
    [CollectionDefinition("AndroidServices")]
    public class AndroidServicesCollection : ICollectionFixture<AndroidServicesFixture>
    {
        // Этот класс не содержит кода, он просто является точкой определения коллекции
    }

    /// <summary>
    /// Фикстура для Android сервисов, разделяемая между тестами
    /// </summary>
    public class AndroidServicesFixture : IDisposable
    {
        public string SharedTempDirectory { get; private set; }

        public AndroidServicesFixture()
        {
            AndroidTestUtilities.SkipIfNotWindows();
            SharedTempDirectory = AndroidTestUtilities.CreateTempDirectory();
        }

        public void Dispose()
        {
            AndroidTestUtilities.CleanupTempDirectory(SharedTempDirectory);
        }

        /// <summary>
        /// Создает общие тестовые файлы для использования в тестах
        /// </summary>
        public void SetupSharedTestFiles()
        {
            // Создаем несколько стандартных тестовых файлов
            AndroidTestUtilities.CreateMockApkFile(SharedTempDirectory, "shared-test.apk", 2048);
            AndroidTestUtilities.CreateMockXapkFile(SharedTempDirectory, "shared-test.xapk");
        }
    }
}
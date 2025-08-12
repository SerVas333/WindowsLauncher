# Android Services Unit Tests

Комплексные unit тесты для новой микросервисной архитектуры Android-подсистемы WindowsLauncher.

## 🏗️ Архитектура тестирования

### Структура тестов

```
WindowsLauncher.Tests/Services/Android/
├── WSAConnectionServiceTests.cs           # Unit тесты WSAConnectionService
├── ApkManagementServiceTests.cs          # Unit тесты ApkManagementService  
├── InstalledAppsServiceTests.cs          # Unit тесты InstalledAppsService
├── WSAIntegrationServiceIntegrationTests.cs # Integration тесты композитного сервиса
├── AndroidTestUtilities.cs               # Утилиты и хелперы для тестирования
├── AndroidServicesWindowsTests.cs        # Демонстрация использования утилит
└── README.md                             # Эта документация
```

### Типы тестов

1. **Unit Tests** - Тестируют отдельные сервисы в изоляции
2. **Integration Tests** - Проверяют взаимодействие между сервисами
3. **Windows Compatibility Tests** - Специальные тесты для WPF/Windows среды

## 🖥️ Windows/WPF Требования

### Системные требования
- **Платформа**: Windows 10/11 (обязательно)
- **Runtime**: .NET 8.0-windows
- **Тестовый фреймворк**: xUnit
- **Mocking**: Moq

### Запуск тестов

**Visual Studio 2022:**
```
Test → Run All Tests
```

**Command Line (.NET CLI):**
```powershell
# Из корня WindowsLauncher
dotnet test --logger "console;verbosity=detailed"

# Только Android тесты
dotnet test --filter "FullyQualifiedName~Android"

# Только Windows-совместимые тесты
dotnet test --filter "Category=WindowsOnly"
```

**PowerShell скрипт:**
```powershell
# Специальный скрипт для Windows тестирования
.\Scripts\Run-AndroidTests.ps1 -Verbose
```

## 🧪 Использование тестовых утилит

### AndroidTestUtilities

Центральный класс с утилитами для Android тестирования на Windows:

```csharp
// Проверка Windows окружения
AndroidTestUtilities.SkipIfNotWindows();

// Создание моков
var connectionMock = AndroidTestUtilities.CreateSuccessfulConnectionServiceMock();
var processMock = AndroidTestUtilities.CreateProcessExecutorMock();

// Создание тестовых файлов
var apkPath = AndroidTestUtilities.CreateMockApkFile(tempDir, "test.apk");
var xapkPath = AndroidTestUtilities.CreateMockXapkFile(tempDir, "test.xapk");

// Генерация тестовых данных
var metadata = AndroidTestUtilities.CreateTestApkMetadata("com.example.app");
var installResult = AndroidTestUtilities.CreateSuccessfulInstallResult();
```

### Windows-специфические атрибуты

```csharp
[AndroidTestUtilities.WindowsOnlyFact]
public void WindowsSpecificTest()
{
    // Этот тест выполнится только на Windows
}

[AndroidTestUtilities.WindowsOnlyTheory]
[InlineData("param1")]
public void WindowsParameterizedTest(string param)
{
    // Параметризованный тест только для Windows
}
```

### Базовые классы для тестов

```csharp
public class MyAndroidServiceTests : AndroidServiceTestsBase
{
    [Fact]
    public void MyTest()
    {
        // TempDirectory автоматически создается и очищается
        var testFile = CreateTestApkFile("my-test.apk");
        // ...тест логика...
    }
    
    // Dispose вызывается автоматически
}
```

## 📋 Покрытие тестами

### WSAConnectionService
- ✅ WSA availability detection
- ✅ WSA startup/shutdown
- ✅ ADB availability and connection
- ✅ Android version detection
- ✅ Intelligent caching (TTL-based)
- ✅ Connection status monitoring
- ✅ Event-driven notifications
- ✅ Retry mechanisms with exponential backoff

### ApkManagementService  
- ✅ APK/XAPK file validation
- ✅ Metadata extraction (AAPT integration)
- ✅ Multiple installation methods (Standard, Split, XAPK)
- ✅ Progress reporting
- ✅ Cancellation support
- ✅ WSA compatibility checks
- ✅ Fallback strategies
- ✅ File information extraction

### InstalledAppsService
- ✅ App inventory with caching
- ✅ Real-time app change detection  
- ✅ App lifecycle management (launch/stop/uninstall)
- ✅ Usage statistics
- ✅ Log retrieval
- ✅ Data clearing
- ✅ Event notifications
- ✅ Cache management

### WSAIntegrationService (Composite)
- ✅ Service orchestration
- ✅ Backward compatibility
- ✅ Enhanced methods delegation
- ✅ Event subscription patterns
- ✅ End-to-end workflows
- ✅ Error propagation
- ✅ Concurrent operations

## 🔧 Конфигурация тестов

### appsettings.Test.json
```json
{
  "Logging": {
    "LogLevel": {
      "WindowsLauncher.Services.Android": "Debug",
      "WindowsLauncher.Tests": "Information"
    }
  },
  "AndroidSubsystem": {
    "Mode": "OnDemand",
    "EnableDiagnostics": true
  }
}
```

### Переменные окружения для тестов
```powershell
# Принудительное включение Windows-режима
$env:WINDOWSLAUNCHER_FORCE_WINDOWS_TESTS = "true"

# Детальное логирование тестов
$env:WINDOWSLAUNCHER_TEST_LOGGING = "verbose"

# Использование реальных Android инструментов (если доступны)
$env:WINDOWSLAUNCHER_USE_REAL_TOOLS = "false"
```

## 🎯 Стратегии тестирования

### 1. Mocking стратегия
- **IProcessExecutor** - все внешние команды (ADB, AAPT, PowerShell)
- **IWSAConnectionService** - для изоляции сервисов
- **ILogger** - для проверки логирования

### 2. File System тестирование
- Временные директории в Windows Temp
- Безопасное создание/удаление файлов
- Правильная обработка Windows file locking

### 3. Windows-специфические сценарии
- PowerShell команды для WSA
- Windows процессы и сервисы
- Registry операции (при необходимости)
- Windows Firewall интеграция

### 4. Performance тестирование
- Кэширование эффективности
- Concurrent операции
- Memory usage patterns
- Resource cleanup

## 🚨 Troubleshooting

### Частые проблемы

**1. "Test requires Windows environment"**
```
Решение: Запускайте тесты только на Windows машинах
Причина: WPF требует Windows runtime
```

**2. "Access denied" при создании файлов**
```powershell
# Проверьте права доступа к Temp директории
$env:TEMP
icacls $env:TEMP
```

**3. "File is being used by another process"**
```
Решение: Убедитесь что Dispose() вызывается в тестах
Причина: Windows file locking механизм
```

**4. MockExecutor не настроен**
```csharp
// Используйте AndroidTestUtilities для создания моков
var mock = AndroidTestUtilities.CreateProcessExecutorMock();
```

### Debug режим

```csharp
// В тестах можно включить детальное логирование
[Fact]
public void TestWithDetailedLogging()
{
    // Создаем реальный logger вместо мока для отладки
    using var loggerFactory = LoggerFactory.Create(builder => 
        builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var logger = loggerFactory.CreateLogger<WSAConnectionService>();
    
    // Используем в тесте...
}
```

## 📊 Метрики и отчетность

### Code Coverage
```powershell
# Запуск с измерением покрытия
dotnet test --collect:"XPlat Code Coverage"

# Генерация HTML отчета
reportgenerator -reports:"**/*.xml" -targetdir:"TestResults/Coverage"
```

### Test Results
- **Test Explorer** в Visual Studio
- **Azure DevOps** интеграция
- **GitHub Actions** для CI/CD

### Performance Metrics
- Время выполнения тестов
- Memory consumption
- File I/O operations
- Mock verification counts

---

## 🏃 Быстрый старт

1. **Клонируйте и соберите проект:**
```powershell
git clone <repo>
cd WindowsLauncher
dotnet build
```

2. **Запустите Android тесты:**
```powershell
dotnet test --filter "FullyQualifiedName~Android"
```

3. **Изучите примеры:**
```
WindowsLauncher.Tests/Services/Android/AndroidServicesWindowsTests.cs
```

4. **Создайте свой тест:**
```csharp
public class MyServiceTests : AndroidServiceTestsBase
{
    [AndroidTestUtilities.WindowsOnlyFact]
    public void MyTest()
    {
        // Ваш тест здесь
    }
}
```

Тесты готовы к использованию в Windows/WPF среде! 🚀
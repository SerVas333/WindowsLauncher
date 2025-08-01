# WebView2 Implementation Guide

## Обзор WebView2 решения

Новая архитектура на базе Microsoft.Web.WebView2 заменяет проблематичный ChromeAppLauncher и предоставляет полный контроль над веб-приложениями.

## 🎯 Преимущества WebView2 над Chrome Apps

### Контроль жизненного цикла
- ✅ Полный контроль создания/закрытия окон
- ✅ Нет проблем с множественными процессами Chrome
- ✅ Встроенная поддержка событий жизненного цикла
- ✅ Прямая интеграция с ApplicationInstanceManager

### Управление окнами
- ✅ Прямое управление WPF окном с WebView2
- ✅ Легкая интеграция с AppSwitcher
- ✅ Контроль размера, позиции, состояния окна
- ✅ Корпоративный дизайн с заголовком и статус-баром

### Архитектурные преимущества
- ✅ Нативная интеграция с .NET/WPF
- ✅ Меньше зависимостей от внешних процессов
- ✅ Лучшая изоляция и безопасность
- ✅ Единый процесс для всех веб-приложений

## 📁 Структура компонентов

```
WindowsLauncher.UI/Components/WebView2/
├── WebView2ApplicationWindow.xaml      # XAML разметка окна
└── WebView2ApplicationWindow.xaml.cs   # Логика WebView2 окна

WindowsLauncher.Services/Lifecycle/Launchers/
└── WebView2ApplicationLauncher.cs      # Лаунчер для WebView2 приложений

WindowsLauncher.Tests/Services/Lifecycle/Launchers/
└── WebView2ApplicationLauncherTests.cs # Unit тесты
```

## 🏗️ Архитектура компонентов

### WebView2ApplicationWindow
**Назначение:** WPF окно с встроенным WebView2 контролом

**Основные возможности:**
- Полноценный веб-браузер на базе Chromium Edge
- Корпоративный заголовок с иконкой и кнопками управления
- Статусная строка с индикатором загрузки
- События для интеграции с ApplicationLifecycleService
- Настраиваемые параметры безопасности

**Ключевые свойства:**
```csharp
public string InstanceId { get; }           // Уникальный ID экземпляра
public DateTime StartTime { get; }          // Время запуска
public string LaunchedBy { get; }           // Кто запустил
```

**События жизненного цикла:**
```csharp
public event EventHandler<ApplicationInstance>? WindowActivated;
public event EventHandler<ApplicationInstance>? WindowDeactivated; 
public event EventHandler<ApplicationInstance>? WindowClosed;
public event EventHandler<ApplicationInstance>? WindowStateChanged;
```

### WebView2ApplicationLauncher
**Назначение:** Запуск и управление WebView2 приложениями

**Поддерживаемые типы:**
- `ApplicationType.ChromeApp` (заменяет ChromeAppLauncher)
- `ApplicationType.Web` (дополняет WebApplicationLauncher)

**Основные методы:**
```csharp
bool CanLaunch(Application application)                           // Проверка совместимости
Task<LaunchResult> LaunchAsync(Application app, string user)      // Запуск приложения  
Task<bool> SwitchToAsync(string instanceId)                      // Переключение на окно
Task<bool> TerminateAsync(string instanceId, bool force)         // Закрытие приложения
Task<IReadOnlyList<ApplicationInstance>> GetActiveInstancesAsync() // Список экземпляров
```

## 🔧 Интеграция с существующей архитектурой

### ApplicationLifecycleService
WebView2ApplicationLauncher автоматически регистрируется в коллекции лаунчеров и используется ApplicationLifecycleService для запуска веб-приложений.

### AppSwitcherService
WebView2 окна полностью интегрированы с переключателем приложений через события жизненного цикла.

### ApplicationInstanceManager
Каждое WebView2 окно создает ApplicationInstance с уникальным ID для отслеживания состояния.

## ⚙️ Конфигурация WebView2

### Настройки безопасности
```csharp
webView2.Settings.IsScriptEnabled = true;
webView2.Settings.AreDefaultScriptDialogsEnabled = true;
webView2.Settings.IsWebMessageEnabled = true;
webView2.Settings.AreDevToolsEnabled = true; // TODO: отключить в production
```

### Пользовательский агент
```csharp
webView2.Settings.UserAgent = $"WindowsLauncher/1.0 WebView2/{webView2.Environment.BrowserVersionString}";
```

### Папка пользовательских данных
```csharp
UserDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "WindowsLauncher", "WebView2", InstanceId)
```

## 🎨 UI компоненты

### Корпоративный заголовок
- Иконка приложения (emoji или текст)
- Название приложения 
- Кнопки управления (свернуть, развернуть, закрыть)
- Корпоративные цвета KDV

### Статусная строка
- URL или статус загрузки
- Индикатор прогресса
- Состояние соединения (цветной индикатор)

### Настраиваемость
```csharp
public bool ShowApplicationHeader { get; set; } = true;  // Показать заголовок
public bool ShowStatusBar { get; set; } = true;         // Показать статус-бар
```

## 🔄 Миграция с Chrome Apps

### Автоматическая совместимость
WebView2ApplicationLauncher автоматически обрабатывает приложения типа `ChromeApp`:

```csharp
// Извлекает URL из аргументов --app=URL
var args = application.Arguments ?? "";
var match = Regex.Match(args, @"--app=([^\\s]+)");
if (match.Success)
{
    return match.Groups[1].Value; // URL для WebView2
}
```

### Приоритет лаунчеров
1. **WebView2ApplicationLauncher** - высший приоритет для ChromeApp и Web
2. **ChromeAppLauncher** - fallback для совместимости
3. **WebApplicationLauncher** - fallback для простых веб-ссылок

## 🧪 Тестирование

### Unit тесты
- Проверка совместимости с различными типами приложений
- Валидация URL (HTTP/HTTPS)
- Обработка ошибок и edge cases
- Управление экземплярами

### Тестовые сценарии
```csharp
[Theory]
[InlineData(ApplicationType.ChromeApp, "https://example.com", "--app=https://example.com", true)]
[InlineData(ApplicationType.Web, "https://app.company.com", "", true)]
[InlineData(ApplicationType.Desktop, @"C:\Windows\notepad.exe", "", false)]
public void CanLaunch_WithVariousApplicationTypes_ShouldReturnCorrectResult(...)
```

## 🚀 Использование в производстве

### Требования
- **.NET 8.0-windows**
- **Microsoft.Web.WebView2 1.0.3351.48+**
- **Windows 10 версии 1803+ или Windows 11**
- **WebView2 Runtime** (обычно предустановлен)

### Развертывание
WebView2 Runtime автоматически устанавливается с Windows Updates. Для изолированных сред можно включить Evergreen режим.

### Производительность
- Один процесс для всех веб-приложений
- Изолированные пользовательские данные для каждого экземпляра
- Оптимизированное использование памяти по сравнению с отдельными процессами Chrome

## 🔍 Отладка и диагностика

### Логирование
Все компоненты WebView2 используют Microsoft.Extensions.Logging:

```csharp
_logger.LogInformation("Launching WebView2 application {AppName} (Instance: {InstanceId})", 
    application.Name, instanceId);
```

### Developer Tools
В debug режиме доступны Developer Tools (F12) для отладки веб-контента.

### Диагностика ошибок
- Подробное логирование навигации и ошибок
- Обработка исключений инициализации WebView2
- Мониторинг состояния соединения

## 📝 Примеры использования

### Запуск веб-приложения
```csharp
var app = new Application 
{
    Name = "Corporate Dashboard",
    Type = ApplicationType.Web,
    ExecutablePath = "https://dashboard.company.com",
    IconText = "📊"
};

var result = await lifecycleService.LaunchAsync(app, "john.doe");
```

### Миграция Chrome App
```csharp
// Старый Chrome App
var chromeApp = new Application 
{
    Name = "Gmail",
    Type = ApplicationType.ChromeApp,
    ExecutablePath = "chrome.exe",
    Arguments = "--app=https://mail.google.com"
};

// Автоматически запустится через WebView2ApplicationLauncher
var result = await lifecycleService.LaunchAsync(chromeApp, "user");
```

## 🎯 Следующие шаги

1. **Тестирование в Visual Studio** - проверка сборки и базовой функциональности
2. **Интеграционные тесты** - создание реальных WebView2 окон
3. **Performance тесты** - сравнение с Chrome Apps
4. **Постепенная миграция** - перевод Chrome Apps на WebView2
5. **Отключение ChromeAppLauncher** - после успешного тестирования

WebView2 решение представляет собой современный, масштабируемый и легко управляемый подход для запуска веб-приложений в WindowsLauncher.
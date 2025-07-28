# Руководство по использованию сенсорной клавиатуры в Windows 10

## Обзор

В приложении WindowsLauncher реализована поддержка сенсорной клавиатуры Windows 10 через `VirtualKeyboardService` с автоматическим показом при фокусе на текстовых полях.

## Архитектура

### Основные компоненты

1. **IVirtualKeyboardService** - интерфейс для управления виртуальной клавиатурой
2. **VirtualKeyboardService** - реализация с поддержкой Windows 10
3. **TouchKeyboardHelper** - автоматическое управление в UI

### Методы запуска в Windows 10

Сервис использует несколько подходов для запуска TabTip.exe:

#### Метод 1: Стандартный TabTip.exe
```csharp
// Путь: C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe
var process = Process.Start(TOUCH_KEYBOARD_EXECUTABLE_PATH);
```

#### Метод 2: Windows 10 Touch Keyboard API
```csharp
// Использование PowerShell для вызова Windows Runtime API  
powershell.exe -Command "
  Add-Type -AssemblyName System.Runtime.WindowsRuntime;
  [Windows.UI.ViewManagement.InputPane]::GetForCurrentView().TryShow()
"
```

#### Метод 3: Настройки системы
```csharp
// Открытие настроек специальных возможностей
Process.Start("ms-settings:easeofaccess-keyboard");
```

## Использование в коде

### Регистрация сервиса

В `Program.cs` или `Startup.cs`:
```csharp
services.AddScoped<IVirtualKeyboardService, VirtualKeyboardService>();
```

### Программное управление

```csharp
public class MyViewModel
{
    private readonly IVirtualKeyboardService _keyboardService;

    public MyViewModel(IVirtualKeyboardService keyboardService)
    {
        _keyboardService = keyboardService;
    }

    // Показать клавиатуру
    public async Task ShowKeyboard()
    {
        var success = await _keyboardService.ShowVirtualKeyboardAsync();
        if (!success) 
        {
            // Обработка ошибки
        }
    }

    // Скрыть клавиатуру
    public async Task HideKeyboard()
    {
        await _keyboardService.HideVirtualKeyboardAsync();
    }

    // Переключить состояние
    public async Task ToggleKeyboard()
    {
        await _keyboardService.ToggleVirtualKeyboardAsync();
    }
}
```

### Автоматическое управление в XAML

В конструкторе окна или UserControl:
```csharp
public partial class LoginWindow : Window
{
    public LoginWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        
        // Подключаем автоматический показ клавиатуры
        TouchKeyboardHelper.Initialize(serviceProvider);
        TouchKeyboardHelper.AttachToWindow(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Отключаем при закрытии окна
        TouchKeyboardHelper.DetachFromWindow(this);
        base.OnClosed(e);
    }
}
```

### События состояния

```csharp
_keyboardService.StateChanged += (sender, args) =>
{
    if (args.IsVisible)
    {
        Console.WriteLine($"Клавиатура показана: {args.Message}");
    }
    else
    {
        Console.WriteLine($"Клавиатура скрыта: {args.Message}");
    }
};
```

## Конфигурация в appsettings.json

```json
{
  "TouchKeyboard": {
    "AutoShowOnFocus": true,
    "AutoHideOnFocusLost": true,
    "PreferTabTip": true,
    "FallbackToOSK": true,
    "ShowSettingsIfUnavailable": true,
    "DelayBeforeHide": 100
  }
}
```

## Особенности Windows 10

### TabTip.exe в Windows 10

- **Расположение:** `C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe`  
- **Особенность:** Может не отображаться на обычных ПК без сенсорного экрана
- **Решение:** Используем Windows Runtime API для принудительного показа

### Системные требования

- Windows 10 версии 1703 и выше
- .NET 8 Desktop Runtime
- Права пользователя (не требует админ прав)

### Проверка доступности

```csharp
if (_keyboardService.IsVirtualKeyboardAvailable())
{
    // Клавиатура доступна
    await _keyboardService.ShowVirtualKeyboardAsync();
}
else
{
    // Fallback на OSK или показ сообщения
    MessageBox.Show("Сенсорная клавиатура недоступна");
}
```

## Интеграция с Material Design

### Кнопка для показа клавиатуры

```xml
<Button Style="{StaticResource CorporateButton}"
        Content="🔤 Показать клавиатуру"
        Command="{Binding ShowKeyboardCommand}"
        Margin="5"
        ToolTip="Показать сенсорную клавиатуру"/>
```

### Стиль для текстовых полей с поддержкой сенсора

```xml
<Style x:Key="TouchTextBox" TargetType="TextBox" BasedOn="{StaticResource MaterialTextBoxStyle}">
    <Setter Property="FontSize" Value="16"/>
    <Setter Property="MinHeight" Value="40"/>
    <Setter Property="Padding" Value="10,8"/>
    <EventSetter Event="GotFocus" Handler="OnTextBoxGotFocus"/>
    <EventSetter Event="LostFocus" Handler="OnTextBoxLostFocus"/>
</Style>
```

## Логирование и отладка

### Включение детального логирования

```json
{
  "Logging": {
    "LogLevel": {
      "WindowsLauncher.Services.VirtualKeyboardService": "Debug"
    }
  }
}
```

### Типичные сообщения в логах

```
[Information] TouchKeyboardHelper инициализирован
[Information] Попытка запуска TabTip.exe из стандартного расположения  
[Information] TabTip успешно запущен из стандартного расположения
[Debug] Сенсорная клавиатура показана для элемента TextBox
[Warning] Не удалось запустить TabTip процесс
[Information] Попытка запуска сенсорной клавиатуры через Windows 10 API
```

## Устранение проблем

### TabTip.exe не запускается

**Проблема:** Процесс TabTip запускается, но клавиатура не отображается

**Решения:**
1. Включить сенсорную клавиатуру в настройках Windows:
   - `ms-settings:easeofaccess-keyboard`
   - Включить "Использовать экранную клавиатуру"

2. Проверить реестр Windows:
   ```reg
   [HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
   "EnableDesktopModeAutoInvoke"=dword:00000001
   ```

3. Перезапустить службу "TabletInputService":
   ```cmd
   net stop TabletInputService
   net start TabletInputService
   ```

### Клавиатура не скрывается автоматически

**Проблема:** TouchKeyboardHelper не скрывает клавиатуру при потере фокуса

**Решение:** Проверить подключение события `LostFocus` и задержку в `OnTextElementLostFocus`

### Ошибки Windows Runtime API

**Проблема:** PowerShell не может загрузить Windows Runtime assemblies

**Решение:** Использовать fallback на OSK:
```csharp
if (!await LaunchTouchKeyboardAPI())
{
    await TryStartOSK(); // Fallback на osk.exe
}
```

## Альтернативы

### 1. Классическая экранная клавиатура (OSK)

```csharp
Process.Start(@"C:\Windows\System32\osk.exe");
```

### 2. Сторонние решения

- **Click-N-Type** - бесплатная виртуальная клавиатура
- **On-Screen Keyboard Portable** - портативная версия
- **Hot Virtual Keyboard** - коммерческое решение

### 3. Веб-компонент

```html
<!-- Для гибридных приложений -->
<input type="text" inputmode="text" />
```

## Настройка для корпоративной среды

### Group Policy настройки

```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\TabletTip\1.7]
"EnableDesktopModeAutoInvoke"=dword:00000001
"EnableAutomaticInvocation"=dword:00000001
```

### Массовое развертывание

PowerShell скрипт для настройки на всех машинах:
```powershell
# EnableTouchKeyboard.ps1
Set-ItemProperty -Path "HKCU:\Software\Microsoft\TabletTip\1.7" -Name "EnableDesktopModeAutoInvoke" -Value 1
Restart-Service TabletInputService -Force
```

### Мониторинг использования

```csharp
_keyboardService.StateChanged += (sender, args) =>
{
    // Логирование для аналитики использования
    _auditService.LogEvent("VirtualKeyboard", args.IsVisible ? "Shown" : "Hidden");
};
```

## Заключение

Интеграция сенсорной клавиатуры в WindowsLauncher обеспечивает:

✅ **Автоматический показ** при фокусе на текстовых полях  
✅ **Множественные методы запуска** для максимальной совместимости  
✅ **Fallback на OSK** если TabTip недоступен  
✅ **Material Design интеграция** с корпоративными стилями  
✅ **Логирование и мониторинг** для поддержки  
✅ **Корпоративная настройка** через Group Policy  

Решение готово для использования в корпоративной среде с сенсорными мониторами и планшетными ПК под управлением Windows 10.
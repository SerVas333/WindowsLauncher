# Windows 10 Touch Keyboard Solution

## Проблема

В Windows 10 сенсорная клавиатура (TabTip.exe) работает по-другому, чем в Windows 11:

- **Windows 10**: Использует TabTip.exe с COM интерфейсом ITipInvocation
- **Windows 11**: Использует TextInputHost.exe с WinRT API InputPane

Предыдущая реализация не учитывала эти различия, что приводило к нестабильной работе в Windows 10.

## Новое решение

### 1. **Windows10TouchKeyboardService.cs**

Специализированный сервис для Windows 10 с использованием COM интерфейса:

```csharp
[ComImport]
[Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITipInvocation
{
    void Toggle(IntPtr hwnd);
}
```

**Особенности:**
- Использует официальный COM интерфейс ITipInvocation
- Получает handle активного окна для правильной работы
- Мягкое позиционирование без перехвата фокуса
- Fallback на прямой запуск TabTip.exe при неудаче COM

### 2. **WindowsVersionHelper.cs**

Определение версии Windows и совместимости:

```csharp
public enum TouchKeyboardCompatibility
{
    OSKOnly,              // Windows 7 и ранее
    TabTipLegacy,         // Windows 8/8.1
    TabTipWithCOM,        // Windows 10 (наше решение)
    TextInputHost         // Windows 11+
}
```

**Функции:**
- Точное определение версии Windows через RtlGetVersion
- Классификация совместимости с клавиатурой
- Рекомендации по выбору сервиса

### 3. **VirtualKeyboardServiceFactory.cs**

Автоматический выбор подходящего сервиса:

```csharp
public class AdaptiveVirtualKeyboardService : IVirtualKeyboardService
{
    // Автоматически выбирает подходящий сервис при создании
    // Прозрачно работает со всеми версиями Windows
}
```

**Логика выбора:**
- Windows 10 → Windows10TouchKeyboardService (COM)
- Windows 11 → VirtualKeyboardService (универсальный)
- Windows 8/8.1 → VirtualKeyboardService (legacy TabTip)
- Windows 7 → VirtualKeyboardService (OSK fallback)

## Архитектура решения

```
                    IVirtualKeyboardService
                            ↑
                 AdaptiveVirtualKeyboardService
                            ↑
                VirtualKeyboardServiceFactory
                       ↙            ↘
    Windows10TouchKeyboardService   VirtualKeyboardService
         (COM ITipInvocation)         (Universal fallback)
```

## Тестирование на Windows 10

### Предварительные проверки

1. **Проверить версию Windows:**
   ```cmd
   winver
   # Должно показать Windows 10 Build < 22000
   ```

2. **Проверить наличие TabTip.exe:**
   ```cmd
   dir "C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe"
   ```

3. **Проверить COM интерфейс (PowerShell):**
   ```powershell
   $tipInvocation = New-Object -ComObject "UIHostNoLaunch"
   $tipInvocation -ne $null
   # Должно вернуть True
   ```

### Тестовые сценарии

#### 1. **Автоматический показ при фокусе**

**Шаги:**
1. Запустить приложение на Windows 10
2. Войти в LoginWindow
3. Нажать на поле "Username"
4. Нажать на поле "Password"
5. Перейти в MainWindow
6. Нажать на поля поиска

**Ожидаемый результат:**
- ✅ Клавиатура появляется при фокусе на поля
- ✅ НЕ крадет фокус у активного поля
- ✅ Позиционируется внизу экрана
- ✅ Работает стабильно во всех окнах

#### 2. **Кнопка показа клавиатуры**

**Шаги:**
1. В MainWindow нажать кнопку "⌨️" (правый нижний угол)
2. Повторить нажатие несколько раз
3. Проверить логи диагностики

**Ожидаемый результат:**
- ✅ Клавиатура показывается при каждом нажатии
- ✅ НЕ переключается (toggle), а всегда показывает
- ✅ Правильно позиционируется

#### 3. **Диагностика и логирование**

**Проверить в Output Window → "WindowsLauncher.UI Debug":**

**Успешная инициализация:**
```
[Information] Обнаружена версия Windows: Windows10 (Build XXXXX)
[Information] Совместимость с клавиатурой: TabTipWithCOM
[Information] Создание Windows 10 сервиса клавиатуры с COM интерфейсом
[Information] COM интерфейс ITipInvocation успешно инициализирован
```

**Успешный показ:**
```
[Information] Показ виртуальной клавиатуры через Windows 10 ITipInvocation
[Debug] Окно клавиатуры найдено и видимо
[Information] Клавиатура успешно показана через ITipInvocation
```

**Проблемы (требуют внимания):**
```
[Error] Ошибка инициализации COM интерфейса ITipInvocation
[Warning] Не удалось создать Windows 10 сервис, используем универсальный
[Warning] Ошибка при использовании ITipInvocation
```

### Специфичные для Windows 10 проверки

#### 1. **Проверка реестра**

Клавиатура может требовать включения через реестр:
```reg
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"EnableDesktopModeAutoInvoke"=dword:00000001
```

#### 2. **Проверка служб Windows**

TouchKeyboard service должна быть запущена:
```cmd
sc query TabletInputService
# Status должен быть RUNNING
```

#### 3. **Проверка процессов**

```cmd
tasklist | findstr TabTip
# Должен показать процесс TabTip.exe после показа клавиатуры
```

### Решение проблем

#### Проблема: COM интерфейс не инициализируется

**Возможные причины:**
- Отсутствует регистрация COM объекта
- Недостаточные права доступа
- Поврежденная установка Windows

**Решение:**
1. Запустить как администратор
2. Переустановить компоненты планшетного ПК:
   ```cmd
   dism /online /enable-feature /featurename:TabletPCOC
   ```
3. Перерегистрировать COM объекты:
   ```cmd
   regsvr32 tiptsf.dll
   ```

#### Проблема: TabTip.exe не запускается

**Возможные причины:**
- Отключен сервис Touch Keyboard
- Политики группы блокируют TabTip
- Отсутствуют файлы

**Решение:**
1. Включить службу:
   ```cmd
   sc config TabletInputService start= auto
   sc start TabletInputService
   ```
2. Проверить групповые политики
3. Переустановить Windows компоненты

#### Проблема: Клавиатура появляется, но не позиционируется

**Решение:**
- Проверить разрешение экрана
- Отключить DPI scaling для приложения
- Проверить multiple monitor setup

### Fallback behavior

Если Windows 10 специфичный сервис не работает:

1. **Первый fallback**: Универсальный VirtualKeyboardService
2. **Второй fallback**: Прямой запуск TabTip.exe
3. **Третий fallback**: Классическая экранная клавиатура (osk.exe)

### Логи диагностики

Команда для полной диагностики:
```csharp
var diagnosis = await virtualKeyboardService.DiagnoseVirtualKeyboardAsync();
```

Показывает:
- Версию Windows и совместимость
- Состояние COM интерфейса
- Наличие файлов и процессов
- Состояние окон клавиатуры
- Настройки реестра

## Ожидаемые улучшения

### До внедрения Windows 10 решения:
- ❌ Клавиатура не появлялась при нажатии кнопок
- ❌ Нестабильная работа автопоказа
- ❌ Конфликты с фокусом
- ❌ Неправильное позиционирование

### После внедрения:
- ✅ Стабильная работа кнопки показа через COM
- ✅ Надежный автопоказ при фокусе
- ✅ Корректное позиционирование
- ✅ Сохранение фокуса в полях ввода
- ✅ Адаптивный выбор метода по версии ОС

## Дальнейшие улучшения

1. **Windows 11 специализированный сервис**: Использование WinRT InputPane API
2. **Кэширование COM объектов**: Повышение производительности
3. **Конфигурируемость**: Настройки поведения через appsettings.json
4. **Метрики**: Телеметрия успешности показа клавиатуры

---

**Статус:** ✅ Реализовано и готово к тестированию  
**Приоритет:** 🔥 Высокий - критично для Windows 10  
**Тестирование:** 🧪 Требуется проверка на реальных системах Windows 10
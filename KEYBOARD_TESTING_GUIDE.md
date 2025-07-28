# Руководство по тестированию сенсорной клавиатуры Windows 10

## Обновления в VirtualKeyboardService

Сервис был полностью переписан с использованием Windows API для решения проблем с показом TabTip.exe в Windows 10.

### Новые методы запуска

1. **Windows API поиск и показ** - Ищет существующие окна клавиатуры через `FindWindow()` и показывает их через `ShowWindow()`

2. **Принудительный запуск через CMD** - Убивает существующие процессы TabTip и запускает новый через cmd.exe

3. **Реестр + запуск** - Устанавливает `EnableDesktopModeAutoInvoke=1` в реестре и запускает TabTip

4. **Fallback на OSK** - Классическая экранная клавиатура как запасной вариант

### Диагностика

Добавлена функция `DiagnoseVirtualKeyboardAsync()` которая проверяет:
- Существование файлов TabTip.exe и OSK.exe
- Запущенные процессы
- Окна клавиатур через Windows API
- Настройки реестра

## Как тестировать

### 1. Запуск приложения и диагностика

1. Запустите `WindowsLauncher.UI` в Visual Studio (F5)
2. В окне входа нажмите кнопку "🔤" (виртуальная клавиатура)
3. Будет выполнена диагностика и попытка показа клавиатуры
4. Если клавиатура не появится, появится окно с результатами диагностики

### 2. Анализ диагностики

В окне диагностики вы увидите:
```
=== ДИАГНОСТИКА ВИРТУАЛЬНОЙ КЛАВИАТУРЫ ===
TabTip.exe существует: True/False
Путь TabTip: C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe
OSK.exe существует: True/False
Путь OSK: C:\Windows\System32\osk.exe
Процессов TabTip запущено: 0
Процессов OSK запущено: 0
Окно сенсорной клавиатуры найдено: False
Окно OSK найдено: False
Реестр EnableDesktopModeAutoInvoke:
  - Exit Code: 0/1
  - Output: ...
=== КОНЕЦ ДИАГНОСТИКИ ===
```

### 3. Проверка логов

Откройте **View → Output** в Visual Studio и выберите "WindowsLauncher.UI Debug".
Ищите сообщения:
```
[Information] Попытка показать виртуальную клавиатуру
[Information] Запуск TabTip через cmd: C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe
[Information] Пытаемся включить сенсорную клавиатуру через реестр
[Information] TabTip принудительно запущен и показан
```

### 4. Ручная проверка TabTip

Откройте командную строку и выполните:
```cmd
# Проверка файла
dir "C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe"

# Запуск вручную
"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe"

# Проверка процесса
tasklist | findstr TabTip

# Установка реестра
reg add "HKCU\Software\Microsoft\TabletTip\1.7" /v EnableDesktopModeAutoInvoke /t REG_DWORD /d 1 /f
```

### 5. Альтернативный запуск OSK

Если TabTip не работает, проверьте OSK:
```cmd
# Запуск OSK
osk.exe

# Проверка процесса
tasklist | findstr osk
```

## Типичные проблемы и решения

### TabTip.exe не показывается

**Причина:** Windows 10 блокирует показ TabTip на обычных ПК без сенсорного экрана

**Решения:**
1. **Включить в настройках Windows:**
   - Открыть Settings → Ease of Access → Keyboard
   - Включить "Use the On-Screen Keyboard"

2. **Реестр:**
   ```reg
   [HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
   "EnableDesktopModeAutoInvoke"=dword:00000001
   ```

3. **Служба TabletInputService:**
   ```cmd
   net start TabletInputService
   ```

### Процесс запускается но окно не видно

**Причина:** TabTip работает в фоне но не показывает UI

**Решение:** Приложение теперь использует `FindWindow()` и `ShowWindow()` для принудительного показа

### OSK не запускается

**Причина:** Политики безопасности или поврежденные файлы

**Решение:** 
```cmd
sfc /scannow
dism /online /cleanup-image /restorehealth
```

## Расширенные настройки

### Group Policy для корпоративной среды

```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\TabletTip\1.7]
"EnableDesktopModeAutoInvoke"=dword:00000001
"EnableAutomaticInvocation"=dword:00000001
```

### PowerShell скрипт для массового развертывания

```powershell
# EnableTouchKeyboard.ps1
$regPath = "HKCU:\Software\Microsoft\TabletTip\1.7"
if (!(Test-Path $regPath)) {
    New-Item -Path $regPath -Force
}
Set-ItemProperty -Path $regPath -Name "EnableDesktopModeAutoInvoke" -Value 1
Restart-Service TabletInputService -Force
Write-Host "Touch keyboard enabled"
```

### Мониторинг через Event Viewer

1. Откройте Event Viewer
2. Перейдите в Application and Services Logs
3. Ищите события от TabTip или TouchKeyboard

## Результаты тестирования

После тестирования заполните:

- [ ] TabTip.exe существует: ___
- [ ] TabTip запускается вручную: ___  
- [ ] TabTip показывается через приложение: ___
- [ ] OSK работает как fallback: ___
- [ ] Автоматический показ при фокусе: ___
- [ ] Реестр настроен правильно: ___

**Версия Windows:** _______________
**Сенсорный экран:** Да / Нет
**Результат:** Работает / Не работает / Частично

---

Пожалуйста, запустите тесты и отправьте результаты диагностики для дальнейшего устранения проблем.
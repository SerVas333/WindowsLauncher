# Deployment Guide - Windows Launcher

## Исправление ClickOnce для .NET 8

Проблема ClickOnce была решена путем создания современного профиля публикации, совместимого с .NET 8.

### Что было исправлено:

1. **Удален проблемный ClickOnce профиль** - старый `ClickOnceProfile.pubxml` содержал устаревшие настройки
2. **Создан новый Folder Profile** - современный способ публикации для .NET 8
3. **Обновлены настройки проекта** - убраны legacy свойства, добавлены современные

### Как публиковать приложение в Visual Studio:

#### Вариант 1: Folder Publishing (Рекомендуется)
1. **Right-click** на проект `WindowsLauncher.UI` → **Publish**
2. Выберите **Folder** как target
3. Путь: `bin\Release\net8.0-windows\publish\` (уже настроен)
4. Нажмите **Publish**

#### Вариант 2: MSI Installer (Альтернатива)
```xml
<!-- Добавить в WindowsLauncher.UI.csproj -->
<PropertyGroup>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <PackageId>KDV.WindowsLauncher</PackageId>
  <Title>KDV Windows Launcher</Title>
  <Authors>KDV Group</Authors>
  <Description>Enterprise application launcher with Active Directory authentication</Description>
</PropertyGroup>
```

#### Вариант 3: ClickOnce (если нужен)
Для ClickOnce в .NET 8 используйте:
```powershell
# В Package Manager Console
dotnet publish -c Release -r win-x64 --self-contained false
```

### Структура деплоймента:

```
publish/
├── WindowsLauncher.UI.exe          # Основной исполняемый файл
├── WindowsLauncher.UI.dll
├── appsettings.json                # Конфигурация
├── runtimes/                       # Нативные библиотеки
├── firebird/                       # Firebird embedded (если есть)
└── *.dll                          # Зависимости .NET
```

### Настройки для разных режимов:

#### Обычный режим (Desktop App):
```json
"SessionManagement": {
  "RunAsShell": false,
  "LogoutOnMainWindowClose": true,
  "ReturnToLoginOnLogout": true
}
```

#### Shell режим (Kiosk/Embedded):
```json
"SessionManagement": {
  "RunAsShell": true,
  "AutoRestartOnClose": true,
  "LogoutOnMainWindowClose": true
}
```

### Автоматическая установка как Shell:

Для замены explorer.exe создайте reg-файл:
```reg
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon]
"Shell"="C:\\Program Files\\KDV\\WindowsLauncher\\WindowsLauncher.UI.exe"
```

### Тестирование деплоймента:

1. **Проверьте зависимости:**
   - .NET 8 Desktop Runtime должен быть установлен на целевой машине
   - Windows 10+ (поддержка согласно app.manifest)

2. **Проверьте файлы конфигурации:**
   - `appsettings.json` для основных настроек
   - `%AppData%\WindowsLauncher\` для пользовательских данных

3. **Проверьте логи:**
   - Console output в Debug режиме
   - Event Viewer для критических ошибок

### Устранение проблем:

#### "Could not load assembly" ошибки:
- Убедитесь что все DLL включены в publish
- Проверьте что .NET 8 Desktop Runtime установлен

#### База данных не создается:
- Проверьте права записи в `%AppData%\WindowsLauncher\`
- Проверьте что SQLite/Firebird нативные библиотеки скопированы

#### Active Directory не работает:
- Настройте fallback режим: `"EnableFallbackMode": true`
- Проверьте сетевое подключение к домен-контроллеру

### Команды для автоматизации:

```powershell
# Сборка Release версии
dotnet build -c Release

# Публикация
dotnet publish -c Release -r win-x64 --self-contained false

# Создание ZIP архива
Compress-Archive -Path "bin\Release\net8.0-windows\publish\*" -DestinationPath "WindowsLauncher-v1.0.zip"
```

## Готово к использованию!

Приложение теперь готово для деплоймента в корпоративной среде с поддержкой:
- ✅ Session Management с Shell режимом
- ✅ Современная публикация .NET 8
- ✅ Active Directory интеграция
- ✅ Multi-database поддержка (SQLite/Firebird)
- ✅ Material Design корпоративный интерфейс
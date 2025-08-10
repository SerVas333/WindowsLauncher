# Руководство по настройке Android для WindowsLauncher

Это руководство объясняет, как настроить поддержку Android-приложений в WindowsLauncher с WSA (Windows Subsystem for Android), включая новую конфигурируемую систему управления Android-подсистемой, автоматическую установку APK/XAPK и устранение неполадок.

## Новое в v1.2.0: Расширенная поддержка Android

WindowsLauncher теперь имеет комплексное Android решение с множественными улучшениями:

### ✨ **Ключевые возможности**
- **🔄 Автоматическая установка APK/XAPK** - Приложения автоматически устанавливаются при первом запуске
- **📦 Поддержка XAPK** - Полная поддержка пакетов XAPK со split APK файлами
- **🚀 Множественные методы установки** - Стратегии резервирования для сложных APK форматов
- **⚙️ Конфигурируемая подсистема** - Три режима работы для разных потребностей
- **📊 Мониторинг в реальном времени** - Живой статус WSA и диагностика

### **Режимы работы**

- **Disabled** - Android функциональность полностью отключена, экономит системные ресурсы
- **OnDemand** - WSA запускается при необходимости (сбалансированный подход, по умолчанию)
- **Preload** - WSA предзагружается в фоне для максимальной производительности

### Конфигурация Android

Настройка в `appsettings.json`:

```json
{
  "AndroidSubsystem": {
    "Mode": "OnDemand",
    "PreloadDelaySeconds": 30,
    "AutoStartWSA": true,
    "ShowStatusInUI": true,
    "EnableDiagnostics": true,
    "ResourceOptimization": {
      "MaxMemoryMB": 2048,
      "StopWSAOnIdle": false,
      "IdleTimeoutMinutes": 30
    },
    "Fallback": {
      "DisableOnLowMemory": true,
      "MemoryThresholdMB": 4096
    }
  }
}
```

**Интеграция с UI:**
- Индикатор статуса WSA в статус-баре главного окна (🤖)
- Обновления статуса в реальном времени (Готов, Запуск, Ошибка и т.д.)
- Админ панель скрывает Android функции при Mode = "Disabled"

## Быстрый старт

### Автоматическая установка (Рекомендуемая)

Запустите скрипт автоматической установки от имени Администратора:

```powershell
# Установить AAPT и ADB
.\Scripts\Install-AndroidTools.ps1

# Установить только ADB (если AAPT уже доступен)
.\Scripts\Install-AndroidTools.ps1 -BuildToolsOnly

# Установить только AAPT (если ADB уже доступен) 
.\Scripts\Install-AndroidTools.ps1 -PlatformToolsOnly

# Принудительная переустановка существующих инструментов
.\Scripts\Install-AndroidTools.ps1 -Force
```

### Ручная проверка

После установки проверьте доступность инструментов:

```powershell
# Проверить доступность ADB
adb version

# Проверить доступность AAPT
aapt version

# Проверить статус WSA
Get-AppxPackage MicrosoftCorporationII.WindowsSubsystemForAndroid
```

## Предварительные требования

### Совместимость с Windows - Поддержка WSA

**Windows 11 (Официально поддерживается):**
- ✅ **Полная поддержка WSA** через Microsoft Store
- ✅ **Автоматические обновления** и официальная поддержка
- ✅ **Все Android функции WindowsLauncher** работают безупречно

**Windows 10 (Поддержка сообщества):**
- ⚠️ **WSA доступен через неофициальные установщики** (WSABuilds, MagiskOnWSA и др.)
- ⚠️ **Требуется ручная установка** - недоступен в Microsoft Store
- ⚠️ **Нет официальной поддержки Microsoft** - поддерживается сообществом
- ⚠️ **Потенциальные проблемы стабильности** по сравнению с Windows 11
- ✅ **Android функции WindowsLauncher работают** при правильной установке WSA

### Варианты установки WSA для Windows 10

**Неофициальные установщики WSA для Windows 10:**
1. **WSABuilds** - https://github.com/MustardChef/WSABuilds
2. **MagiskOnWSA** - https://github.com/LSPosed/MagiskOnWSA
3. **WSA-Script** - Автоматизированные скрипты установки

**Требования для Windows 10:**
- Windows 10 Build 19041 (20H1) или выше
- Включенная виртуализация в BIOS/UEFI
- Минимум 8ГБ RAM (рекомендуется 16ГБ)
- Права администратора для установки

### Windows Subsystem for Android (WSA) - Официальная установка

1. **Официальная установка Windows 11:**
   - Windows 11 Build 22000.0 или выше
   - Минимум 8ГБ RAM (рекомендуется 16ГБ)
   - Включенная виртуализация в BIOS/UEFI
   - TPM 2.0 и Secure Boot включены

2. **Установить WSA:**
   ```powershell
   # Через Microsoft Store
   ms-windows-store://pdp/?ProductId=9P3395VX91NR
   
   # Через PowerShell (если Store недоступен)
   Add-AppxPackage -RegisterByFamilyName -MainPackage MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe
   ```

3. **Включить режим разработчика:**
   - Открыть настройки Windows Subsystem for Android
   - Включить "Developer mode"
   - Отметить IP адрес для ADB подключения (обычно 127.0.0.1:58526)

### Android Tools

WindowsLauncher требует два основных Android SDK инструмента:

1. **AAPT (Android Asset Packaging Tool)** - для извлечения метаданных APK
2. **ADB (Android Debug Bridge)** - для установки и управления приложениями

## Методы установки

### Метод 1: Автоматический скрипт (Рекомендуемый)

Скрипт `Install-AndroidTools.ps1` автоматически загружает и устанавливает необходимые инструменты:

```powershell
# Запустить от имени Администратора для изменения PATH
PowerShell -ExecutionPolicy Bypass -File .\Scripts\Install-AndroidTools.ps1
```

**Что делает:**
- Загружает Android Build Tools (включает AAPT)
- Загружает Android Platform Tools (включает ADB и Fastboot)
- Устанавливает в `C:\WindowsLauncher\Tools\Android\`
- Добавляет директории инструментов в системный PATH
- Проверяет установку тестированием версий инструментов

**Места установки:**
- AAPT: `C:\WindowsLauncher\Tools\Android\android-14\aapt.exe`
- ADB: `C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe`
- Fastboot: `C:\WindowsLauncher\Tools\Android\platform-tools\fastboot.exe`

### Метод 2: Android Studio (Продвинутые пользователи)

Если у вас установлен Android Studio:

1. **Найти существующие инструменты:**
   ```
   %LOCALAPPDATA%\Android\Sdk\build-tools\{version}\aapt.exe
   %LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe
   ```

2. **Добавить в PATH:**
   - Добавить директории build-tools и platform-tools в системный PATH
   - Перезапустить WindowsLauncher для обнаружения инструментов

### Метод 3: Ручная загрузка

Для продвинутых пользователей или корпоративных сред:

1. **Загрузить Build Tools:**
   - URL: https://dl.google.com/android/repository/build-tools_r34-windows.zip
   - Распаковать в `C:\WindowsLauncher\Tools\Android\`

2. **Загрузить Platform Tools:**
   - URL: https://dl.google.com/android/repository/platform-tools-latest-windows.zip
   - Распаковать в `C:\WindowsLauncher\Tools\Android\`

3. **Обновить PATH вручную через Свойства системы**

## Использование в WindowsLauncher

### Режимы Android-подсистемы

**Режим Disabled (Отключено):**
- Android функциональность полностью скрыта в UI
- Нет потребления ресурсов WSA
- Идеально для установок, которым не нужны Android приложения

**Режим OnDemand (По умолчанию):**
- WSA запускается автоматически при запуске Android приложения
- Сбалансированное использование ресурсов и производительность
- Рекомендуется для большинства пользователей

**Режим Preload (Предзагрузка):**
- WSA запускается в фоне с настраиваемой задержкой
- Максимальная производительность для частого использования Android приложений
- Высокое потребление памяти

### Добавление Android приложений

**Предварительные условия:** AndroidMode должен быть "OnDemand" или "Preload"

1. **Открыть Админ панель** (кнопка 🛠️ Админ)
2. **Добавить новое приложение** (➕ Добавить приложение)
3. **Установить тип приложения:** Android APK (видно только когда Android включен)
4. **Выбрать файл APK/XAPK:** Выберите ваш файл Android приложения
   - **📱 APK файлы** - Стандартные файлы пакетов Android
   - **📦 XAPK файлы** - Улучшенные пакеты с поддержкой split APK (рекомендуется для сложных приложений)
5. **Автоматическое извлечение метаданных:** WindowsLauncher автоматически извлекает:
   - Имя пакета (com.example.app)
   - Код и имя версии
   - Минимальная и целевая версии SDK
   - Отображаемое имя приложения
   - Информацию о split APK (для XAPK файлов)
6. **Умная установка:** Приложения автоматически устанавливаются при первом запуске

**💡 **Совет:** Используйте XAPK файлы для приложений, которые ранее не устанавливались с ошибкой "INSTALL_FAILED_MISSING_SPLIT".

### Запуск Android приложений

**🚀 Улучшенный процесс запуска:**

**Возможности всех режимов:**
- **🔍 Интеллектуальное определение:** Автоматически определяет APK против XAPK файлов
- **⚡ Умная установка:** Приложения автоматически устанавливаются при первом запуске, если еще не установлены
- **🛠️ Множественные методы установки:** 
  - Стандартная установка APK
  - Установка split APK для сложных приложений
  - Распаковка XAPK и установка multi-APK
  - Резервные методы для проблемных пакетов

**Режим OnDemand:**
1. **Проверка WSA:** Проверяет доступность WSA
2. **Автоматический запуск:** Запускает WSA если не запущен
3. **ADB подключение:** Подключается к WSA (127.0.0.1:58526)  
4. **Умная установка:** 
   - Проверяет, установлено ли приложение в WSA
   - Если не установлено: автоматически находит APK/XAPK файл в базе данных
   - Устанавливает используя наиболее подходящий метод (стандартный, split или XAPK)
5. **Запуск:** Открывает приложение в WSA окружении

**Режим Preload:**
1. **Предварительно разогретый WSA:** Использует уже запущенный экземпляр WSA
2. **Мгновенное подключение:** Быстрое ADB подключение
3. **Мгновенная установка:** При необходимости использует кэшированные APK/XAPK файлы для быстрейшей установки
4. **Быстрый запуск:** Минимальная задержка запуска

**Методы установки (автоматическое резервирование):**
1. **Стандартная установка:** `adb install "app.apk"`
2. **Установка тестового APK:** `adb install -t -r "app.apk"` (для совместимости split APK)
3. **Принудительная установка:** `adb install -g -t -r "app.apk"` (предоставляет все разрешения)
4. **Установка XAPK:** Распаковывает и устанавливает все APK файлы используя `adb install-multiple`
5. **Индивидуальная установка:** Возвращается к установке каждого split APK отдельно

## Диагностика и устранение неполадок

### Встроенный инструмент диагностики

WindowsLauncher включает всестороннюю Android диагностику:

1. **Открыть Админ панель**
2. **Нажать "🤖 Диагностика Android"** (видно только когда Android включен)
3. **Просмотреть результаты:**
   - Текущая конфигурация AndroidMode (Disabled/OnDemand/Preload)
   - Доступность и статус WSA
   - Доступность и версия ADB
   - Версия Android в WSA
   - Пути установки инструментов (системный PATH vs директории WindowsLauncher)
   - Установленные Android приложения
   - Использование памяти и настройки оптимизации
   - Статус AndroidSubsystemService

### Индикатор статуса WSA

Статус-бар главного окна показывает статус WSA в реальном времени:
- 🤖 **Ready** - WSA запущен и доступен (зеленый фон)
- 🤖 **Starting** - WSA запускается (оранжевый фон)  
- 🤖 **Error** - WSA недоступен или ошибка (красный фон)
- 🤖 **Disabled** - Android подсистема отключена (серый)

### Поддержка файлов XAPK

**Что такое XAPK?**
XAPK это улучшенный APK формат, который содержит:
- Основной APK файл(ы)
- Split APK файлы для разных конфигураций устройств
- OBB expansion файлы (при необходимости)
- JSON манифест с метаданными установки

**Когда использовать XAPK:**
- ✅ Приложения, которые не устанавливаются с ошибкой "INSTALL_FAILED_MISSING_SPLIT"
- ✅ Большие игры и приложения с поддержкой множественных архитектур
- ✅ Приложения, скачанные с APKCombo, APKPure или подобных магазинов
- ✅ Современные Android приложения использующие формат Android App Bundle

**Процесс установки XAPK:**
1. **Автоматическое определение:** WindowsLauncher распознает расширение .xapk
2. **Извлечение:** Распаковывает XAPK во временную директорию
3. **Парсинг манифеста:** Читает метаданные из manifest.json
4. **Умная установка:** 
   - Единичный APK: Использует стандартные методы установки
   - Множественные APK: Использует `adb install-multiple` или устанавливает по отдельности
5. **Очистка:** Удаляет временные файлы после установки

**Получение XAPK файлов:**
- [APKCombo](https://apkcombo.com/) - Скачивание XAPK файлов напрямую
- [APKPure](https://apkpure.com/) - Альтернативный источник XAPK файлов
- [APKMirror](https://www.apkmirror.com/) - Некоторые приложения доступны как APK bundles

### Распространенные проблемы и решения

#### Проблема: "Android функции скрыты/отключены"

**Симптомы:**
```
Опция Android APK отсутствует в типах приложений
Кнопка 🤖 Диагностика Android не видна
Индикатор статуса WSA показывает "Disabled"
```

**Решения:**
1. **Проверить конфигурацию AndroidSubsystem в appsettings.json:**
   ```json
   "AndroidSubsystem": {
     "Mode": "OnDemand"  // Изменить с "Disabled"
   }
   ```

2. **Перезапустить WindowsLauncher после изменения конфигурации**

3. **Проверить загрузку конфигурации:**
   - Проверить debug логи на "Android subsystem configured in {Mode} mode"
   - Режим должен быть "OnDemand" или "Preload", не "Disabled"

#### Проблема: "Android подсистема отключена конфигурацией"

**Симптомы:**
```
[DEBUG] Android support disabled by configuration
AndroidApplicationManager возвращает false для IsAndroidSupportAvailableAsync
```

**Решения:**
1. **Включить Android в конфигурации:**
   ```json
   "AndroidSubsystem": {
     "Mode": "OnDemand", // или "Preload"
     "AutoStartWSA": true
   }
   ```

2. **Проверить настройки порога памяти при использовании fallback:**
   ```json
   "Fallback": {
     "DisableOnLowMemory": false, // или увеличить MemoryThresholdMB
     "MemoryThresholdMB": 2048
   }
   ```

#### Проблема: "ADB команда не найдена в PATH"

**Симптомы:**
```
ERROR: ADB command not found in PATH
Android environment initialization failed
```

**Решения:**
1. Запустить скрипт установки от имени Администратора:
   ```powershell
   .\Scripts\Install-AndroidTools.ps1 -Force
   ```

2. Вручную проверить установку ADB:
   ```powershell
   # Проверить существует ли файл
   Test-Path "C:\WindowsLauncher\Tools\Android\platform-tools\adb.exe"
   
   # Добавить в PATH текущей сессии
   $env:PATH += ";C:\WindowsLauncher\Tools\Android\platform-tools"
   ```

3. Перезапустить WindowsLauncher после изменений PATH

#### Проблема: "WSA недоступен"

**Симптомы:**
```
WSA Available: False
WSA Running: False
```

**Решения:**
1. **Установить WSA из Microsoft Store**
2. **Включить режим разработчика:**
   - Пуск → Windows Subsystem for Android
   - Включить "Developer mode"
   - Разрешить исключения брандмауэра

3. **Проверить версию Windows:**
   ```powershell
   Get-ComputerInfo | Select WindowsProductName, WindowsVersion, WindowsBuildLabEx
   ```
   - Требуется Windows 11 Build 22000+

#### Проблема: "AAPT валидация не удалась"

**Симптомы:**
```
APK validation failed: Invalid APK format
Could not extract APK metadata
```

**Решения:**
1. **Проверить установку AAPT:**
   ```powershell
   aapt version
   # Должен выводить: Android Asset Packaging Tool, v0.2-...
   ```

2. **Переустановить build tools:**
   ```powershell
   .\Scripts\Install-AndroidTools.ps1 -PlatformToolsOnly -Force
   ```

3. **Проверить целостность APK файла:**
   - Убедиться что APK файл не поврежден
   - Попробовать с другим APK файлом
   - Проверить права доступа к файлу

#### Проблема: "Подключение к WSA не удалось"

**Симптомы:**
```
ADB Available: True
WSA Running: True
Failed to connect to WSA via ADB
```

**Решения:**
1. **Сбросить ADB подключение:**
   ```powershell
   adb kill-server
   adb start-server
   adb connect 127.0.0.1:58526
   ```

2. **Проверить режим разработчика WSA:**
   - Windows Subsystem for Android → Расширенные настройки
   - Убедиться что "Developer mode" ВКЛЮЧЕН
   - Отметить правильный IP:Port (обычно 127.0.0.1:58526)

3. **Брандмауэр и сеть:**
   - Разрешить ADB через брандмауэр Windows
   - Проверить не блокирует ли антивирус подключения
   - Перезапустить WSA: Настройки → Приложения → Windows Subsystem for Android → Дополнительные параметры → Сброс

#### Проблема: "Проблемы с кодировкой PowerShell"

**Симптомы:**
```
Искаженный русский текст в логах
Кракозябры вместо русского текста
```

**Решения:**
1. **Установить кодировку PowerShell:**
   ```powershell
   [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
   chcp 65001
   ```

2. **WindowsLauncher автоматически обрабатывает кодировку** - перезапустите приложение

#### Проблема: "INSTALL_FAILED_MISSING_SPLIT с APK файлами"

**Симптомы:**
```
adb.exe: failed to install app.apk: Failure [INSTALL_FAILED_MISSING_SPLIT: Missing split for com.example.app]
Split APK installation failed: All installation methods failed
```

**Решения:**
1. **Используйте XAPK вместо APK:**
   - Скачайте XAPK версию приложения с APKCombo или APKPure
   - XAPK файлы содержат все необходимые split APK файлы
   - WindowsLauncher автоматически обрабатывает установку XAPK

2. **Найдите Universal APK:**
   - Некоторые разработчики предоставляют "Universal APK" которые не требуют splits
   - Ищите APK файлы с пометкой "universal", "fat" или "all-architectures"

3. **Попробуйте старые версии приложения:**
   - Старые версии приложений часто не используют формат Android App Bundle
   - Проверьте APKMirror на старые APK версии

#### Проблема: "Установка XAPK не удалась"

**Симптомы:**
```
XAPK installation exception: Invalid XAPK: manifest.json not found
Multiple APK installation exception: install-multiple failed
```

**Решения:**
1. **Проверьте целостность XAPK файла:**
   - Перезагрузите XAPK файл из надежного источника
   - Проверьте размер файла соответствует ожидаемому размеру загрузки
   - Попробуйте открыть XAPK с помощью ZIP инструмента для проверки содержимого

2. **Проверьте содержимое XAPK:**
   ```powershell
   # XAPK это ZIP файл, вы можете проверить его:
   Rename-Item "app.xapk" "app.zip"
   # Извлеките и проверьте наличие manifest.json и APK файлов
   ```

3. **Ручная установка:**
   - Извлеките XAPK файл вручную
   - Установите APK файлы по отдельности используя ADB:
   ```powershell
   adb install "base.apk"
   adb install "config.arm64_v8a.apk"  
   adb install "config.en.apk"
   # Установите все APK файлы найденные в извлеченном XAPK
   ```

#### Проблема: "Автоматическая установка APK не работает"

**Симптомы:**
```
Application is not installed and APK file not found for automatic installation
Failed to automatically install APK: APK file not found in database
```

**Решения:**
1. **Проверьте путь к APK файлу:**
   - Убедитесь что APK/XAPK файл все еще существует по записанному пути
   - Проверьте был ли файл перемещен или удален после добавления в базу данных

2. **Переадобавьте приложение:**
   - Удалите приложение из WindowsLauncher
   - Добавьте его снова с правильным путем к APK/XAPK файлу

3. **Проверьте права доступа к файлу:**
   - Убедитесь что WindowsLauncher имеет доступ на чтение к APK/XAPK файлу
   - Переместите APK файлы в место доступное приложению

### Оптимизация производительности

#### Настройки производительности WSA

1. **Выделить больше ресурсов:**
   - Настройки → Windows Subsystem for Android → Расширенные
   - Увеличить выделение памяти если доступно
   - Включить аппаратное ускорение

2. **Закрыть неиспользуемые приложения:**
   - WSA запускает несколько приложений одновременно
   - Закрыть неиспользуемые Android приложения для освобождения ресурсов

#### Настройки WindowsLauncher

1. **Включить кэширование метаданных APK** (автоматически)
2. **Пакетная установка нескольких APK** вместо по одному
3. **Регулярная очистка временных файлов**

## Расширенная конфигурация

### Настройка корпоративной среды

Для развертывания в корпоративных средах:

1. **Предустановить Android инструменты:**
   ```powershell
   # Тихая установка без запросов пользователя
   .\Scripts\Install-AndroidTools.ps1 -Force -Confirm:$false
   ```

2. **Групповая политика для WSA:**
   - Развернуть WSA через WSUS или SCCM
   - Предварительно настроить режим разработчика через реестр:
     ```reg
     [HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Lxss]
     "EnableDeveloperMode"=dword:00000001
     ```

3. **Сетевые соображения:**
   - Разрешить ADB порт 58526 через корпоративный брандмауэр
   - Настроить прокси настройки если нужно

### Пользовательские расположения инструментов

WindowsLauncher ищет инструменты в следующем порядке:

1. **Системный PATH** (стандартная установка)
2. **Директории WindowsLauncher:**
   - `C:\WindowsLauncher\Tools\Android\platform-tools\`
   - `C:\WindowsLauncher\Tools\Android\android-14\`
3. **Расположения Android Studio:**
   - `%LOCALAPPDATA%\Android\Sdk\platform-tools\`
   - `%LOCALAPPDATA%\Android\Sdk\build-tools\{latest}\`

Для использования пользовательских расположений, добавьте их в системный PATH или разместите инструменты в директориях WindowsLauncher.

## Разработка и тестирование

### Тестирование Android интеграции

1. **Использовать образцы APK:**
   - Загрузить образцы APK с APKMirror или F-Droid
   - Начать с простых приложений (калькуляторы, заметки)
   - Избегать приложения требующие Google Play Services первоначально

2. **Отладка:**
   - Включить подробное логирование в WindowsLauncher
   - Использовать `adb logcat` для системных логов Android
   - Проверить Просмотрщик событий Windows на проблемы WSA

3. **Рабочий процесс разработки:**
   - Установить Android Studio для сборки APK
   - Использовать `adb install -r` для обновлений приложений
   - Тестировать как установку, так и запуск

### Логирование и мониторинг

WindowsLauncher обеспечивает подробное логирование:

```csharp
// Проверить логи в окне Output (Visual Studio) или консоли
[INFO] Android support is fully available (WSA + ADB)
[INFO] Installing Android APK: com.example.app v1.2.3
[INFO] Successfully installed Android app: com.example.app
[INFO] Launching Android app: com.example.app
```

## Соображения безопасности

### Безопасность APK

1. **Валидация источника:**
   - Устанавливать APK только из надежных источников
   - Проверять подписи APK когда возможно
   - Использовать антивирусное сканирование для APK файлов

2. **Управление разрешениями:**
   - Просматривать разрешения приложения перед установкой
   - WSA предоставляет систему разрешений Android
   - Мониторить сетевой доступ приложений

### Сетевая безопасность

1. **Безопасность ADB подключения:**
   - ADB подключение локальное (127.0.0.1)
   - Нет внешнего сетевого воздействия по умолчанию
   - Мониторить правила брандмауэра для ADB порта

2. **Корпоративные политики:**
   - Реализовать разрешенный/запрещенный список APK
   - Мониторить установленные Android приложения
   - Регулярные аудиты безопасности WSA окружения

## Поддержка и ресурсы

### Получение помощи

1. **Встроенная диагностика:** Используйте кнопку "🤖 Диагностика Android"
2. **Файлы логов:** Проверьте debug вывод WindowsLauncher
3. **Форумы сообщества:** Обсуждения сообщества Android WSA

### Полезные команды

```powershell
# Проверить статус WSA
Get-AppxPackage *WindowsSubsystemForAndroid*

# ADB отладка
adb devices
adb shell pm list packages
adb shell am start -n com.example.app/.MainActivity

# Версии инструментов
adb --version
aapt version

# Управление путями
echo $env:PATH
[Environment]::GetEnvironmentVariable("PATH", "Machine")
```

### Дополнительные ресурсы

- [Документация Windows Subsystem for Android](https://learn.microsoft.com/en-us/windows/android/wsa/)
- [Руководство Android Debug Bridge (ADB)](https://developer.android.com/studio/command-line/adb)
- [Android Asset Packaging Tool (AAPT)](https://developer.android.com/studio/command-line/aapt2)
- [Документация проекта WindowsLauncher](README.md)

---

**Последнее обновление:** Январь 2025
**Версия WindowsLauncher:** 1.2.0+
**Новые возможности:** 
- 🔄 Автоматическая установка APK/XAPK при первом запуске
- 📦 Полная поддержка XAPK с обработкой split APK  
- 🚀 Множественные методы установки с умным резервированием
- ⚙️ Конфигурируемые режимы Android подсистемы
- 📊 Улучшенное управление жизненным циклом WSA и мониторинг
**Минимальные требования:** 
- **Android функции:** Windows 10 Build 19041+ или Windows 11, WSA, Android SDK Tools
- **Полная поддержка:** Windows 11 рекомендуется для официальной поддержки WSA

*Этот документ является частью проекта WindowsLauncher. Для технической поддержки, пожалуйста, обратитесь к документации проекта или свяжитесь с командой разработки.*
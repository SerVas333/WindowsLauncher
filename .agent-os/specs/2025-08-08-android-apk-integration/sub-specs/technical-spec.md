# Technical Specification

This is the technical specification for the spec detailed in @.agent-os/specs/2025-08-08-android-apk-integration/spec.md

## Technical Requirements

### WSA Integration Architecture
- **Windows Subsystem for Android (WSA)** - базовая платформа для запуска Android приложений
- **WSABuilds integration** - использование предсобранных WSA образов (Apache 2.0 лицензия)
- **ADB (Android Debug Bridge)** - управление Android приложениями через команды adb
- **APK sideloading** - установка APK файлов без Google Play Store

### New Components Architecture

**WSAApplicationLauncher : IApplicationLauncher**
- Priority: 25 (выше DesktopApplicationLauncher но ниже TextEditor)
- SupportedType: ApplicationType.Android 
- CanLaunch() проверяет ExecutablePath.EndsWith(".apk")
- LaunchAsync() устанавливает и запускает APK через ADB команды

**WSAIntegrationService**
- Управление жизненным циклом WSA (запуск/остановка подсистемы)
- Выполнение ADB команд (установка APK, запуск приложений, получение списка)
- Мониторинг состояния Android приложений
- Извлечение метаданных из APK файлов (название, версия, иконка)

**AndroidApplicationManager** 
- Высокоуровневый API для управления Android приложениями
- Валидация APK файлов перед установкой
- Кэширование метаданных Android приложений
- Интеграция с ApplicationLifecycleService

### Database Schema Extensions
```sql
-- Расширение таблицы APPLICATIONS для поддержки Android
ALTER TABLE APPLICATIONS ADD COLUMN APK_PACKAGE_NAME VARCHAR(255) NULL;
ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_CODE INTEGER NULL; 
ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_NAME VARCHAR(100) NULL;
ALTER TABLE APPLICATIONS ADD COLUMN APK_MIN_SDK INTEGER NULL;
ALTER TABLE APPLICATIONS ADD COLUMN APK_FILE_PATH VARCHAR(500) NULL;

-- Индексы для быстрого поиска Android приложений
CREATE INDEX IDX_APPLICATIONS_APK_PACKAGE ON APPLICATIONS(APK_PACKAGE_NAME);
CREATE INDEX IDX_APPLICATIONS_TYPE_ANDROID ON APPLICATIONS(APPLICATION_TYPE_ID) 
WHERE APPLICATION_TYPE_ID = (SELECT ID FROM APPLICATION_TYPES WHERE TYPE_NAME = 'Android');
```

### UI/UX Integration Points

**AdminPanel Extensions**
- Новый тип приложения "Android" в выпадающем списке
- File picker для выбора APK файлов
- Автоматическое извлечение метаданных при загрузке APK
- Валидация APK файлов (подпись, целостность, совместимость)

**MainWindow Category Support**  
- Новая категория "Android" с эмодзи иконкой 🤖
- Отображение Android приложений с извлеченными иконками
- Интеграция в существующую систему фильтрации по категориям

**AppSwitcher Integration**
- Android приложения отображаются как ApplicationInstance
- ProcessId соответствует WSA процессу или adb процессу
- State отслеживается через ADB команды (adb shell dumpsys activity)

### Performance Optimization

**WSA Lifecycle Management**
- Ленивая инициализация WSA - запуск только при необходимости  
- Кэширование состояния WSA для избежания повторных проверок
- Graceful shutdown WSA при завершении WindowsLauncher

**APK Metadata Caching**
- Кэширование извлеченных метаданных APK в базе данных
- Проверка изменений APK файлов по хэшу или timestamp
- Фоновое обновление метаданных для больших APK файлов

### Error Handling & Fallback

**WSA Availability Check**
- Проверка наличия WSA в системе при запуске
- Graceful degradation - скрытие Android категории если WSA недоступен
- Информативные сообщения об ошибках для пользователя

**ADB Command Reliability**
- Retry механизм для ADB команд (timeout, network issues)
- Fallback на альтернативные команды при сбоях
- Детальное логирование всех ADB операций

## External Dependencies

**WSABuilds** (Apache 2.0) - https://github.com/MustardChef/WSABuilds
- **Purpose:** Предсобранные образы Windows Subsystem for Android
- **Justification:** Избегает необходимости самостоятельной сборки WSA, обеспечивает стабильность

**WSATools** (MIT) - https://github.com/Simizfo/WSATools  
- **Purpose:** C# библиотека для управления WSA и установки APK
- **Justification:** Готовая .NET интеграция, MIT лицензия позволяет коммерческое использование

**Android SDK Platform Tools** (Apache 2.0)
- **Purpose:** ADB (Android Debug Bridge) для управления Android приложениями
- **Justification:** Официальный инструмент Google для взаимодействия с Android устройствами

**AAPT (Android Asset Packaging Tool)** (Apache 2.0)
- **Purpose:** Извлечение метаданных из APK файлов (название, версия, иконки)
- **Justification:** Стандартный инструмент для анализа APK файлов
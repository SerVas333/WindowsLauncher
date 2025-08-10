# Database Schema

This is the database schema implementation for the spec detailed in @.agent-os/specs/2025-08-08-android-apk-integration/spec.md

## Database Changes

### New Application Type
```sql
-- Добавление нового типа приложения "Android"
INSERT INTO APPLICATION_TYPES (TYPE_NAME, DISPLAY_NAME, DESCRIPTION, ICON_TEXT) 
VALUES ('Android', 'Android APK', 'Android приложения (APK файлы)', '🤖');
```

### Extended APPLICATIONS Table Schema
```sql
-- Новые колонки для поддержки Android APK метаданных
-- SQLite версия
ALTER TABLE APPLICATIONS ADD COLUMN APK_PACKAGE_NAME TEXT;
ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_CODE INTEGER;  
ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_NAME TEXT;
ALTER TABLE APPLICATIONS ADD COLUMN APK_MIN_SDK INTEGER;
ALTER TABLE APPLICATIONS ADD COLUMN APK_TARGET_SDK INTEGER;
ALTER TABLE APPLICATIONS ADD COLUMN APK_FILE_PATH TEXT;
ALTER TABLE APPLICATIONS ADD COLUMN APK_FILE_HASH TEXT;
ALTER TABLE APPLICATIONS ADD COLUMN APK_INSTALL_STATUS TEXT DEFAULT 'NotInstalled';

-- Firebird версия (с правильным синтаксисом)
ALTER TABLE APPLICATIONS ADD APK_PACKAGE_NAME VARCHAR(255);
ALTER TABLE APPLICATIONS ADD APK_VERSION_CODE INTEGER;
ALTER TABLE APPLICATIONS ADD APK_VERSION_NAME VARCHAR(100);
ALTER TABLE APPLICATIONS ADD APK_MIN_SDK INTEGER;
ALTER TABLE APPLICATIONS ADD APK_TARGET_SDK INTEGER;
ALTER TABLE APPLICATIONS ADD APK_FILE_PATH VARCHAR(500);
ALTER TABLE APPLICATIONS ADD APK_FILE_HASH VARCHAR(64);
ALTER TABLE APPLICATIONS ADD APK_INSTALL_STATUS VARCHAR(50) DEFAULT 'NotInstalled';
```

### Performance Indexes
```sql
-- Индексы для быстрого поиска Android приложений
-- SQLite версия
CREATE INDEX IDX_APPLICATIONS_APK_PACKAGE ON APPLICATIONS(APK_PACKAGE_NAME);
CREATE INDEX IDX_APPLICATIONS_TYPE_ANDROID ON APPLICATIONS(APPLICATION_TYPE_ID);
CREATE INDEX IDX_APPLICATIONS_APK_STATUS ON APPLICATIONS(APK_INSTALL_STATUS);

-- Firebird версия  
CREATE INDEX IDX_APPLICATIONS_APK_PACKAGE ON APPLICATIONS(APK_PACKAGE_NAME);
CREATE INDEX IDX_APPLICATIONS_TYPE_ANDROID ON APPLICATIONS(APPLICATION_TYPE_ID);
CREATE INDEX IDX_APPLICATIONS_APK_STATUS ON APPLICATIONS(APK_INSTALL_STATUS);
```

### APK Installation Status Enum
```sql
-- Возможные статусы установки APK
-- 'NotInstalled' - APK файл загружен но не установлен в WSA
-- 'Installing' - процесс установки в WSA в процессе  
-- 'Installed' - APK успешно установлен и готов к запуску
-- 'Failed' - установка APK завершилась с ошибкой
-- 'Outdated' - установленная версия устарела, доступно обновление
```

## Migration Script

### Database Version Update
```sql
-- Обновление версии базы данных для Android support
-- Предполагаем что это версия 1.2.0.001 (новая minor версия)
UPDATE DATABASE_VERSION SET VERSION = '1.2.0.001', APPLIED_AT = CURRENT_TIMESTAMP;

INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
VALUES ('1.2.0.001', 'AddAndroidAPKSupport', 'Add Android APK application support with metadata fields', CURRENT_TIMESTAMP);
```

### Data Integrity Constraints
```sql
-- Ограничения целостности для Android приложений
-- SQLite версия (через CHECK constraints)
-- APK_PACKAGE_NAME должен быть заполнен для Android приложений
-- APK_FILE_PATH должен быть валидным путем к APK файлу
-- APK_VERSION_CODE должен быть положительным числом

-- Firebird версия (через CHECK constraints)
ALTER TABLE APPLICATIONS ADD CONSTRAINT CHK_APK_VERSION_CODE 
CHECK (APK_VERSION_CODE IS NULL OR APK_VERSION_CODE > 0);

ALTER TABLE APPLICATIONS ADD CONSTRAINT CHK_APK_INSTALL_STATUS
CHECK (APK_INSTALL_STATUS IN ('NotInstalled', 'Installing', 'Installed', 'Failed', 'Outdated'));
```

## Sample Data

### Example Android Application Entry
```sql
-- Пример записи Android приложения в базе данных
INSERT INTO APPLICATIONS (
    NAME, DESCRIPTION, EXECUTABLE_PATH, ARGUMENTS, ICON_TEXT,
    CATEGORY_ID, APPLICATION_TYPE_ID, IS_ENABLED, CREATED_BY_USER_ID,
    APK_PACKAGE_NAME, APK_VERSION_CODE, APK_VERSION_NAME, 
    APK_MIN_SDK, APK_TARGET_SDK, APK_FILE_PATH, APK_FILE_HASH,
    APK_INSTALL_STATUS
) VALUES (
    'Corporate Mobile App', 
    'Корпоративное мобильное приложение для сотрудников',
    'com.company.corpapp', -- Package name используется как executable path
    '', -- Arguments не нужны для APK
    '📱', -- Mobile app icon
    (SELECT ID FROM CATEGORIES WHERE NAME = 'Android'),
    (SELECT ID FROM APPLICATION_TYPES WHERE TYPE_NAME = 'Android'),
    1, -- Enabled
    (SELECT ID FROM USERS WHERE USERNAME = 'admin'),
    'com.company.corpapp', -- APK package name
    42, -- Version code
    '2.1.3', -- Version name  
    21, -- Min SDK (Android 5.0)
    34, -- Target SDK (Android 14)
    'C:\WindowsLauncher\APKs\corpapp-2.1.3.apk', -- File path
    'a1b2c3d4e5f6...', -- SHA-256 hash
    'NotInstalled' -- Initial status
);
```

## Rationale

### Why Extend APPLICATIONS Table
- **Консистентность** - Android приложения являются приложениями, логично хранить их в той же таблице
- **Переиспользование логики** - существующая система ролей, категорий, аудита работает автоматически  
- **Простота UI** - админ панель может использовать те же компоненты с минимальными изменениями

### APK-Specific Metadata Fields
- **APK_PACKAGE_NAME** - уникальный идентификатор Android приложения (com.company.app)
- **APK_VERSION_CODE/NAME** - версионирование для обновлений и совместимости
- **APK_MIN_SDK/TARGET_SDK** - проверка совместимости с WSA Android версией
- **APK_FILE_PATH** - путь к APK файлу для переустановки и обновлений
- **APK_FILE_HASH** - проверка целостности APK файла
- **APK_INSTALL_STATUS** - отслеживание состояния установки в WSA

### Performance Considerations  
- **Индексы по APK_PACKAGE_NAME** - быстрый поиск приложений по package name
- **Индекс по APPLICATION_TYPE_ID** - фильтрация Android приложений
- **Индекс по APK_INSTALL_STATUS** - поиск приложений требующих установки/обновления
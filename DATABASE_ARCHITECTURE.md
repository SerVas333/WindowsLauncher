# База данных WindowsLauncher - Полная архитектура

## Обзор архитектуры

WindowsLauncher использует **многопоставщическую архитектуру базы данных** с поддержкой:
- **SQLite** (основная, по умолчанию) - для локальных развертываний
- **Firebird** (опциональная) - для корпоративных развертываний

## Структура данных

### 1. Таблица USERS
**Основная модель:** `WindowsLauncher.Core.Models.User`
**Конфигурация:** `WindowsLauncher.Data.Configurations.UserConfiguration`

```sql
CREATE TABLE USERS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USERNAME NVARCHAR(100) NOT NULL UNIQUE,
    DISPLAY_NAME NVARCHAR(200),
    EMAIL NVARCHAR(320),
    ROLE INTEGER NOT NULL DEFAULT 0, -- UserRole enum
    IS_ACTIVE BOOLEAN NOT NULL DEFAULT 1,
    IS_SERVICE_ACCOUNT BOOLEAN NOT NULL DEFAULT 0,
    PASSWORD_HASH NVARCHAR(500),
    SALT NVARCHAR(500),
    CREATED_AT DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LAST_LOGIN_AT DATETIME,
    LAST_ACTIVITY_AT DATETIME,
    FAILED_LOGIN_ATTEMPTS INTEGER NOT NULL DEFAULT 0,
    IS_LOCKED BOOLEAN NOT NULL DEFAULT 0,
    LOCKOUT_END DATETIME,
    LAST_PASSWORD_CHANGE DATETIME,
    GROUPS_JSON NVARCHAR(2000) NOT NULL DEFAULT '[]',
    SETTINGS_JSON NVARCHAR(4000) NOT NULL DEFAULT '{}',
    METADATA_JSON NVARCHAR(2000) NOT NULL DEFAULT '{}',
    
    -- Гибридная авторизация
    AUTHENTICATION_TYPE INTEGER NOT NULL DEFAULT 0, -- AuthenticationType enum
    DOMAIN_USERNAME NVARCHAR(100),
    LAST_DOMAIN_SYNC DATETIME,
    IS_LOCAL_USER BOOLEAN NOT NULL DEFAULT 1,
    ALLOW_LOCAL_LOGIN BOOLEAN NOT NULL DEFAULT 0
);
```

**Индексы:**
- `IX_USERS_USERNAME` (UNIQUE)
- `IX_USERS_IS_ACTIVE`
- `IX_USERS_IS_SERVICE_ACCOUNT`
- `IX_USERS_AUTHENTICATION_TYPE`
- `IX_USERS_IS_LOCAL_USER`
- `IX_USERS_DOMAIN_USERNAME`
- `IX_USERS_LAST_DOMAIN_SYNC`

**Типы авторизации:**
```csharp
public enum AuthenticationType
{
    LocalService = 0,     // Локальный сервисный аккаунт
    DomainLDAP = 1,       // Активная LDAP авторизация
    WindowsSSO = 2,       // Windows SSO
    CachedDomain = 3      // Кэшированный доменный пользователь
}
```

**Роли пользователей:**
```csharp
public enum UserRole
{
    Standard = 0,    // Обычный пользователь
    PowerUser = 1,   // Расширенные права
    Admin = 2        // Администратор
}
```

### 2. Таблица APPLICATIONS
**Основная модель:** `WindowsLauncher.Core.Models.Application`
**Конфигурация:** `WindowsLauncher.Data.Configurations.ApplicationConfiguration`

```sql
CREATE TABLE APPLICATIONS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    NAME NVARCHAR(200) NOT NULL,
    DESCRIPTION NVARCHAR(500),
    EXECUTABLE_PATH NVARCHAR(1000) NOT NULL,
    ARGUMENTS NVARCHAR(500),
    WORKING_DIRECTORY NVARCHAR(1000),
    ICON_PATH NVARCHAR(1000),
    CATEGORY NVARCHAR(100) NOT NULL DEFAULT 'General',
    APP_TYPE INTEGER NOT NULL DEFAULT 0, -- ApplicationType enum
    MINIMUM_ROLE INTEGER NOT NULL DEFAULT 0, -- UserRole enum
    REQUIRED_GROUPS TEXT, -- JSON массив строк
    IS_ENABLED BOOLEAN NOT NULL DEFAULT 1,
    SORT_ORDER INTEGER NOT NULL DEFAULT 0,
    CREATED_DATE DATETIME NOT NULL,
    MODIFIED_DATE DATETIME NOT NULL,
    CREATED_BY NVARCHAR(100)
);
```

**Индексы:**
- `IX_APPLICATIONS_NAME`
- `IX_APPLICATIONS_CATEGORY`
- `IX_APPLICATIONS_IS_ENABLED`

**Типы приложений:**
```csharp
public enum ApplicationType
{
    Desktop = 0,    // Десктопное приложение
    Web = 1,        // Веб-ссылка
    Script = 2      // Скрипт/батник
}
```

### 3. Таблица USER_SETTINGS
**Основная модель:** `WindowsLauncher.Core.Models.UserSettings`
**Конфигурация:** `WindowsLauncher.Data.Configurations.UserSettingsConfiguration`

```sql
CREATE TABLE USER_SETTINGS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USER_ID INTEGER NOT NULL,
    THEME NVARCHAR(50) NOT NULL DEFAULT 'Light',
    ACCENT_COLOR NVARCHAR(50) NOT NULL DEFAULT 'Blue', 
    TILE_SIZE INTEGER NOT NULL DEFAULT 150,
    SHOW_CATEGORIES BOOLEAN NOT NULL DEFAULT 1,
    DEFAULT_CATEGORY NVARCHAR(100) NOT NULL DEFAULT 'All',
    HIDDEN_CATEGORIES TEXT, -- JSON массив строк
    AUTO_REFRESH BOOLEAN NOT NULL DEFAULT 1,
    REFRESH_INTERVAL_MINUTES INTEGER NOT NULL DEFAULT 30,
    SHOW_DESCRIPTIONS BOOLEAN NOT NULL DEFAULT 1,
    UPDATED_AT DATETIME NOT NULL,
    
    FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE CASCADE
);
```

**Индексы:**
- `IX_USER_SETTINGS_USER_ID` (UNIQUE)

### 4. Таблица AUDIT_LOGS
**Основная модель:** `WindowsLauncher.Core.Models.AuditLog`
**Конфигурация:** `WindowsLauncher.Data.Configurations.AuditLogConfiguration`

```sql
CREATE TABLE AUDIT_LOGS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    USER_ID INTEGER, -- FK to USERS (nullable для системных действий)
    USERNAME NVARCHAR(100) NOT NULL,
    ACTION NVARCHAR(100) NOT NULL,
    APPLICATION_NAME NVARCHAR(200),
    DETAILS NVARCHAR(2000),
    TIMESTAMP_UTC DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SUCCESS BOOLEAN NOT NULL DEFAULT 1,
    ERROR_MESSAGE NVARCHAR(1000),
    COMPUTER_NAME NVARCHAR(100) NOT NULL,
    IP_ADDRESS NVARCHAR(45), -- Поддержка IPv6
    USER_AGENT NVARCHAR(500),
    METADATA_JSON NVARCHAR(2000) NOT NULL DEFAULT '{}',
    
    FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE SET NULL
);
```

**Индексы:**
- `IX_AUDIT_LOGS_USERNAME`
- `IX_AUDIT_LOGS_ACTION`
- `IX_AUDIT_LOGS_TIMESTAMP_UTC`
- `IX_AUDIT_LOGS_USERNAME_TIMESTAMP` (composite)

**Навигационные свойства:**
- `User.UserSettings` → `UserSettings` (1:1, через USER_ID FK)
- `UserSettings.User` → `User` (через USER_ID FK) 
- `User.AuditLogs` → `ICollection<AuditLog>` (через USER_ID FK)
- `AuditLog.User` → `User` (optional, через USER_ID FK)

## Конфигурация подключения

### DatabaseConfiguration
**Модель:** `WindowsLauncher.Core.Models.DatabaseConfiguration`

```csharp
public class DatabaseConfiguration
{
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SQLite;
    public FirebirdConnectionMode ConnectionMode { get; set; } = FirebirdConnectionMode.Embedded;
    public string DatabasePath { get; set; } = "launcher.db";
    public string? Server { get; set; } = "localhost";
    public int Port { get; set; } = 3050;
    public string Username { get; set; } = "SYSDBA";
    public string Password { get; set; } = "masterkey";
    public int Dialect { get; set; } = 3;
    public int PageSize { get; set; } = 8192;
    public string Charset { get; set; } = "UTF8";
    public int ConnectionTimeout { get; set; } = 30;
}
```

### Строки подключения

**SQLite:**
```
Data Source={DatabasePath};
```

**Firebird Embedded:**
```
database={DatabasePath};user={Username};password={Password};dialect={Dialect};charset={Charset};connection timeout={ConnectionTimeout};servertype=1
```

**Firebird Client-Server:**
```
database={Server}/{Port}:{DatabasePath};user={Username};password={Password};dialect={Dialect};charset={Charset};connection timeout={ConnectionTimeout}
```

## Entity Framework конфигурация

### LauncherDbContext
**Файл:** `WindowsLauncher.Data.LauncherDbContext`

**DbSets:**
- `Users` → `User` (таблица USERS)
- `Applications` → `Application` (таблица APPLICATIONS)
- `UserSettings` → `UserSettings` (таблица USER_SETTINGS)
- `AuditLogs` → `AuditLog` (таблица AUDIT_LOGS)

**Особенности:**
- **Унифицированные UPPERCASE имена** для всех БД (SQLite и Firebird)
- **Единая конфигурация** без условной логики по типу БД
- **Стандартные FK связи** через USER_ID вместо текстовых ключей
- **ApplyUniversalConfiguration** обеспечивает UPPERCASE во всех именах

## Сервисы инициализации

### DatabaseVersionService
**Файл:** `WindowsLauncher.Services.DatabaseVersionService`
**Функции:**
- Проверка и создание таблицы `__DatabaseVersion`
- Контроль версий схемы БД
- Синхронизация с версией приложения

### DatabaseSeeder
**Файл:** `WindowsLauncher.Data.DatabaseSeeder`
**Функции:**
- Заполнение начальными данными (Calculator, Notepad, Google, Control Panel)
- Проверка существования данных перед добавлением

### DatabaseMigrationContext
**Файл:** `WindowsLauncher.Data.Services.DatabaseMigrationContext`
**Функции:**
- Кросс-БД проверка существования таблиц/колонок/индексов
- Выполнение SQL команд для миграций
- Адаптация SQL под SQLite/Firebird

## Размещение файлов БД

### SQLite (по умолчанию)
**Путь:** `%AppData%\\WindowsLauncher\\launcher.db`

### Конфигурация
**Путь:** `%AppData%\\WindowsLauncher\\database-config.json`

## Особенности безопасности

### Пароли и хеширование
- **Алгоритм:** PBKDF2 с SHA-256
- **Соль:** Уникальная для каждого пароля
- **Итерации:** 10,000+ (настраивается)

### Аудит безопасности
- **Все действия пользователей** логируются в AuditLogs
- **Неудачные попытки входа** отслеживаются и блокируются
- **IP адреса и User-Agent** сохраняются для анализа

### Авторизация приложений
- **Минимальная роль** (`MinimumRole`)
- **Обязательные группы** (`RequiredGroups`) 
- **Логический AND** для групп (все должны совпадать)

## Совместимость с Entity Framework 9.0

### Исправленные проблемы
1. **SqlQueryRaw<T>** заменен на **ExecuteScalarAsync** для скалярных значений
2. **Корректная работа** с новым синтаксисом EF Core 9.0
3. **Правильная обработка** параметризованных запросов

### Рекомендации
- **Использовать ExecuteScalarAsync** для COUNT(*) и простых значений  
- **Избегать SqlQueryRaw<int>** для скалярных типов
- **Предпочитать LINQ** где это возможно

## Настройки производительности

### Индексы
- **Все внешние ключи** проиндексированы
- **Часто используемые поля** (Username, Timestamp, IsActive) проиндексированы
- **Композитные индексы** для сложных запросов

### Connection Pooling
- **SQLite:** Автоматический через EF Core
- **Firebird:** Настраивается через строку подключения

### Оптимизация запросов
- **AsNoTracking()** для read-only операций
- **Batch операции** для массовых изменений
- **Lazy loading отключен** по умолчанию

## Миграции и обновления

### Стратегия миграций
1. **Проверка версии БД** при запуске
2. **Автоматическое создание** новых таблиц если нужно  
3. **Ручные миграции** для сложных изменений схемы
4. **Rollback не поддерживается** - только forward миграции

### Резервное копирование
- **SQLite:** Простое копирование файла
- **Firebird:** gbak утилита или SQL BACKUP

## Устранение неисправностей

### Частые проблемы
1. **База заблокирована** → Перезапустить приложение или удалить .db файл
2. **Таблицы не созданы** → Удалить БД, перезапустить приложение  
3. **Ошибки миграций** → Проверить права доступа к папке AppData
4. **SqlQueryRaw ошибки** → Обновлены в DatabaseVersionService для EF Core 9.0

### Диагностика
- **Логи:** WindowsLauncher.UI Debug output window
- **Конфигурация:** database-config.json в AppData
- **Состояние БД:** Проверить через DatabaseVersionService

---

**Последнее обновление:** 2024-01-23  
**Entity Framework версия:** 9.0.6  
**Поддерживаемые БД:** SQLite 3.x, Firebird 3.0+
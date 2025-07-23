# База данных WindowsLauncher - Полная архитектура

## Обзор архитектуры

WindowsLauncher использует **многопоставщическую архитектуру базы данных** с поддержкой:
- **SQLite** (основная, по умолчанию) - для локальных развертываний
- **Firebird** (опциональная) - для корпоративных развертываний

## Структура данных

### 1. Таблица Users
**Основная модель:** `WindowsLauncher.Core.Models.User`
**Конфигурация:** `WindowsLauncher.Data.Configurations.UserConfiguration`

```sql
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    DisplayName NVARCHAR(200),
    Email NVARCHAR(255),
    Role INTEGER NOT NULL DEFAULT 0, -- UserRole enum
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    IsServiceAccount BOOLEAN NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(500),
    Salt NVARCHAR(500),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt DATETIME,
    LastActivityAt DATETIME,
    FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
    IsLocked BOOLEAN NOT NULL DEFAULT 0,
    LockoutEnd DATETIME,
    LastPasswordChange DATETIME,
    GroupsJson NVARCHAR(2000) NOT NULL DEFAULT '[]',
    SettingsJson NVARCHAR(4000) NOT NULL DEFAULT '{}',
    MetadataJson NVARCHAR(2000) NOT NULL DEFAULT '{}',
    
    -- Гибридная авторизация (новое)
    AuthenticationType INTEGER NOT NULL DEFAULT 0, -- AuthenticationType enum
    DomainUsername NVARCHAR(100),
    LastDomainSync DATETIME,
    IsLocalUser BOOLEAN NOT NULL DEFAULT 1,
    AllowLocalLogin BOOLEAN NOT NULL DEFAULT 0
);
```

**Индексы:**
- `IX_Users_Username` (UNIQUE)
- `IX_Users_IsActive`
- `IX_Users_IsServiceAccount`
- `IX_Users_AuthenticationType`
- `IX_Users_IsLocalUser`
- `IX_Users_DomainUsername`
- `IX_Users_LastDomainSync`

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

### 2. Таблица Applications
**Основная модель:** `WindowsLauncher.Core.Models.Application`
**Конфигурация:** `WindowsLauncher.Data.Configurations.ApplicationConfiguration`

```sql
CREATE TABLE Applications (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500),
    ExecutablePath NVARCHAR(1000) NOT NULL,
    Arguments NVARCHAR(500),
    IconPath NVARCHAR(1000),
    Category NVARCHAR(100) NOT NULL DEFAULT 'General',
    Type INTEGER NOT NULL DEFAULT 0, -- ApplicationType enum
    MinimumRole INTEGER NOT NULL DEFAULT 0, -- UserRole enum
    RequiredGroups TEXT, -- JSON массив строк
    IsEnabled BOOLEAN NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedDate DATETIME NOT NULL,
    ModifiedDate DATETIME NOT NULL,
    CreatedBy NVARCHAR(100)
);
```

**Индексы:**
- `IX_Applications_Name`
- `IX_Applications_Category`
- `IX_Applications_IsEnabled`

**Типы приложений:**
```csharp
public enum ApplicationType
{
    Desktop = 0,    // Десктопное приложение
    Web = 1,        // Веб-ссылка
    Script = 2      // Скрипт/батник
}
```

### 3. Таблица UserSettings
**Основная модель:** `WindowsLauncher.Core.Models.UserSettings`
**Конфигурация:** `WindowsLauncher.Data.Configurations.UserSettingsConfiguration`

```sql
CREATE TABLE UserSettings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Theme NVARCHAR(50) NOT NULL DEFAULT 'Light',
    AccentColor NVARCHAR(50) NOT NULL DEFAULT 'Blue',
    TileSize INTEGER NOT NULL DEFAULT 150,
    ShowCategories BOOLEAN NOT NULL DEFAULT 1,
    DefaultCategory NVARCHAR(100) NOT NULL DEFAULT 'All',
    HiddenCategories TEXT, -- JSON массив строк
    AutoRefresh BOOLEAN NOT NULL DEFAULT 1,
    RefreshIntervalMinutes INTEGER NOT NULL DEFAULT 30,
    ShowDescriptions BOOLEAN NOT NULL DEFAULT 1,
    LastModified DATETIME NOT NULL
);
```

**Индексы:**
- `IX_UserSettings_Username` (UNIQUE)

### 4. Таблица AuditLogs
**Основная модель:** `WindowsLauncher.Core.Models.AuditLog`
**Конфигурация:** `WindowsLauncher.Data.Configurations.AuditLogConfiguration`

```sql
CREATE TABLE AuditLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER, -- FK to Users (nullable для системных действий)
    Username NVARCHAR(100) NOT NULL,
    Action NVARCHAR(100) NOT NULL,
    ApplicationName NVARCHAR(200),
    Details NVARCHAR(2000),
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Success BOOLEAN NOT NULL DEFAULT 1,
    ErrorMessage NVARCHAR(1000),
    ComputerName NVARCHAR(100) NOT NULL,
    IPAddress NVARCHAR(45), -- Поддержка IPv6
    UserAgent NVARCHAR(500),
    MetadataJson NVARCHAR(2000) NOT NULL DEFAULT '{}'
);
```

**Индексы:**
- `IX_AuditLogs_Username`
- `IX_AuditLogs_Action`
- `IX_AuditLogs_Timestamp`
- `IX_AuditLogs_Username_Timestamp` (composite)

**Навигационные свойства:**
- `AuditLog.User` → `User` (optional, через UserId)
- `User.AuditLogs` → `ICollection<AuditLog>`
- `User.UserSettings` → `UserSettings` (optional)

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
- `Users` → `User`
- `Applications` → `Application`  
- `UserSettings` → `UserSettings`
- `AuditLogs` → `AuditLog`

**Особенности:**
- **Динамическая конфигурация БД** через `IDatabaseConfigurationService`
- **Адаптация к типу БД** (SQLite/Firebird) в `OnModelCreating`
- **Firebird специфика:** UPPERCASE имена таблиц/колонок, специальные типы данных
- **SQLite специфика:** стандартные настройки EF Core

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
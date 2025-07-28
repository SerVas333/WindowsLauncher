# CLAUDE.md

Инструкции для работы с WindowsLauncher в Visual Studio 2022

## Обзор проекта

WindowsLauncher — современное корпоративное WPF приложение на .NET 8.0 для централизованного управления приложениями с интеграцией Active Directory и Material Design интерфейсом.

### Архитектура решения

```
WindowsLauncher.sln (Visual Studio 2022)
├── WindowsLauncher.Core      # Модели, интерфейсы, перечисления
├── WindowsLauncher.Data      # Entity Framework, репозитории  
├── WindowsLauncher.Services  # Бизнес-логика, AD интеграция
└── WindowsLauncher.UI        # WPF интерфейс (стартовый проект)
```

**Паттерны:** Clean Architecture, Repository, MVVM, Dependency Injection

## Сборка и запуск в Visual Studio

### Конфигурации сборки
- **Debug** — для разработки с отладочной информацией
- **Release** — для продакшн сборки

### Основные команды IDE

#### Сборка проекта
- **Build Solution** (`Ctrl+Shift+B`) — полная сборка решения
- **Rebuild Solution** — пересборка с очисткой
- **Build → Clean Solution** — очистка артефактов сборки

#### Запуск и отладка
- **Start Debugging** (`F5`) — запуск с отладчиком
- **Start Without Debugging** (`Ctrl+F5`) — запуск без отладки
- **Стартовый проект:** `WindowsLauncher.UI`

#### Альтернативная очистка
```powershell
# Из корня проекта в PowerShell
.\clean-build.ps1
```

### База данных

**📋 Подробная документация по архитектуре БД:** См. [DATABASE_ARCHITECTURE.md](DATABASE_ARCHITECTURE.md)

## Фреймворки и технологии

### Основной стек
- **.NET 8.0-windows** — целевая платформа
- **WPF** — десктоп UI фреймворк
- **Entity Framework Core 9.0.6** — ORM (SQLite + Firebird)
- **MaterialDesignThemes 5.2.1** — Modern UI компоненты
- **Microsoft.Extensions.*** — DI, Logging, Configuration

### Базы данных
- **Многопоставщическая архитектура:** SQLite (основная) + Firebird (корпоративная)
- **Подробности:** см. [DATABASE_ARCHITECTURE.md](DATABASE_ARCHITECTURE.md)

### Интеграции
- **Active Directory** — корпоративная аутентификация
- **System.DirectoryServices** — LDAP интеграция

## Система управления версиями БД

### Архитектура версионирования

**Версионирование:** Семантическое `MAJOR.MINOR.PATCH.BUILD` (например, `1.0.0.001`)

**Структура миграций:**
```
WindowsLauncher.Data/Migrations/
├── v1.0.0.001_InitialSchema.cs     # Базовая схема версии 1.0.0
├── v1.0.0.002_AddNewFeature.cs     # Следующие инкрементальные изменения
├── v1.1.0.001_MajorUpdate.cs       # Большие обновления
└── v2.0.0.001_BreakingChanges.cs   # Критические изменения API
```

### Таблицы метаданных версионирования

**DATABASE_VERSION** — текущая версия БД:
```sql
CREATE TABLE DATABASE_VERSION (
    VERSION VARCHAR(20) PRIMARY KEY,        -- Версия БД (1.0.0.001)
    APPLIED_AT TIMESTAMP NOT NULL,          -- Время применения
    APPLICATION_VERSION VARCHAR(20)         -- Совместимая версия приложения
);
```

**MIGRATION_HISTORY** — полная история миграций:
```sql
CREATE TABLE MIGRATION_HISTORY (
    ID INTEGER PRIMARY KEY,
    VERSION VARCHAR(20) NOT NULL,           -- Версия миграции
    NAME VARCHAR(200) NOT NULL,             -- Имя миграции
    DESCRIPTION TEXT,                       -- Описание изменений
    APPLIED_AT TIMESTAMP NOT NULL,          -- Время применения
    ROLLBACK_SCRIPT TEXT                    -- SQL для отката (будущее)
);
```

### Сервисы версионирования

**IApplicationVersionService** — управление совместимостью:
```csharp
GetApplicationVersion()                    // Версия приложения из Assembly
GetDatabaseVersionAsync()                  // Текущая версия БД
SetDatabaseVersionAsync(version)           // Обновление версии БД
IsDatabaseCompatibleAsync()                // Проверка совместимости
IsDatabaseInitializedAsync()               // Проверка инициализации БД
```

**IDatabaseMigrationService** — применение миграций:
```csharp
GetAllMigrations()                         // Список всех миграций
GetPendingMigrationsAsync()               // Неприменённые миграции
MigrateAsync()                            // Применение ожидающих миграций
IsDatabaseUpToDateAsync()                 // Проверка актуальности схемы
```

### Процесс инициализации БД

**При первом запуске приложения:**
1. **Проверка инициализации** — `IsDatabaseInitializedAsync()`
2. **Применение базовой миграции** — `v1.0.0.001_InitialSchema.cs`
3. **Создание полной схемы** — все таблицы, индексы, начальные данные
4. **Установка версии** — запись в `DATABASE_VERSION`

**При последующих запусках:**
1. **Проверка совместимости** — `IsDatabaseCompatibleAsync()`
2. **Применение новых миграций** — инкрементальные обновления
3. **Обновление версии** — при успешном применении миграций

### Схема БД версии 1.0.0.001

**Основные таблицы:**
- **USERS** — пользователи с ролями и AD интеграцией
- **APPLICATIONS** — приложения с эмодзи иконками (`ICONTEXT` поле)  
- **USER_SETTINGS** — персональные настройки UI
- **AUDIT_LOGS** — журнал действий пользователей
- **DATABASE_VERSION** — текущая версия БД
- **MIGRATION_HISTORY** — история применённых миграций

**Совместимость БД:**
- **SQLite** — основная БД для разработки и малых установок
- **Firebird** — корпоративная БД для production окружений
- **UPPERCASE имена** — таблицы и колонки для универсальности

### Первоначальная настройка (важно!)

**⚠️ Для первого запуска с новой системой версионирования:**

1. **Удалите старую базу данных:**
   ```bash
   # Удалить SQLite БД
   rm "%AppData%\WindowsLauncher\launcher.db"
   
   # Удалить конфигурацию БД
   rm "%AppData%\WindowsLauncher\database-config.json"
   ```

2. **Первый запуск:**
   - Приложение автоматически создаст БД версии `1.0.0.001`
   - Применится миграция `InitialSchema` с полной схемой
   - Добавятся начальные данные (пользователь `guest`, базовые приложения)

3. **Проверка успешной инициализации:**
   ```sql
   SELECT * FROM DATABASE_VERSION;  -- Должна показать версию 1.0.0.001
   SELECT * FROM MIGRATION_HISTORY; -- История применённых миграций
   SELECT * FROM APPLICATIONS;      -- Базовые приложения с эмодзи иконками
   ```

### Развертывание Firebird БД

**Автоматизированное развертывание:**
```powershell
# Embedded режим (автономная установка)
.\Scripts\Deploy-FirebirdDatabase.ps1 -Mode Embedded

# Server режим (корпоративная установка)
.\Scripts\Deploy-FirebirdDatabase.ps1 -Mode Server -ServerHost "fb-server.local" -CreateBackup
```

**Ручное развертывание:**

**Firebird Embedded:**
```bash
# 1. Создание embedded БД
isql -i Scripts\create_firebird_embedded_v1.0.0.001.sql

# 2. Конфигурация приложения
{
  "DatabaseType": "Firebird",
  "DatabasePath": "C:\\WindowsLauncher\\Data\\launcher_embedded.fdb",
  "Username": "SYSDBA",
  "Password": "KDV_Launcher_2025!",
  "ConnectionMode": "Embedded"
}
```

**Firebird Server:**
```bash
# 1. Создание пользователя БД
isql localhost:security3.fdb -user SYSDBA -password your_password
CREATE USER KDV_LAUNCHER PASSWORD 'KDV_L@unch3r_S3cur3_2025!';

# 2. Создание server БД  
isql -i Scripts\create_firebird_server_v1.0.0.001.sql

# 3. Конфигурация приложения
{
  "DatabaseType": "Firebird",
  "Server": "localhost",
  "Port": 3050,
  "DatabasePath": "C:\\FirebirdData\\launcher_server.fdb",
  "Username": "KDV_LAUNCHER", 
  "Password": "KDV_L@unch3r_S3cur3_2025!",
  "ConnectionMode": "Server"
}
```

**Требования безопасности:**
- **Пароли:** Минимум 12 символов, смешанный регистр, цифры, спецсимволы
- **Пользователи:** Отдельный пользователь для приложения (НЕ SYSDBA)
- **Сеть:** Firewall настройки для порта 3050 (server режим)
- **SSL/TLS:** Рекомендуется для удаленных подключений

### Разработка новых миграций

**Создание новой миграции:**
```csharp
public class AddNotificationSystem : IDatabaseMigration
{
    public string Name => "AddNotificationSystem";
    public string Version => "1.1.0.001";  // Инкремент версии
    public string Description => "Add notification tables and triggers";
    
    public async Task UpAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
    {
        // Проверяем что изменение ещё не применено
        bool tableExists = await context.TableExistsAsync("NOTIFICATIONS");
        if (tableExists) return;
        
        // SQL для создания новых таблиц/колонок
        string sql = databaseType switch { /* ... */ };
        await context.ExecuteSqlAsync(sql);
    }
    
    public async Task DownAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
    {
        // SQL для отката изменений (future feature)
    }
}
```

**Регистрация в DatabaseMigrationService:**
```csharp
_migrations = new List<IDatabaseMigration>
{
    new InitialSchema(),           // v1.0.0.001
    new AddNotificationSystem()    // v1.1.0.001
};
```

### Рекомендации по версионированию

**Правила инкрементации версий:**
- **PATCH (1.0.0.xxx)** — мелкие изменения, добавление колонок, индексов
- **MINOR (1.x.0.001)** — новые таблицы, функциональность, обратно совместимые изменения  
- **MAJOR (x.0.0.001)** — критические изменения, несовместимые с предыдущими версиями

**Безопасность миграций:**
- Всегда проверять существование объектов перед созданием
- Использовать транзакции для критических изменений
- Тестировать на копиях production данных
- Сохранять скрипты отката для критических миграций

## Корпоративный дизайн-система

### Архитектура стилей

**Иерархия стилевых файлов:**
```
WindowsLauncher.UI/Styles/
├── MaterialColors.xaml     # Корпоративная цветовая палитра KDV
├── MaterialStyles.xaml     # Material Design базовые стили
└── CorporateStyles.xaml    # Кастомные корпоративные элементы
```

**Порядок подключения в App.xaml:** Цвета → Material → Корпоративные стили

### Корпоративная цветовая схема KDV

**Основные цвета:**
- **Корпоративный красный:** `#C41E3A` (CorporateRed/PrimaryBrush) 
- **Темно-красный:** `#A01729` (CorporateRedDark) - для активных состояний
- **Светло-красный:** `#E8324F` (CorporateRedLight) - для hover эффектов
- **Градиент заголовка:** Красный → Темно-красный

**Material Design палитра:**
- **Поверхности:** Белый (#FFFFFF), Светло-серый (#F5F5F5)
- **Текст:** Темно-серый (#333333), Средне-серый (#666666)
- **Границы:** Светло-серый (#DDDDDD), Очень светлый (#EEEEEE)

### Система стилей и компонентов

**Кнопки:**
```xml
CorporateButton              <!-- Основная красная кнопка -->
CorporateButtonSecondary     <!-- Контурная кнопка -->
CorporateCategoryButton      <!-- Скругленные кнопки категорий -->
CorporateButtonDanger        <!-- Кнопка удаления (красная) -->
```

**Формы и поля ввода:**
```xml
CorporateSearchBox           <!-- Поле поиска (radius: 25px) -->
MaterialTextBoxStyle         <!-- Material Design текстовые поля -->
MaterialComboBoxStyle        <!-- Выпадающие списки -->
MaterialCheckBoxStyle        <!-- Чекбоксы с анимацией -->
```

**Контейнеры и карточки:**
```xml
CorporateAppCard            <!-- Карточки приложений (radius: 8px) -->
LocalUserCard               <!-- Карточки пользователей -->
CorporateHeaderGradient     <!-- Градиентный заголовок -->
```

### Типографика

**Шрифт:** Segoe UI (система Windows)

**Стили текста:**
- **CorporateTitle:** 24px, Light - заголовки окон
- **CorporateSubtitle:** 14px, Normal - подзаголовки секций  
- **CorporateBodyText:** 13px, Normal - основной текст
- **Статус и роли:** 9-12px, SemiBold - метки и бейджи

### Брендинг и символика

**Корпоративная идентичность:**
- **Логотип:** KDV.png (150x40px) в заголовках
- **Иконка приложения:** KDV_icon.ico
- **Название:** "KDV Corporate Portal" / "Корпоративный портал KDV"

**Векторные ресурсы:**
- Встроенные SVG-подобные пути для иконок
- Emoji для системных значков (⚙️, 🔧, 📋, etc.)

### UI Конвертеры

**Основные конвертеры (UIConverters.cs):**
```csharp
BooleanToVisibilityConverter     // Bool → Visibility
RoleToVisibilityConverter        // UserRole → доступность элементов  
FirstLetterValueConverter        // String → первая буква (аватары)
EqualityToBooleanConverter       // Сравнение для выделения
BoolToStringConverter           // Bool → локализованный текст
```

### Специальные элементы

**Пользовательские контролы:**
- **LocalUserCard** - Полная карточка пользователя с аватаром, статусом, ролью
- **UserStatusDot** - Цветовой индикатор активности пользователя
- **UserRoleBadge** - Цветные значки ролей (Admin, PowerUser, Standard)

**Система состояний:**
- **Активен:** Зеленый (#4CAF50)
- **Заблокирован:** Красный (#F44336) 
- **Предупреждение:** Оранжевый (#FF9800)
- **Информация:** Синий (#2196F3)

### Работа с корпоративными стилями в Visual Studio

**Редактирование стилей:**
1. **Solution Explorer** → `WindowsLauncher.UI/Styles/`
2. **XAML Designer** для визуального редактирования
3. **Properties Window** для быстрого изменения свойств

**Предварительный просмотр:**  
- **Design View** в XAML редакторе
- **Live Visual Tree** при отладке (`Ctrl+Shift+Y`)
- **Live Property Explorer** для runtime изменений

**Добавление новых стилей:**
```xml
<!-- В CorporateStyles.xaml -->
<Style x:Key="NewCorporateStyle" TargetType="Button" BasedOn="{StaticResource CorporateButton}">
    <Setter Property="Background" Value="{StaticResource CorporateRedBrush}"/>
    <!-- Дополнительные свойства -->
</Style>
```

**Использование в XAML:**
```xml
<Button Style="{StaticResource NewCorporateStyle}" Content="Кнопка"/>
```

### Material Design интеграция

**Пакет:** MaterialDesignThemes v5.2.1

**Основные принципы:**
- **Elevation** - тени для создания глубины
- **Motion** - плавные анимации переходов  
- **Typography** - консистентная типографика
- **Color** - осмысленная цветовая система

**Кастомизация Material Design:**
- Корпоративные цвета вместо стандартных Material
- Скругления 4-8px вместо стандартных радиусов
- Корпоративные шрифты и размеры

### Локализация брендинга

**Многоязычная поддержка:**
- **EN:** "KDV Corporate Portal"
- **RU:** "Корпоративный портал KDV"
- Консистентный брендинг во всех языках
- Локализованные описания ролей и статусов

## Тестирование

### Встроенные инструменты отладки
- **Окно вывода** → "WindowsLauncher.UI Debug" для логов
- **Immediate Window** (`Ctrl+Alt+I`) для выполнения кода
- **Locals/Autos** для просмотра переменных

### Вспомогательные классы
- `ADTestService.cs` — тестирование AD подключения
- `TestPassword.cs` — утилиты для паролей

### Тестирование в Debug режиме
- Точки останова в коде
- **Debug → Windows → Output** для просмотра логов

## Конфигурационные файлы

### `appsettings.json` (основной)
```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "ActiveDirectory": { "Domain": "kdvm.ru", ... },
  "Logging": { "LogLevel": { ... } },
  "Application": { "Name": "Windows Launcher", ... },
  "UI": { "Theme": "Light", "Language": "ru-RU", ... }
}
```

### `app.manifest`
- **UAC Level:** asInvoker (не требует админ прав)
- **DPI Awareness:** PerMonitorV2
- **OS Support:** Windows 7-11

### `Settings.settings` (130+ параметров)
- Пользовательские настройки UI
- Доступ через **Project Properties → Settings**


## Соглашения код-стайла

### Именование
- **Классы:** PascalCase (`UserRepository`)
- **Методы:** PascalCase (`GetUserAsync`) 
- **Поля:** _camelCase (`_logger`)
- **Свойства:** PascalCase (`IsEnabled`)
- **Локальные переменные:** camelCase (`userId`)

### Архитектурные принципы
- **Async/await** для всех I/O операций
- **ILogger** для логирования вместо Console
- **Dependency Injection** через конструктор
- **Repository Pattern** для доступа к данным
- **MVVM Commands** для UI логики

### Структура файлов
```
ViewModels/
├── Base/ViewModelBase.cs     # Базовый класс для MVVM
├── MainViewModel.cs          # Основная логика UI
└── AdminViewModel.cs         # Административная панель

Services/
├── ActiveDirectory/          # AD интеграция
├── Authentication/           # Аутентификация
└── Applications/             # Управление приложениями
```

## Переменные окружения и параметры

### Важные пути
- **Логи:** Console + Debug Output (настраивается в appsettings.json)
- **База данных:** см. [DATABASE_ARCHITECTURE.md](DATABASE_ARCHITECTURE.md)

### Режимы запуска
- **Debug:** Full logging, исключения показываются пользователю
- **Release:** Minimal logging, graceful error handling

### Настройки AD (опциональные)
- **DOMAIN** — Active Directory домен
- **LDAP_SERVER** — адрес LDAP сервера
- **AD_GROUP_MAPPING** — маппинг AD групп на роли

## CI/CD и Visual Studio

### ClickOnce публикация
1. **Project Properties → Publish**
2. **Publish Wizard** для настройки
3. **Target:** Folder or Web
4. **Updates:** Automatic via ClickOnce

### MSBuild интеграция
- **Build → Batch Build** для множественных конфигураций
- **Project Properties → Build Events** для pre/post build скриптов

### Packaging
```xml
<!-- В WindowsLauncher.UI.csproj -->
<GenerateManifests>true</GenerateManifests>
<BootstrapperEnabled>true</BootstrapperEnabled>
<ApplicationIcon>Resources\Icons\KDV_icon.ico</ApplicationIcon>
```

## Типичные ошибки и решения в Visual Studio

### 1. Ошибки базы данных
**Все проблемы с БД:** см. [DATABASE_ARCHITECTURE.md](DATABASE_ARCHITECTURE.md) → раздел "Устранение неисправностей"

### 2. MaterialDesign Theme не загружается
**Проблема:** UI выглядит как стандартный WPF  
**Решение:** Проверить `App.xaml` → MaterialDesign ResourceDictionary

### 3. Active Directory недоступен
**Проблема:** AD authentication fails in development  
**Решение:** 
- Настроить `"ActiveDirectory": { "Enabled": false }` в appsettings.json
- Использовать fallback на локальных пользователей

### 4. Настройки пользователя сбрасываются
**Проблема:** Settings.settings не сохраняются  
**Решение:** **Project Properties → Settings** → проверить Scope = User

### 5. DPI Scaling проблемы
**Проблема:** UI размытый на high-DPI мониторах  
**Решение:** Проверить `app.manifest` → DPI Awareness = PerMonitorV2


## Быстрая диагностика

### Проверка конфигурации
1. **View → Output** → Select "WindowsLauncher.UI Debug"
2. Поиск в логах: "DATABASE INITIALIZATION" или "Authentication"
3. **Tools → Options → Debugging → Output Window** → Program Output

### Сброс к заводским настройкам
1. Закрыть Visual Studio
2. Удалить папку `%AppData%\WindowsLauncher\`
3. **Build → Clean Solution** → **Rebuild Solution**
4. Первый запуск покажет Setup Window

### Performance Profiling
- **Debug → Performance Profiler** для анализа производительности
- **Diagnostic Tools** window во время отладки
- **PerfView** для детального анализа .NET приложений

## Команды для типовых задач

### Сборка и запуск
```bash
# Полная пересборка через PowerShell
.\clean-build.ps1

# Или через dotnet CLI
dotnet clean
dotnet restore  
dotnet build --configuration Debug
```

### Работа с базой данных
**📋 Команды и миграции:** см. [DATABASE_ARCHITECTURE.md](DATABASE_ARCHITECTURE.md)

### Генерация хэшей паролей
```powershell
# Из корня проекта
.\generate-hash.ps1 "your_password"
```
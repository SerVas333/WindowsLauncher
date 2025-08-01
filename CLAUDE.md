# CLAUDE.md

Инструкции для работы с WindowsLauncher в Visual Studio 2022

# Роль Claude
Ты — эксперт по WPF и C#, MVVM, XAML, UI/UX.

# Правила структуры ответа
1. Краткое описание решения
2. Код с детальными комментариями
3. Пояснения, рекомендации по улучшению архитектуры, указание на применённые технологии (.NET 8, DI, Data Binding и т.д.)
4. Лучшие практики: безопасность, тестируемость, масштабируемость
5. Краткий обзор альтернатив — выдели плюсы/минусы

# Workflow & Planning Policy

- IMPORTANT: Claude НЕ ДОЛЖЕН начинать писать или изменять код, пока не:
  1. Убедится, что его предложенный план решения одобрен пользователем.
  2. Четко проговорит пошаговый план реализации, получит подтверждение и комментарии.
- Сначала — только анализ задачи и пошаговый продуманный план, никаких фрагментов кода до явного запроса или подтверждения.
- Если у задачи есть несколько подходов, обязательно предложить варианты и озвучить плюсы и минусы перед началом кода.
- Если пользователь не подтвердил план, Claude должен уточнить или запросить одобрение.

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

## SQL Совместимость между диалектами

### Важные различия в синтаксисе

**Ограничение результатов:**
- **SQLite**: `SELECT ... LIMIT n`
- **Firebird**: `SELECT FIRST n ...`

**Проверка существования таблиц:**
- **SQLite**: `SELECT name FROM sqlite_master WHERE type='table' AND name='TABLE_NAME'`
- **Firebird**: `SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'TABLE_NAME'`

**Получение текущего времени:**
- **SQLite**: `datetime('now')`
- **Firebird**: `CURRENT_TIMESTAMP`

### Рекомендованный подход к SQL запросам

**ВСЕГДА используйте проверку типа БД для критических различий:**
```csharp
string sql = config.DatabaseType switch
{
    DatabaseType.SQLite => "SELECT ... ORDER BY ... LIMIT 1",
    DatabaseType.Firebird => "SELECT FIRST 1 ... ORDER BY ...",
    _ => throw new NotSupportedException($"Database type {config.DatabaseType} is not supported")
};
```

**Используйте UPPERCASE имена для таблиц и столбцов** - это работает в обеих БД.

**Избегайте специфичных функций** - используйте только стандартные SQL конструкции, поддерживаемые обеими БД.

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
        
        // SQL для создания новых таблиц/колонок с учетом диалекта БД
        string sql = databaseType switch 
        { 
            DatabaseType.SQLite => "CREATE TABLE ... ; SELECT ... LIMIT 1;",
            DatabaseType.Firebird => "CREATE TABLE ... ; SELECT FIRST 1 ...;",
            _ => throw new NotSupportedException($"Database type {databaseType} not supported")
        };
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

## Переключатель приложений (AppSwitcher)

### Описание функциональности

**AppSwitcher** — встроенный механизм переключения между запущенными приложениями, аналогичный стандартному Alt+Tab в Windows, адаптированный для работы как в режиме Shell, так и в обычном Windows окружении.

### Режимы работы

#### Shell режим (замена проводника Windows)
- **Автоматическое определение:** когда WindowsLauncher зарегистрирован как системный Shell
- **Горячие клавиши:** 
  - `Alt+Tab` — переключение вперед
  - `Ctrl+Alt+Tab` — переключение назад
- **Использование:** полная замена стандартного переключателя Windows

#### Normal режим (обычное приложение)
- **Автоматическое определение:** в стандартном Windows окружении
- **Горячие клавиши:**
  - `Win+\`` — переключение вперед  
  - `Win+Shift+\`` — переключение назад
- **Использование:** дополнительный переключатель совместно с системным Alt+Tab

### Способы определения режима

Система автоматически определяет режим работы по приоритету:
1. **Реестр Windows** — проверка регистрации как Shell в `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`
2. **Переменная окружения** — `WINDOWSLAUNCHER_SHELL_MODE=Shell`
3. **Аргументы командной строки** — `--shell` или `/shell`
4. **Отсутствие explorer.exe** — если процесс не найден, считается Shell режимом
5. **По умолчанию** — Normal режим

### UI компоненты

#### Окно переключателя (AppSwitcherWindow)
- **Современный Material Design** интерфейс
- **Карточки приложений** с подробной информацией:
  - Эмодзи иконки по типу приложения
  - Название приложения
  - Статус (активно/свернуто/не отвечает) с цветовой индикацией
  - Потребление памяти в МБ
  - PID процесса
- **Анимации появления/исчезновения** для плавной работы
- **Информация о режиме** и доступных горячих клавишах

#### Кнопка в статус баре (для сенсорных терминалов)
- **Расположение:** справа в статус баре рядом с кнопкой виртуальной клавиатуры
- **Иконка переключения:** 🔄
- **Счетчик приложений:** красный badge с количеством запущенных приложений
- **Умное состояние:** автоматически отключается при отсутствии приложений
- **Информативный tooltip:** показывает текущий режим работы и доступные горячие клавиши

### Управление

#### Клавиатурная навигация:
- **↑↓←→** — навигация по приложениям
- **Tab/Shift+Tab** — переключение вперед/назад
- **Enter** — переключиться на выбранное приложение
- **Escape** — отменить переключение
- **Отпускание Alt** — автоматическое переключение (в Shell режиме)

#### Мышиное управление:
- **Клик по карточке** — мгновенное переключение на приложение
- **Клик по кнопке в статус баре** — открытие переключателя

### Интеграция с системой

#### Глобальные хоткеи (GlobalHotKeyService)
- **Системная регистрация** через Windows API
- **Автоматический выбор** комбинаций в зависимости от режима
- **Обработка конфликтов** при занятости хоткеев

#### Мониторинг приложений (RunningApplicationsService)
- **Отслеживание процессов** запущенных через лаунчер
- **Обновление статуса** каждые 5 секунд
- **Обработка завершения** процессов с улучшенной логикой
- **Поддержка браузерных приложений** с множественными процессами

### Техническая архитектура

#### Основные сервисы:
- **`AppSwitcherService`** — управление жизненным циклом переключателя
- **`GlobalHotKeyService`** — регистрация и обработка горячих клавиш
- **`ShellModeDetectionService`** — автоматическое определение режима работы
- **`RunningApplicationsService`** — мониторинг запущенных приложений

#### Конфигурация DI:
```csharp
// В App.xaml.cs
services.AddSingleton<GlobalHotKeyService>();
services.AddSingleton<AppSwitcherService>();  
services.AddSingleton<ShellModeDetectionService>();
services.AddSingleton<IRunningApplicationsService, RunningApplicationsService>();
```

### Тестирование режимов

#### Normal режим (по умолчанию):
```bash
# Обычный запуск
WindowsLauncher.exe

# Использование: Win+` для вызова переключателя
```

#### Shell режим:
```bash
# Запуск с параметром
WindowsLauncher.exe --shell

# Или установка переменной окружения
set WINDOWSLAUNCHER_SHELL_MODE=Shell
WindowsLauncher.exe

# Использование: Alt+Tab для вызова переключателя
```

### Логирование и диагностика

Все операции переключателя логируются с детальной информацией:
- Инициализация с указанием режима работы
- Регистрация горячих клавиш
- События переключения приложений
- Ошибки и предупреждения

**Пример лога:**
```
[INFO] Running in: Режим обычного Windows  
[INFO] Available hotkeys: Win+` - переключение вперед, Win+Shift+` - назад
[INFO] Application switcher shown with 3 applications in Normal mode
```

## Встроенный текстовый редактор (TextEditor)

### Описание функциональности

**TextEditor** — встроенный текстовый редактор на основе WPF RichTextBox, заменяющий проблематичный DesktopApplicationLauncher для текстовых приложений. Обеспечивает полный контроль жизненного цикла и надежную работу с AppSwitcher.

### Архитектура решения

**Основные компоненты:**
```
WindowsLauncher.UI/Components/TextEditor/
├── TextEditorWindow.xaml.cs          # Главное окно редактора
└── TextEditorArguments.cs            # Система аргументов для настройки

WindowsLauncher.UI/Services/
└── TextEditorApplicationLauncher.cs  # Сервис жизненного цикла
```

**Интеграция с системой:**
- **ApplicationLifecycleService** — управление жизненным циклом
- **AppSwitcher** — переключение между экземплярами (Alt+Tab)
- **DI Container** — регистрация в App.xaml.cs

### Система аргументов

**TextEditorArguments** — мощная система настройки функциональности через аргументы приложения:

```csharp
// Основные флаги отключения функций
--notopen      // Отключить открытие файлов
--notsave      // Отключить сохранение файлов  
--notprint     // Отключить печать документов
--notformat    // Отключить форматирование текста
--readonly     // Режим только чтения
--notoolbar    // Скрыть панель инструментов
--nostatusbar  // Скрыть статус бар

// Путь к файлу (можно в кавычках)
"C:\path\to\file.txt"
```

**Примеры использования:**
```
# Блокнот только для чтения без сохранения
--readonly --notsave readme.txt

# Простой просмотрщик без редактирования
--notopen --notsave --notprint --readonly document.txt

# Редактор без форматирования (plain text)
--notformat --notoolbar report.txt
```

### Функциональность редактора

#### Полноценный UI с Material Design
- **Меню бар** — Файл, Правка, Формат, Вид
- **Панель инструментов** — быстрые действия с эмодзи иконками
- **Статус бар** — позиция курсора, режим, индикаторы
- **RichTextBox** — основная область редактирования

#### Файловые операции
- **Создать** (Ctrl+N) — новый документ
- **Открыть** (Ctrl+O) — диалог открытия файлов
- **Сохранить** (Ctrl+S) — с защитой от перезаписи оригинала
- **Сохранить как** (Ctrl+Shift+S) — с автоматическим суффиксом "_копия"

#### Система защиты данных
```csharp
// НОВОЕ: Защита от перезаписи оригинала
if (string.Equals(_filePath, _originalFilePath, StringComparison.OrdinalIgnoreCase))
{
    var result = MessageBox.Show(
        "Вы пытаетесь перезаписать оригинальный файл...",
        "Предупреждение о перезаписи", 
        MessageBoxButton.YesNo);
        
    // Создание backup при перезаписи
    File.Copy(_originalFilePath, backupPath, true);
}
```

#### Печать документов
- **Печать** (Ctrl+P) — полнофункциональная печать с заголовками
- **Предварительный просмотр** — FlowDocumentScrollViewer
- **Параметры страницы** — настройка формата (планируется)

#### Форматирование текста
- **Базовое форматирование** — полужирный, курсив, подчеркивание
- **Цвета** — текст и выделение фона
- **Размер шрифта** — выпадающий список 8-24px
- **Шрифты** — диалог выбора (планируется)

### Техническая архитектура

#### TextEditorApplicationLauncher
```csharp
public class TextEditorApplicationLauncher : IApplicationLauncher
{
    public ApplicationType SupportedType => ApplicationType.Desktop;
    public int Priority => 30; // Высший приоритет над DesktopApplicationLauncher (10)
    
    // События для интеграции с ApplicationLifecycleService
    public event EventHandler<ApplicationInstance>? WindowActivated;
    public event EventHandler<ApplicationInstance>? WindowClosed;
    
    // Автоматическое определение текстовых редакторов по пути
    public bool CanLaunch(Application application)
    {
        var executablePath = application.ExecutablePath?.ToLowerInvariant() ?? "";
        
        // Определение по пути к исполняемому файлу (основной критерий)
        return executablePath.Contains("notepad.exe") ||      // Windows Notepad
               executablePath.Contains("notepad++.exe") ||    // Notepad++ (fallback)
               executablePath.Contains("wordpad.exe") ||      // WordPad
               executablePath.Contains("code.exe");           // VS Code и др.
    }
}
```

#### Single-Instance логика
- **FindExistingInstanceAsync** — поиск запущенных экземпляров по Application.Id
- **SwitchToAsync** — активация существующего окна вместо создания нового
- **Event-based интеграция** — уведомления ApplicationLifecycleService

#### AppSwitcher интеграция
```csharp
// Создание ApplicationInstance для AppSwitcher
var instance = new ApplicationInstance
{
    InstanceId = window.InstanceId,
    Application = application,
    ProcessId = Environment.ProcessId, // TextEditor работает в текущем процессе
    State = ApplicationState.Running,
    IsActive = true
};

// Уведомление AppSwitcher о новом окне
WindowActivated?.Invoke(this, instance);
```

### Регистрация в DI контейнере

**App.xaml.cs:**
```csharp
// TextEditor лаунчер для встроенного текстового редактора
services.AddScoped<IApplicationLauncher, TextEditorApplicationLauncher>();

// ApplicationLifecycleService автоматически подключает события через рефлексию
// SubscribeToLauncherEvents() обнаруживает TextEditorApplicationLauncher
```

### Преимущества над DesktopApplicationLauncher

#### Надежность
- **Отсутствие multi-process проблем** — все работает в едином WPF процессе
- **Нет проблем с Window detection** — прямое управление WPF окнами
- **Stable single-instance логика** — без зависимости от Process.MainWindowHandle

#### Функциональность
- **Полный контроль UI** — никаких ограничений внешних приложений
- **Аргументы для настройки** — гибкое управление функциональностью
- **Защита данных** — предотвращение потери оригинальных файлов

#### Интеграция
- **Идеальная работа с AppSwitcher** — нативная WPF интеграция
- **Event-driven архитектура** — реактивные уведомления
- **Material Design UI** — консистентный с остальным приложением

### Тестирование в Visual Studio

#### Создание тестового приложения
1. **Админ панель** → Добавить приложение
2. **Настройки:**
   ```
   Название: "Notepad++ Editor"
   Тип: Desktop
   Путь: C:\Program Files\Notepad++\notepad++.exe
   Аргументы: "--readonly --notprint C:\test.txt"
   ```
   
   **Альтернативные варианты:**
   ```
   # Стандартный блокнот Windows
   Путь: C:\Windows\System32\notepad.exe
   
   # WordPad
   Путь: C:\Program Files\Windows NT\Accessories\wordpad.exe
   
   # VS Code
   Путь: C:\Users\%USERNAME%\AppData\Local\Programs\Microsoft VS Code\Code.exe
   ```

#### Проверка функциональности
1. **Запуск** → должен открыться TextEditor вместо стандартного notepad
2. **Повторный запуск** → переключение на существующий экземпляр
3. **AppSwitcher** (Alt+Tab) → окно должно отображаться в переключателе
4. **Аргументы** → проверить отключение соответствующих функций

#### Debug логирование
```
[INFO] TextEditorApplicationLauncher can launch Notepad++ Editor (Path: C:\Program Files\Notepad++\notepad++.exe)
[INFO] Launching TextEditor application Notepad++ Editor (Instance: texteditor_123_abc) by user admin  
[INFO] Successfully launched TextEditor application Notepad++ Editor in 45ms
[DEBUG] Subscribed to TextEditorApplicationLauncher events via reflection
```

#### Поддерживаемые текстовые редакторы
- **notepad.exe** — стандартный блокнот Windows
- **notepad++.exe** — Notepad++ (рекомендуемый fallback)
- **wordpad.exe** — WordPad
- **code.exe** — Visual Studio Code
- **sublimetext.exe** — Sublime Text
- **atom.exe** — Atom Editor
- **texteditor.exe** — общие названия редакторов

### Рекомендации по использованию

#### Когда использовать TextEditor
- **Текстовые файлы** — .txt, .log, .md, .ini, .cfg
- **Простое редактирование** — без сложного форматирования
- **Корпоративная среда** — когда нужен контроль над функциональностью
- **Проблемы с notepad.exe** — multi-process, detection issues
- **Fallback для внешних редакторов** — замена Notepad++, VS Code при недоступности

#### Fallback стратегия
1. **Установлен Notepad++** → указать путь `C:\Program Files\Notepad++\notepad++.exe`
2. **Только стандартный блокнот** → указать путь `C:\Windows\System32\notepad.exe`
3. **TextEditor запустится** вместо внешнего приложения с полным контролем функций
4. **При проблемах с внешними редакторами** — встроенный редактор работает стабильно

#### Настройка через аргументы
- **Киоски и терминалы** — `--readonly --notopen --notsave`
- **Просмотр логов** — `--readonly --notformat --notoolbar`
- **Безопасное редактирование** — `--notopen` для предотвращения открытия посторонних файлов

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
- **OS Support:** Windows 10-11

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
# important-instruction-reminders
Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
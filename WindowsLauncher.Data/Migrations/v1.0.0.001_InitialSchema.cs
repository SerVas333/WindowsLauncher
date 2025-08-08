using System.Threading.Tasks;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Migrations
{
    /// <summary>
    /// Базовая миграция v1.0.0.001 - создание полной схемы БД
    /// </summary>
    public class InitialSchema : IDatabaseMigration
    {
        public string Name => "InitialSchema";
        public string Version => "1.0.0.001";
        public string Description => "Create initial database schema with all tables and indexes";

        public async Task UpAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Создаем таблицы в правильном порядке (с учетом зависимостей)
            
            // 1. Таблица пользователей
            await CreateUsersTableAsync(context, databaseType);
            
            // 2. Таблица приложений
            await CreateApplicationsTableAsync(context, databaseType);
            
            // 3. Таблица настроек пользователей
            await CreateUserSettingsTableAsync(context, databaseType);
            
            // 4. Таблица аудита
            await CreateAuditLogsTableAsync(context, databaseType);
            
            // 5. Таблицы для системы миграций
            await CreateMigrationTablesAsync(context, databaseType);
            
            // 6. Создаем индексы
            await CreateIndexesAsync(context, databaseType);
            
            // 7. Добавляем начальные данные
            await SeedInitialDataAsync(context, databaseType);
        }

        private async Task CreateUsersTableAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            string sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE USERS (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        USERNAME TEXT NOT NULL UNIQUE,
                        DISPLAY_NAME TEXT NOT NULL,
                        EMAIL TEXT DEFAULT '',
                        ROLE INTEGER NOT NULL DEFAULT 2,
                        IS_ACTIVE INTEGER NOT NULL DEFAULT 1,
                        IS_SERVICE_ACCOUNT INTEGER NOT NULL DEFAULT 0,
                        PASSWORD_HASH TEXT DEFAULT '',
                        SALT TEXT DEFAULT '',
                        CREATED_AT TEXT NOT NULL,
                        LAST_LOGIN_AT TEXT,
                        LAST_ACTIVITY_AT TEXT,
                        FAILED_LOGIN_ATTEMPTS INTEGER NOT NULL DEFAULT 0,
                        IS_LOCKED INTEGER NOT NULL DEFAULT 0,
                        LOCKOUT_END TEXT,
                        LAST_PASSWORD_CHANGE TEXT,
                        GROUPS_JSON TEXT DEFAULT '[]',
                        SETTINGS_JSON TEXT DEFAULT '{{}}',
                        METADATA_JSON TEXT DEFAULT '{{}}',
                        AUTHENTICATION_TYPE INTEGER NOT NULL DEFAULT 0,
                        DOMAIN_USERNAME TEXT DEFAULT '',
                        LAST_DOMAIN_SYNC TEXT,
                        IS_LOCAL_USER INTEGER NOT NULL DEFAULT 1,
                        ALLOW_LOCAL_LOGIN INTEGER NOT NULL DEFAULT 0
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE USERS (
                        ID INTEGER NOT NULL PRIMARY KEY,
                        USERNAME VARCHAR(100) NOT NULL UNIQUE,
                        DISPLAY_NAME VARCHAR(200) NOT NULL,
                        EMAIL VARCHAR(255) DEFAULT '',
                        ROLE INTEGER NOT NULL DEFAULT 2,
                        IS_ACTIVE SMALLINT NOT NULL DEFAULT 1,
                        IS_SERVICE_ACCOUNT SMALLINT NOT NULL DEFAULT 0,
                        PASSWORD_HASH VARCHAR(500) DEFAULT '',
                        SALT VARCHAR(500) DEFAULT '',
                        CREATED_AT TIMESTAMP NOT NULL,
                        LAST_LOGIN_AT TIMESTAMP,
                        LAST_ACTIVITY_AT TIMESTAMP,
                        FAILED_LOGIN_ATTEMPTS INTEGER NOT NULL DEFAULT 0,
                        IS_LOCKED SMALLINT NOT NULL DEFAULT 0,
                        LOCKOUT_END TIMESTAMP,
                        LAST_PASSWORD_CHANGE TIMESTAMP,
                        GROUPS_JSON VARCHAR(2000) DEFAULT '[]',
                        SETTINGS_JSON VARCHAR(4000) DEFAULT '{{}}',
                        METADATA_JSON VARCHAR(2000) DEFAULT '{{}}',
                        AUTHENTICATION_TYPE INTEGER NOT NULL DEFAULT 0,
                        DOMAIN_USERNAME VARCHAR(100) DEFAULT '',
                        LAST_DOMAIN_SYNC TIMESTAMP,
                        IS_LOCAL_USER SMALLINT NOT NULL DEFAULT 1,
                        ALLOW_LOCAL_LOGIN SMALLINT NOT NULL DEFAULT 0
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(sql);

            // Создаем генератор для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                await context.ExecuteSqlAsync("CREATE GENERATOR GEN_USERS_ID;");
                await context.ExecuteSqlAsync("SET GENERATOR GEN_USERS_ID TO 1;");
                await context.ExecuteSqlAsync(@"
                    CREATE TRIGGER TRG_USERS_ID FOR USERS
                    ACTIVE BEFORE INSERT POSITION 0
                    AS BEGIN
                        IF (NEW.ID IS NULL) THEN
                            NEW.ID = GEN_ID(GEN_USERS_ID, 1);
                    END;");
            }
        }

        private async Task CreateApplicationsTableAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            string sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE APPLICATIONS (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        NAME TEXT NOT NULL,
                        DESCRIPTION TEXT,
                        EXECUTABLE_PATH TEXT NOT NULL,
                        ARGUMENTS TEXT,
                        WORKING_DIRECTORY TEXT,
                        ICON_PATH TEXT,
                        ICONTEXT TEXT DEFAULT '📱' CHECK(LENGTH(ICONTEXT) <= 50),
                        CATEGORY TEXT,
                        APP_TYPE INTEGER NOT NULL DEFAULT 0,
                        MINIMUM_ROLE INTEGER NOT NULL DEFAULT 2,
                        IS_ENABLED INTEGER NOT NULL DEFAULT 1,
                        SORT_ORDER INTEGER NOT NULL DEFAULT 0,
                        CREATED_DATE TEXT NOT NULL,
                        MODIFIED_DATE TEXT NOT NULL,
                        CREATED_BY TEXT,
                        REQUIRED_GROUPS TEXT DEFAULT '[]'
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE APPLICATIONS (
                        ID INTEGER NOT NULL PRIMARY KEY,
                        NAME VARCHAR(200) NOT NULL,
                        DESCRIPTION VARCHAR(500),
                        EXECUTABLE_PATH VARCHAR(1000) NOT NULL,
                        ARGUMENTS VARCHAR(500),
                        WORKING_DIRECTORY VARCHAR(1000),
                        ICON_PATH VARCHAR(1000),
                        ICONTEXT VARCHAR(50) DEFAULT '📱',
                        CATEGORY VARCHAR(100),
                        APP_TYPE INTEGER NOT NULL DEFAULT 0,
                        MINIMUM_ROLE INTEGER NOT NULL DEFAULT 2,
                        IS_ENABLED SMALLINT NOT NULL DEFAULT 1,
                        SORT_ORDER INTEGER NOT NULL DEFAULT 0,
                        CREATED_DATE TIMESTAMP NOT NULL,
                        MODIFIED_DATE TIMESTAMP NOT NULL,
                        CREATED_BY VARCHAR(100),
                        REQUIRED_GROUPS BLOB SUB_TYPE 1 DEFAULT '[]'
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(sql);

            // Создаем генератор для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                await context.ExecuteSqlAsync("CREATE GENERATOR GEN_APPLICATIONS_ID;");
                await context.ExecuteSqlAsync("SET GENERATOR GEN_APPLICATIONS_ID TO 1;");
                await context.ExecuteSqlAsync(@"
                    CREATE TRIGGER TRG_APPLICATIONS_ID FOR APPLICATIONS
                    ACTIVE BEFORE INSERT POSITION 0
                    AS BEGIN
                        IF (NEW.ID IS NULL) THEN
                            NEW.ID = GEN_ID(GEN_APPLICATIONS_ID, 1);
                    END;");
            }
        }

        private async Task CreateUserSettingsTableAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            string sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE USER_SETTINGS (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        USER_ID INTEGER NOT NULL,
                        THEME TEXT DEFAULT 'Light',
                        ACCENT_COLOR TEXT DEFAULT 'Blue',
                        TILE_SIZE INTEGER DEFAULT 150,
                        SHOW_CATEGORIES INTEGER DEFAULT 1,
                        DEFAULT_CATEGORY TEXT DEFAULT 'All',
                        AUTO_REFRESH INTEGER DEFAULT 1,
                        REFRESH_INTERVAL_MINUTES INTEGER DEFAULT 30,
                        SHOW_DESCRIPTIONS INTEGER DEFAULT 1,
                        HIDDEN_CATEGORIES TEXT DEFAULT '[]',
                        LAST_MODIFIED TEXT NOT NULL,
                        FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE CASCADE
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE USER_SETTINGS (
                        ID INTEGER NOT NULL PRIMARY KEY,
                        USER_ID INTEGER NOT NULL,
                        THEME VARCHAR(50) DEFAULT 'Light',
                        ACCENT_COLOR VARCHAR(50) DEFAULT 'Blue',
                        TILE_SIZE INTEGER DEFAULT 150,
                        SHOW_CATEGORIES SMALLINT DEFAULT 1,
                        DEFAULT_CATEGORY VARCHAR(100) DEFAULT 'All',
                        AUTO_REFRESH SMALLINT DEFAULT 1,
                        REFRESH_INTERVAL_MINUTES INTEGER DEFAULT 30,
                        SHOW_DESCRIPTIONS SMALLINT DEFAULT 1,
                        HIDDEN_CATEGORIES BLOB SUB_TYPE 1 DEFAULT '[]',
                        LAST_MODIFIED TIMESTAMP NOT NULL,
                        FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE CASCADE
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(sql);

            // Создаем генератор для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                await context.ExecuteSqlAsync("CREATE GENERATOR GEN_USER_SETTINGS_ID;");
                await context.ExecuteSqlAsync("SET GENERATOR GEN_USER_SETTINGS_ID TO 1;");
                await context.ExecuteSqlAsync(@"
                    CREATE TRIGGER TRG_USER_SETTINGS_ID FOR USER_SETTINGS
                    ACTIVE BEFORE INSERT POSITION 0
                    AS BEGIN
                        IF (NEW.ID IS NULL) THEN
                            NEW.ID = GEN_ID(GEN_USER_SETTINGS_ID, 1);
                    END;");
            }
        }

        private async Task CreateAuditLogsTableAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            string sql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE AUDIT_LOGS (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        USER_ID INTEGER,
                        USERNAME TEXT NOT NULL,
                        ACTION TEXT NOT NULL,
                        APPLICATION_NAME TEXT,
                        DETAILS TEXT,
                        TIMESTAMP_UTC TEXT NOT NULL,
                        SUCCESS INTEGER NOT NULL DEFAULT 1,
                        ERROR_MESSAGE TEXT,
                        COMPUTER_NAME TEXT,
                        IP_ADDRESS TEXT,
                        USER_AGENT TEXT,
                        METADATA_JSON TEXT DEFAULT '{{}}',
                        FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE SET NULL
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE AUDIT_LOGS (
                        ID INTEGER NOT NULL PRIMARY KEY,
                        USER_ID INTEGER,
                        USERNAME VARCHAR(100) NOT NULL,
                        ACTION VARCHAR(100) NOT NULL,
                        APPLICATION_NAME VARCHAR(200),
                        DETAILS BLOB SUB_TYPE 1,
                        TIMESTAMP_UTC TIMESTAMP NOT NULL,
                        SUCCESS SMALLINT NOT NULL DEFAULT 1,
                        ERROR_MESSAGE VARCHAR(1000),
                        COMPUTER_NAME VARCHAR(100),
                        IP_ADDRESS VARCHAR(45),
                        USER_AGENT VARCHAR(500),
                        METADATA_JSON BLOB SUB_TYPE 1 DEFAULT '{{}}',
                        FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE SET NULL
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(sql);

            // Создаем генератор для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                await context.ExecuteSqlAsync("CREATE GENERATOR GEN_AUDIT_LOGS_ID;");
                await context.ExecuteSqlAsync("SET GENERATOR GEN_AUDIT_LOGS_ID TO 1;");
                await context.ExecuteSqlAsync(@"
                    CREATE TRIGGER TRG_AUDIT_LOGS_ID FOR AUDIT_LOGS
                    ACTIVE BEFORE INSERT POSITION 0
                    AS BEGIN
                        IF (NEW.ID IS NULL) THEN
                            NEW.ID = GEN_ID(GEN_AUDIT_LOGS_ID, 1);
                    END;");
            }
        }

        private async Task CreateMigrationTablesAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Таблица версий БД
            string versionSql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE DATABASE_VERSION (
                        VERSION TEXT PRIMARY KEY,
                        APPLIED_AT TEXT NOT NULL,
                        APPLICATION_VERSION TEXT
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE DATABASE_VERSION (
                        VERSION VARCHAR(20) NOT NULL PRIMARY KEY,
                        APPLIED_AT TIMESTAMP NOT NULL,
                        APPLICATION_VERSION VARCHAR(20)
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(versionSql);

            // Таблица истории миграций  
            string migrationSql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    CREATE TABLE MIGRATION_HISTORY (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        VERSION TEXT NOT NULL,
                        NAME TEXT NOT NULL,
                        DESCRIPTION TEXT,
                        APPLIED_AT TEXT NOT NULL,
                        ROLLBACK_SCRIPT TEXT
                    );",
                
                DatabaseType.Firebird => @"
                    CREATE TABLE MIGRATION_HISTORY (
                        ID INTEGER NOT NULL PRIMARY KEY,
                        VERSION VARCHAR(20) NOT NULL,
                        NAME VARCHAR(200) NOT NULL,
                        DESCRIPTION BLOB SUB_TYPE 1,
                        APPLIED_AT TIMESTAMP NOT NULL,
                        ROLLBACK_SCRIPT BLOB SUB_TYPE 1
                    );",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(migrationSql);

            // Создаем генератор для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                await context.ExecuteSqlAsync("CREATE GENERATOR GEN_MIGRATION_HISTORY_ID;");
                await context.ExecuteSqlAsync("SET GENERATOR GEN_MIGRATION_HISTORY_ID TO 1;");
                await context.ExecuteSqlAsync(@"
                    CREATE TRIGGER TRG_MIGRATION_HISTORY_ID FOR MIGRATION_HISTORY
                    ACTIVE BEFORE INSERT POSITION 0
                    AS BEGIN
                        IF (NEW.ID IS NULL) THEN
                            NEW.ID = GEN_ID(GEN_MIGRATION_HISTORY_ID, 1);
                    END;");
            }
        }

        private async Task CreateIndexesAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Индексы для производительности
            var indexes = new[]
            {
                "CREATE INDEX IDX_USERS_USERNAME ON USERS(USERNAME);",
                "CREATE INDEX IDX_USERS_ROLE ON USERS(ROLE);",
                "CREATE INDEX IDX_APPLICATIONS_NAME ON APPLICATIONS(NAME);",
                "CREATE INDEX IDX_APPLICATIONS_CATEGORY ON APPLICATIONS(CATEGORY);",
                "CREATE INDEX IDX_APPLICATIONS_ENABLED ON APPLICATIONS(IS_ENABLED);",
                "CREATE INDEX IDX_USER_SETTINGS_USER_ID ON USER_SETTINGS(USER_ID);",
                "CREATE INDEX IDX_AUDIT_LOGS_TIMESTAMP ON AUDIT_LOGS(TIMESTAMP_UTC);",
                "CREATE INDEX IDX_AUDIT_LOGS_USER_ID ON AUDIT_LOGS(USER_ID);",
                "CREATE INDEX IDX_MIGRATION_HISTORY_VERSION ON MIGRATION_HISTORY(VERSION);"
            };

            foreach (var indexSql in indexes)
            {
                try
                {
                    await context.ExecuteSqlAsync(indexSql);
                }
                catch
                {
                    // Игнорируем ошибки создания индексов (может уже существовать)
                }
            }
        }

        private async Task SeedInitialDataAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Добавляем запись о версии БД
            string timestampValue = databaseType switch
            {
                DatabaseType.SQLite => "datetime('now')",
                DatabaseType.Firebird => "CURRENT_TIMESTAMP",
                _ => "CURRENT_TIMESTAMP"
            };

            await context.ExecuteSqlAsync($@"
                INSERT INTO DATABASE_VERSION (VERSION, APPLIED_AT, APPLICATION_VERSION) 
                VALUES ('1.0.0.001', {timestampValue}, '1.0.0');");

            // Добавляем запись в историю миграций
            await context.ExecuteSqlAsync($@"
                INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT) 
                VALUES ('1.0.0.001', 'InitialSchema', 'Create initial database schema with all tables and indexes', {timestampValue});");

            // Добавляем базового пользователя (guest) для первоначального доступа
            // ПРИМЕЧАНИЕ: Пользователь 'serviceadmin' для администрирования создается автоматически
            // сервисом AuthenticationConfigurationService при первом запуске через auth-config.json
            string guestUserSql = databaseType switch
            {
                DatabaseType.SQLite => @"
                    INSERT INTO USERS (USERNAME, DISPLAY_NAME, EMAIL, ROLE, IS_ACTIVE, IS_SERVICE_ACCOUNT, PASSWORD_HASH, SALT, CREATED_AT, AUTHENTICATION_TYPE, IS_LOCAL_USER, ALLOW_LOCAL_LOGIN, FAILED_LOGIN_ATTEMPTS, IS_LOCKED, GROUPS_JSON, SETTINGS_JSON, METADATA_JSON) 
                    VALUES ('guest', 'Guest User', 'guest@local', 2, 1, 0, '', '', datetime('now'), 0, 1, 0, 0, 0, '[]', '{{}}', '{{}}');",
                
                DatabaseType.Firebird => @"
                    INSERT INTO USERS (USERNAME, DISPLAY_NAME, EMAIL, ROLE, IS_ACTIVE, IS_SERVICE_ACCOUNT, PASSWORD_HASH, SALT, CREATED_AT, AUTHENTICATION_TYPE, IS_LOCAL_USER, ALLOW_LOCAL_LOGIN, FAILED_LOGIN_ATTEMPTS, IS_LOCKED, GROUPS_JSON, SETTINGS_JSON, METADATA_JSON) 
                    VALUES ('guest', 'Guest User', 'guest@local', 2, 1, 0, '', '', CURRENT_TIMESTAMP, 0, 1, 0, 0, 0, '[]', '{{}}', '{{}}');",
                
                _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
            };

            await context.ExecuteSqlAsync(guestUserSql);

            // Проверяем, что пользователь создался
            try
            {
                var userCount = await context.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM USERS WHERE USERNAME = 'guest'");
                if (userCount == 0)
                {
                    throw new System.InvalidOperationException("Failed to create guest user");
                }
                
                // Проверяем, что все обязательные поля заполнены
                var nullFieldsQuery = databaseType switch
                {
                    DatabaseType.SQLite => "SELECT COUNT(*) FROM USERS WHERE USERNAME = 'guest' AND (DISPLAY_NAME IS NULL OR CREATED_AT IS NULL)",
                    DatabaseType.Firebird => "SELECT COUNT(*) FROM USERS WHERE USERNAME = 'guest' AND (DISPLAY_NAME IS NULL OR CREATED_AT IS NULL)",
                    _ => throw new System.NotSupportedException($"Database type {databaseType} is not supported")
                };
                
                var nullCount = await context.ExecuteScalarAsync<int>(nullFieldsQuery);
                if (nullCount > 0)
                {
                    throw new System.InvalidOperationException("Guest user created with NULL required fields");
                }
            }
            catch (System.Exception ex)
            {
                throw new System.InvalidOperationException($"Error verifying guest user creation: {ex.Message}", ex);
            }

            // Добавляем базовые приложения
            await SeedApplicationsAsync(context, databaseType);
        }

        private async Task SeedApplicationsAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            string timestampValue = databaseType switch
            {
                DatabaseType.SQLite => "datetime('now')",
                DatabaseType.Firebird => "CURRENT_TIMESTAMP",
                _ => "CURRENT_TIMESTAMP"
            };

            var applications = new[]
            {
                $"('Calculator', 'Windows Calculator', 'calc.exe', '', '', '', '🧮', 'Утилиты', 0, 2, 1, 1, {timestampValue}, {timestampValue}, 'System', '[]')",
                $"('Notepad', 'Text Editor', 'notepad.exe', '', '', '', '📝', 'Утилиты', 0, 2, 1, 2, {timestampValue}, {timestampValue}, 'System', '[]')",
                $"('Google', 'Google Search', 'https://www.google.com', '', '', '', '🌐', 'Приложения', 1, 2, 1, 3, {timestampValue}, {timestampValue}, 'System', '[]')",
                $"('Control Panel', 'Windows Control Panel', 'control.exe', '', '', '', '⚙️', 'Утилиты', 0, 1, 1, 4, {timestampValue}, {timestampValue}, 'System', '[\"LauncherPowerUsers\", \"LauncherAdmins\"]')",
                $"('Command Prompt', 'Windows Command Line', 'cmd.exe', '', '', '', '💻', 'Утилиты', 0, 2, 1, 5, {timestampValue}, {timestampValue}, 'System', '[]')"
            };

            foreach (var app in applications)
            {
                string sql = $@"
                    INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ARGUMENTS, WORKING_DIRECTORY, ICON_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
                    VALUES {app};";
                
                await context.ExecuteSqlAsync(sql);
            }
        }

        public async Task DownAsync(IDatabaseMigrationContext context, DatabaseType databaseType)
        {
            // Удаляем все таблицы в обратном порядке
            var tables = new[] 
            { 
                "MIGRATION_HISTORY", 
                "DATABASE_VERSION", 
                "AUDIT_LOGS", 
                "USER_SETTINGS", 
                "APPLICATIONS", 
                "USERS" 
            };

            foreach (var table in tables)
            {
                try
                {
                    await context.ExecuteSqlAsync($"DROP TABLE {table};");
                }
                catch
                {
                    // Игнорируем ошибки удаления (таблица может не существовать)
                }
            }

            // Удаляем генераторы для Firebird
            if (databaseType == DatabaseType.Firebird)
            {
                var generators = new[] 
                { 
                    "GEN_USERS_ID", 
                    "GEN_APPLICATIONS_ID", 
                    "GEN_USER_SETTINGS_ID", 
                    "GEN_AUDIT_LOGS_ID", 
                    "GEN_MIGRATION_HISTORY_ID" 
                };

                foreach (var generator in generators)
                {
                    try
                    {
                        await context.ExecuteSqlAsync($"DROP GENERATOR {generator};");
                    }
                    catch
                    {
                        // Игнорируем ошибки удаления
                    }
                }
            }
        }
    }
}
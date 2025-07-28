-- ===== create_firebird_server_v1.0.0.001.sql =====
-- Скрипт для создания SERVER Firebird базы данных v1.0.0.001
-- Для корпоративных установок с Firebird Server
-- 
-- ПРЕДВАРИТЕЛЬНЫЕ ТРЕБОВАНИЯ:
-- 1. Firebird Server должен быть установлен и запущен
-- 2. Создан пользователь приложения (не SYSDBA)
-- 3. Настроена безопасность

-- ===== ПАРАМЕТРЫ ПОДКЛЮЧЕНИЯ =====
-- Замените на актуальные параметры вашего сервера:
-- SERVER: localhost или IP адрес сервера Firebird
-- DATABASE: путь к файлу БД на сервере
-- USER: специальный пользователь для приложения (НЕ SYSDBA)
-- PASSWORD: надежный пароль

-- ===== СОЗДАНИЕ ПОЛЬЗОВАТЕЛЯ ПРИЛОЖЕНИЯ =====
-- Выполните на сервере под SYSDBA:

/*
-- Подключение к security3.fdb для создания пользователя
CONNECT 'localhost:security3.fdb' USER 'SYSDBA' PASSWORD 'your_sysdba_password';

-- Создание пользователя приложения
CREATE USER KDV_LAUNCHER PASSWORD 'KDV_L@unch3r_S3cur3_2025!';

-- Предоставление ролей
ALTER USER KDV_LAUNCHER GRANT ADMIN ROLE;

-- Отключение
QUIT;
*/

-- ===== СОЗДАНИЕ БАЗЫ ДАННЫХ =====
-- Выполните под SYSDBA для создания БД:

CREATE DATABASE 'localhost:C:\FirebirdData\WindowsLauncher\launcher_server.fdb'
PAGE_SIZE 16384
USER 'SYSDBA' 
PASSWORD 'your_sysdba_password'
DEFAULT CHARACTER SET UTF8;

-- Подключение к созданной БД
CONNECT 'localhost:C:\FirebirdData\WindowsLauncher\launcher_server.fdb' 
USER 'SYSDBA' 
PASSWORD 'your_sysdba_password';

-- ===== СОЗДАНИЕ ГЕНЕРАТОРОВ =====
CREATE GENERATOR GEN_USERS_ID;
SET GENERATOR GEN_USERS_ID TO 1000;

CREATE GENERATOR GEN_APPLICATIONS_ID;
SET GENERATOR GEN_APPLICATIONS_ID TO 1000;

CREATE GENERATOR GEN_USER_SETTINGS_ID;
SET GENERATOR GEN_USER_SETTINGS_ID TO 1000;

CREATE GENERATOR GEN_AUDIT_LOGS_ID;
SET GENERATOR GEN_AUDIT_LOGS_ID TO 1000;

CREATE GENERATOR GEN_MIGRATION_HISTORY_ID;
SET GENERATOR GEN_MIGRATION_HISTORY_ID TO 1000;

-- ===== СОЗДАНИЕ РОЛЕЙ БЕЗОПАСНОСТИ =====
CREATE ROLE LAUNCHER_USER;
CREATE ROLE LAUNCHER_ADMIN;
CREATE ROLE LAUNCHER_READONLY;

-- ===== СОЗДАНИЕ ТАБЛИЦ =====

-- Таблица пользователей
CREATE TABLE USERS (
    ID INTEGER NOT NULL,
    USERNAME VARCHAR(100) NOT NULL,
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
    SETTINGS_JSON VARCHAR(4000) DEFAULT '{}',
    METADATA_JSON VARCHAR(2000) DEFAULT '{}',
    AUTHENTICATION_TYPE INTEGER NOT NULL DEFAULT 0,
    DOMAIN_USERNAME VARCHAR(100) DEFAULT '',
    LAST_DOMAIN_SYNC TIMESTAMP,
    IS_LOCAL_USER SMALLINT NOT NULL DEFAULT 1,
    ALLOW_LOCAL_LOGIN SMALLINT NOT NULL DEFAULT 0,
    CONSTRAINT PK_USERS PRIMARY KEY (ID),
    CONSTRAINT UQ_USERS_USERNAME UNIQUE (USERNAME)
);

-- Таблица приложений
CREATE TABLE APPLICATIONS (
    ID INTEGER NOT NULL,
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
    REQUIRED_GROUPS BLOB SUB_TYPE 1 DEFAULT '[]',
    CONSTRAINT PK_APPLICATIONS PRIMARY KEY (ID)
);

-- Таблица настроек пользователей
CREATE TABLE USER_SETTINGS (
    ID INTEGER NOT NULL,
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
    CONSTRAINT PK_USER_SETTINGS PRIMARY KEY (ID),
    CONSTRAINT FK_USER_SETTINGS_USER_ID FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE CASCADE
);

-- Таблица аудита
CREATE TABLE AUDIT_LOGS (
    ID INTEGER NOT NULL,
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
    METADATA_JSON BLOB SUB_TYPE 1 DEFAULT '{}',
    CONSTRAINT PK_AUDIT_LOGS PRIMARY KEY (ID),
    CONSTRAINT FK_AUDIT_LOGS_USER_ID FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE SET NULL
);

-- Таблица версий БД
CREATE TABLE DATABASE_VERSION (
    VERSION VARCHAR(20) NOT NULL,
    APPLIED_AT TIMESTAMP NOT NULL,
    APPLICATION_VERSION VARCHAR(20),
    CONSTRAINT PK_DATABASE_VERSION PRIMARY KEY (VERSION)
);

-- Таблица истории миграций
CREATE TABLE MIGRATION_HISTORY (
    ID INTEGER NOT NULL,
    VERSION VARCHAR(20) NOT NULL,
    NAME VARCHAR(200) NOT NULL,
    DESCRIPTION BLOB SUB_TYPE 1,
    APPLIED_AT TIMESTAMP NOT NULL,
    ROLLBACK_SCRIPT BLOB SUB_TYPE 1,
    CONSTRAINT PK_MIGRATION_HISTORY PRIMARY KEY (ID)
);

-- ===== ТРИГГЕРЫ =====
CREATE TRIGGER TRG_USERS_ID FOR USERS
ACTIVE BEFORE INSERT POSITION 0
AS BEGIN
    IF (NEW.ID IS NULL) THEN
        NEW.ID = GEN_ID(GEN_USERS_ID, 1);
END;

CREATE TRIGGER TRG_APPLICATIONS_ID FOR APPLICATIONS
ACTIVE BEFORE INSERT POSITION 0
AS BEGIN
    IF (NEW.ID IS NULL) THEN
        NEW.ID = GEN_ID(GEN_APPLICATIONS_ID, 1);
END;

CREATE TRIGGER TRG_USER_SETTINGS_ID FOR USER_SETTINGS
ACTIVE BEFORE INSERT POSITION 0
AS BEGIN
    IF (NEW.ID IS NULL) THEN
        NEW.ID = GEN_ID(GEN_USER_SETTINGS_ID, 1);
END;

CREATE TRIGGER TRG_AUDIT_LOGS_ID FOR AUDIT_LOGS
ACTIVE BEFORE INSERT POSITION 0
AS BEGIN
    IF (NEW.ID IS NULL) THEN
        NEW.ID = GEN_ID(GEN_AUDIT_LOGS_ID, 1);
END;

CREATE TRIGGER TRG_MIGRATION_HISTORY_ID FOR MIGRATION_HISTORY
ACTIVE BEFORE INSERT POSITION 0
AS BEGIN
    IF (NEW.ID IS NULL) THEN
        NEW.ID = GEN_ID(GEN_MIGRATION_HISTORY_ID, 1);
END;

-- ===== ИНДЕКСЫ =====
CREATE INDEX IDX_USERS_USERNAME ON USERS(USERNAME);
CREATE INDEX IDX_USERS_ROLE ON USERS(ROLE);
CREATE INDEX IDX_USERS_ENABLED ON USERS(IS_ENABLED);
CREATE INDEX IDX_USERS_ACTIVE_DIRECTORY ON USERS(IS_ACTIVE_DIRECTORY);
CREATE INDEX IDX_USERS_LAST_LOGIN ON USERS(LAST_LOGIN_DATE);

CREATE INDEX IDX_APPLICATIONS_NAME ON APPLICATIONS(NAME);
CREATE INDEX IDX_APPLICATIONS_CATEGORY ON APPLICATIONS(CATEGORY);
CREATE INDEX IDX_APPLICATIONS_ENABLED ON APPLICATIONS(IS_ENABLED);
CREATE INDEX IDX_APPLICATIONS_TYPE ON APPLICATIONS(APP_TYPE);
CREATE INDEX IDX_APPLICATIONS_ROLE ON APPLICATIONS(MINIMUM_ROLE);
CREATE INDEX IDX_APPLICATIONS_SORT ON APPLICATIONS(SORT_ORDER);

CREATE INDEX IDX_USER_SETTINGS_USER_ID ON USER_SETTINGS(USER_ID);

CREATE INDEX IDX_AUDIT_LOGS_TIMESTAMP ON AUDIT_LOGS(TIMESTAMP_UTC);
CREATE INDEX IDX_AUDIT_LOGS_USER_ID ON AUDIT_LOGS(USER_ID);
CREATE INDEX IDX_AUDIT_LOGS_ACTION ON AUDIT_LOGS(ACTION);
CREATE INDEX IDX_AUDIT_LOGS_USERNAME ON AUDIT_LOGS(USERNAME);
CREATE INDEX IDX_AUDIT_LOGS_USER_ACTION ON AUDIT_LOGS(USER_ID, ACTION);

CREATE INDEX IDX_MIGRATION_HISTORY_VERSION ON MIGRATION_HISTORY(VERSION);
CREATE INDEX IDX_MIGRATION_HISTORY_APPLIED_AT ON MIGRATION_HISTORY(APPLIED_AT);

-- ===== ПРАВА ДОСТУПА =====

-- Права для пользователя приложения
GRANT SELECT, INSERT, UPDATE, DELETE ON USERS TO KDV_LAUNCHER;
GRANT SELECT, INSERT, UPDATE, DELETE ON APPLICATIONS TO KDV_LAUNCHER;
GRANT SELECT, INSERT, UPDATE, DELETE ON USER_SETTINGS TO KDV_LAUNCHER;
GRANT SELECT, INSERT ON AUDIT_LOGS TO KDV_LAUNCHER;
GRANT SELECT ON DATABASE_VERSION TO KDV_LAUNCHER;
GRANT SELECT, INSERT ON MIGRATION_HISTORY TO KDV_LAUNCHER;

-- Права на генераторы
GRANT USAGE ON GENERATOR GEN_USERS_ID TO KDV_LAUNCHER;
GRANT USAGE ON GENERATOR GEN_APPLICATIONS_ID TO KDV_LAUNCHER;
GRANT USAGE ON GENERATOR GEN_USER_SETTINGS_ID TO KDV_LAUNCHER;
GRANT USAGE ON GENERATOR GEN_AUDIT_LOGS_ID TO KDV_LAUNCHER;
GRANT USAGE ON GENERATOR GEN_MIGRATION_HISTORY_ID TO KDV_LAUNCHER;

-- Роли для разных уровней доступа
GRANT SELECT ON USERS TO LAUNCHER_READONLY;
GRANT SELECT ON APPLICATIONS TO LAUNCHER_READONLY;
GRANT SELECT ON USER_SETTINGS TO LAUNCHER_READONLY;
GRANT SELECT ON AUDIT_LOGS TO LAUNCHER_READONLY;

GRANT LAUNCHER_READONLY TO LAUNCHER_USER;
GRANT SELECT, INSERT, UPDATE ON USER_SETTINGS TO LAUNCHER_USER;
GRANT INSERT ON AUDIT_LOGS TO LAUNCHER_USER;

GRANT LAUNCHER_USER TO LAUNCHER_ADMIN;
GRANT SELECT, INSERT, UPDATE, DELETE ON USERS TO LAUNCHER_ADMIN;
GRANT SELECT, INSERT, UPDATE, DELETE ON APPLICATIONS TO LAUNCHER_ADMIN;

-- ===== НАЧАЛЬНЫЕ ДАННЫЕ =====
INSERT INTO DATABASE_VERSION (VERSION, APPLIED_AT, APPLICATION_VERSION) 
VALUES ('1.0.0.001', CURRENT_TIMESTAMP, '1.0.0');

INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT) 
VALUES ('1.0.0.001', 'InitialSchema', 'Create initial database schema with all tables and indexes for server deployment', CURRENT_TIMESTAMP);

-- Базовый пользователь (guest) для первоначального доступа
-- ПРИМЕЧАНИЕ: Пользователь 'serviceadmin' для администрирования создается автоматически
-- сервисом AuthenticationConfigurationService при первом запуске через auth-config.json
INSERT INTO USERS (USERNAME, DISPLAY_NAME, EMAIL, ROLE, IS_ACTIVE, IS_SERVICE_ACCOUNT, PASSWORD_HASH, SALT, CREATED_AT, AUTHENTICATION_TYPE, DOMAIN_USERNAME, IS_LOCAL_USER, ALLOW_LOCAL_LOGIN, FAILED_LOGIN_ATTEMPTS, IS_LOCKED, GROUPS_JSON, SETTINGS_JSON, METADATA_JSON) 
VALUES ('guest', 'Guest User', 'guest@local', 2, 1, 0, '', '', CURRENT_TIMESTAMP, 0, '', 1, 0, 0, 0, '[]', '{}', '{}');

-- Базовые приложения
INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
VALUES ('Calculator', 'Windows Calculator', 'calc.exe', '🧮', 'Utilities', 0, 2, 1, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
VALUES ('Notepad', 'Text Editor', 'notepad.exe', '📝', 'Utilities', 0, 2, 1, 2, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
VALUES ('Google', 'Google Search', 'https://www.google.com', '🌐', 'Web', 1, 2, 1, 3, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
VALUES ('Control Panel', 'Windows Control Panel', 'control.exe', '⚙️', 'System', 0, 1, 1, 4, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '["LauncherPowerUsers", "LauncherAdmins"]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS) 
VALUES ('Command Prompt', 'Windows Command Line', 'cmd.exe', '💻', 'System', 0, 1, 1, 5, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

COMMIT;

-- ===== ПРОВЕРКА И ДИАГНОСТИКА =====
SELECT 'Server Firebird database v1.0.0.001 created successfully!' AS STATUS FROM RDB$DATABASE;
SELECT * FROM DATABASE_VERSION;
SELECT COUNT(*) AS USER_COUNT FROM USERS;
SELECT COUNT(*) AS APP_COUNT FROM APPLICATIONS;

-- Проверка ролей
SELECT RDB$ROLE_NAME FROM RDB$ROLES WHERE RDB$SYSTEM_FLAG = 0;

-- Проверка прав пользователя
SELECT 
    RDB$USER,
    RDB$RELATION_NAME,
    RDB$PRIVILEGE
FROM RDB$USER_PRIVILEGES 
WHERE RDB$USER = 'KDV_LAUNCHER'
ORDER BY RDB$RELATION_NAME, RDB$PRIVILEGE;

/*
===== ИНСТРУКЦИИ ПО РАЗВЕРТЫВАНИЮ FIREBIRD SERVER =====

1. ПОДГОТОВКА СЕРВЕРА:
   - Установите Firebird Server 3.0+ или 4.0+
   - Настройте firebird.conf (безопасность, порты, память)
   - Запустите службу Firebird
   - Настройте firewall для порта 3050

2. СОЗДАНИЕ ПОЛЬЗОВАТЕЛЯ ПРИЛОЖЕНИЯ:
   isql localhost:security3.fdb -user SYSDBA -password your_sysdba_password
   CREATE USER KDV_LAUNCHER PASSWORD 'KDV_L@unch3r_S3cur3_2025!';
   QUIT;

3. СОЗДАНИЕ БД:
   - Отредактируйте пути и пароли в скрипте
   - Выполните: isql -i create_firebird_server_v1.0.0.001.sql

4. НАСТРОЙКА ПРИЛОЖЕНИЯ:
   {
     "DatabaseType": "Firebird",
     "Server": "localhost", 
     "Port": 3050,
     "DatabasePath": "C:\\FirebirdData\\WindowsLauncher\\launcher_server.fdb",
     "Username": "KDV_LAUNCHER",
     "Password": "KDV_L@unch3r_S3cur3_2025!",
     "ConnectionMode": "Server",
     "ConnectionTimeout": 30
   }

5. МОНИТОРИНГ:
   - Используйте gstat для статистики БД
   - Настройте fbaudit для аудита
   - Используйте fb_lock_print для мониторинга блокировок

6. BACKUP:
   gbak -b -user KDV_LAUNCHER -password your_password localhost:launcher_server.fdb launcher_backup.fbk

ВАЖНЫЕ НАСТРОЙКИ БЕЗОПАСНОСТИ:
- Измените пароль SYSDBA после установки
- Используйте отдельного пользователя (не SYSDBA) для приложения
- Настройте SSL/TLS для удаленных подключений
- Ограничьте доступ к серверу через firewall
- Регулярно создавайте backup'ы
*/
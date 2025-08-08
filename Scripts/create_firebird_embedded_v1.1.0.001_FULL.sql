-- ===== create_firebird_embedded_v1.1.0.001_FULL.sql =====
-- Полный скрипт для Firebird 5.0 Embedded с SMTP функциональностью
-- Включает все таблицы из версий 1.0.0.001 + 1.1.0.001 (Email Support)
-- Для локальных установок с Firebird Embedded 5.0+
-- 
-- ПРЕДВАРИТЕЛЬНЫЕ ТРЕБОВАНИЯ:
-- 1. Firebird Embedded 5.0+ файлы скопированы в каталог приложения
-- 2. Настроена структура каталогов для БД
-- 3. Приложение запускается с правами записи в каталог БД

-- ===== ПАРАМЕТРЫ EMBEDDED РЕЖИМА =====
-- ВАЖНО: В embedded режиме используется прямой путь к файлу БД
-- Рекомендуемые пути:
-- - C:\WindowsLauncher\Data\launcher_embedded.fdb (установка)  
-- - %AppData%\WindowsLauncher\launcher_embedded.fdb (пользовательские данные)
-- Пользователь: SYSDBA (встроенный в embedded)
-- Пароль: может быть любым или пустым (игнорируется в embedded)

-- ===== СОЗДАНИЕ БАЗЫ ДАННЫХ EMBEDDED =====
-- ВАЖНО: В embedded режиме путь должен быть абсолютным

CREATE DATABASE 'C:\WindowsLauncher\Data\launcher_embedded.fdb'
PAGE_SIZE 16384
USER 'SYSDBA' 
PASSWORD 'embedded'
DEFAULT CHARACTER SET UTF8;

-- Подключение к созданной БД (embedded автоматически использует SYSDBA)
CONNECT 'C:\WindowsLauncher\Data\launcher_embedded.fdb' 
USER 'SYSDBA' 
PASSWORD 'embedded';

-- ===== СОЗДАНИЕ ОСНОВНЫХ ТАБЛИЦ (v1.0.0.001) =====

-- Таблица пользователей (Firebird 5.0 embedded совместимый синтаксис)
CREATE TABLE USERS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    USERNAME VARCHAR(100) NOT NULL,
    DISPLAY_NAME VARCHAR(200) NOT NULL,
    EMAIL VARCHAR(255),
    ROLE INTEGER NOT NULL,
    IS_ACTIVE BOOLEAN NOT NULL,
    IS_SERVICE_ACCOUNT BOOLEAN NOT NULL,
    PASSWORD_HASH VARCHAR(500),
    SALT VARCHAR(500),
    CREATED_AT TIMESTAMP NOT NULL,
    LAST_LOGIN_AT TIMESTAMP,
    LAST_ACTIVITY_AT TIMESTAMP,
    FAILED_LOGIN_ATTEMPTS INTEGER NOT NULL,
    IS_LOCKED BOOLEAN NOT NULL,
    LOCKOUT_END TIMESTAMP,
    LAST_PASSWORD_CHANGE TIMESTAMP,
    GROUPS_JSON VARCHAR(2000),
    SETTINGS_JSON VARCHAR(4000),
    METADATA_JSON VARCHAR(2000),
    AUTHENTICATION_TYPE INTEGER NOT NULL,
    DOMAIN_USERNAME VARCHAR(100),
    LAST_DOMAIN_SYNC TIMESTAMP,
    IS_LOCAL_USER BOOLEAN NOT NULL,
    ALLOW_LOCAL_LOGIN BOOLEAN NOT NULL,
    CONSTRAINT UQ_USERS_USERNAME UNIQUE (USERNAME)
);

-- Таблица приложений
CREATE TABLE APPLICATIONS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    NAME VARCHAR(200) NOT NULL,
    DESCRIPTION VARCHAR(500),
    EXECUTABLE_PATH VARCHAR(1000) NOT NULL,
    ARGUMENTS VARCHAR(500),
    WORKING_DIRECTORY VARCHAR(1000),
    ICON_PATH VARCHAR(1000),
    ICONTEXT VARCHAR(50),
    CATEGORY VARCHAR(100),
    APP_TYPE INTEGER NOT NULL,
    MINIMUM_ROLE INTEGER NOT NULL,
    IS_ENABLED BOOLEAN NOT NULL,
    SORT_ORDER INTEGER NOT NULL,
    CREATED_DATE TIMESTAMP NOT NULL,
    MODIFIED_DATE TIMESTAMP NOT NULL,
    CREATED_BY VARCHAR(100),
    REQUIRED_GROUPS BLOB SUB_TYPE TEXT
);

-- Таблица настроек пользователей
CREATE TABLE USER_SETTINGS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    USER_ID INTEGER NOT NULL,
    THEME VARCHAR(50),
    ACCENT_COLOR VARCHAR(50),
    TILE_SIZE INTEGER,
    SHOW_CATEGORIES BOOLEAN,
    DEFAULT_CATEGORY VARCHAR(100),
    AUTO_REFRESH BOOLEAN,
    REFRESH_INTERVAL_MINUTES INTEGER,
    SHOW_DESCRIPTIONS BOOLEAN,
    HIDDEN_CATEGORIES BLOB SUB_TYPE TEXT,
    LAST_MODIFIED TIMESTAMP NOT NULL,
    CONSTRAINT FK_USER_SETTINGS_USER_ID FOREIGN KEY (USER_ID) 
        REFERENCES USERS(ID) ON DELETE CASCADE
);

-- Таблица аудита
CREATE TABLE AUDIT_LOGS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    USER_ID INTEGER,
    USERNAME VARCHAR(100) NOT NULL,
    ACTION VARCHAR(100) NOT NULL,
    APPLICATION_NAME VARCHAR(200),
    DETAILS BLOB SUB_TYPE TEXT,
    TIMESTAMP_UTC TIMESTAMP NOT NULL,
    SUCCESS BOOLEAN NOT NULL,
    ERROR_MESSAGE VARCHAR(1000),
    COMPUTER_NAME VARCHAR(100),
    IP_ADDRESS VARCHAR(45),
    USER_AGENT VARCHAR(500),
    METADATA_JSON BLOB SUB_TYPE TEXT,
    CONSTRAINT FK_AUDIT_LOGS_USER_ID FOREIGN KEY (USER_ID) 
        REFERENCES USERS(ID) ON DELETE SET NULL
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
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    VERSION VARCHAR(20) NOT NULL,
    NAME VARCHAR(200) NOT NULL,
    DESCRIPTION BLOB SUB_TYPE TEXT,
    APPLIED_AT TIMESTAMP NOT NULL,
    ROLLBACK_SCRIPT BLOB SUB_TYPE TEXT
);

-- ===== СОЗДАНИЕ EMAIL ТАБЛИЦ (v1.1.0.001) =====

-- Таблица контактов для адресной книги
CREATE TABLE CONTACTS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    FIRST_NAME VARCHAR(50) NOT NULL,
    LAST_NAME VARCHAR(50) NOT NULL,
    EMAIL VARCHAR(200) NOT NULL,
    PHONE VARCHAR(20),
    COMPANY VARCHAR(100),
    DEPARTMENT VARCHAR(50),
    GROUP_NAME VARCHAR(50),
    NOTES VARCHAR(500),
    IS_ACTIVE INTEGER NOT NULL DEFAULT 1,
    CREATED_AT TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UPDATED_AT TIMESTAMP,
    CREATED_BY VARCHAR(100)
);

-- Таблица настроек SMTP серверов
CREATE TABLE SMTP_SETTINGS (
    ID INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    HOST VARCHAR(200) NOT NULL,
    PORT INTEGER NOT NULL,
    USERNAME VARCHAR(200) NOT NULL,
    ENCRYPTED_PASSWORD VARCHAR(1000) NOT NULL,
    USE_SSL INTEGER NOT NULL DEFAULT 1,
    USE_STARTTLS INTEGER NOT NULL DEFAULT 0,
    SERVER_TYPE INTEGER NOT NULL,
    DEFAULT_FROM_EMAIL VARCHAR(250),
    DEFAULT_FROM_NAME VARCHAR(200),
    IS_ACTIVE INTEGER NOT NULL DEFAULT 1,
    CONSECUTIVE_ERRORS INTEGER NOT NULL DEFAULT 0,
    LAST_SUCCESSFUL_SEND TIMESTAMP,
    CREATED_AT TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UPDATED_AT TIMESTAMP
);

-- ===== ДОБАВЛЕНИЕ DEFAULT ЗНАЧЕНИЙ =====
-- Добавляем DEFAULT значения после создания таблиц (совместимость с Firebird 5.0)
ALTER TABLE USERS ALTER COLUMN ROLE SET DEFAULT 2;
ALTER TABLE USERS ALTER COLUMN IS_ACTIVE SET DEFAULT TRUE;
ALTER TABLE USERS ALTER COLUMN IS_SERVICE_ACCOUNT SET DEFAULT FALSE;
ALTER TABLE USERS ALTER COLUMN FAILED_LOGIN_ATTEMPTS SET DEFAULT 0;
ALTER TABLE USERS ALTER COLUMN IS_LOCKED SET DEFAULT FALSE;
ALTER TABLE USERS ALTER COLUMN GROUPS_JSON SET DEFAULT '[]';
ALTER TABLE USERS ALTER COLUMN SETTINGS_JSON SET DEFAULT '{}';
ALTER TABLE USERS ALTER COLUMN METADATA_JSON SET DEFAULT '{}';
ALTER TABLE USERS ALTER COLUMN AUTHENTICATION_TYPE SET DEFAULT 0;
ALTER TABLE USERS ALTER COLUMN IS_LOCAL_USER SET DEFAULT TRUE;
ALTER TABLE USERS ALTER COLUMN ALLOW_LOCAL_LOGIN SET DEFAULT FALSE;

ALTER TABLE APPLICATIONS ALTER COLUMN ICONTEXT SET DEFAULT '📱';
ALTER TABLE APPLICATIONS ALTER COLUMN APP_TYPE SET DEFAULT 1;
ALTER TABLE APPLICATIONS ALTER COLUMN MINIMUM_ROLE SET DEFAULT 2;
ALTER TABLE APPLICATIONS ALTER COLUMN IS_ENABLED SET DEFAULT TRUE;
ALTER TABLE APPLICATIONS ALTER COLUMN SORT_ORDER SET DEFAULT 0;
ALTER TABLE APPLICATIONS ALTER COLUMN REQUIRED_GROUPS SET DEFAULT '[]';

ALTER TABLE USER_SETTINGS ALTER COLUMN THEME SET DEFAULT 'Light';
ALTER TABLE USER_SETTINGS ALTER COLUMN ACCENT_COLOR SET DEFAULT 'Blue';
ALTER TABLE USER_SETTINGS ALTER COLUMN TILE_SIZE SET DEFAULT 150;
ALTER TABLE USER_SETTINGS ALTER COLUMN SHOW_CATEGORIES SET DEFAULT TRUE;
ALTER TABLE USER_SETTINGS ALTER COLUMN DEFAULT_CATEGORY SET DEFAULT 'All';
ALTER TABLE USER_SETTINGS ALTER COLUMN AUTO_REFRESH SET DEFAULT TRUE;
ALTER TABLE USER_SETTINGS ALTER COLUMN REFRESH_INTERVAL_MINUTES SET DEFAULT 30;
ALTER TABLE USER_SETTINGS ALTER COLUMN SHOW_DESCRIPTIONS SET DEFAULT TRUE;
ALTER TABLE USER_SETTINGS ALTER COLUMN HIDDEN_CATEGORIES SET DEFAULT '[]';

ALTER TABLE AUDIT_LOGS ALTER COLUMN SUCCESS SET DEFAULT TRUE;
ALTER TABLE AUDIT_LOGS ALTER COLUMN METADATA_JSON SET DEFAULT '{}';

-- ===== СОЗДАНИЕ ИНДЕКСОВ =====

-- Индексы для основных таблиц
CREATE INDEX IDX_USERS_USERNAME ON USERS(USERNAME);
CREATE INDEX IDX_USERS_ROLE ON USERS(ROLE);
CREATE INDEX IDX_USERS_IS_ACTIVE ON USERS(IS_ACTIVE);
CREATE INDEX IDX_USERS_AUTHENTICATION_TYPE ON USERS(AUTHENTICATION_TYPE);
CREATE INDEX IDX_USERS_LAST_LOGIN_AT ON USERS(LAST_LOGIN_AT);

CREATE INDEX IDX_APPLICATIONS_NAME ON APPLICATIONS(NAME);
CREATE INDEX IDX_APPLICATIONS_CATEGORY ON APPLICATIONS(CATEGORY);
CREATE INDEX IDX_APPLICATIONS_IS_ENABLED ON APPLICATIONS(IS_ENABLED);
CREATE INDEX IDX_APPLICATIONS_APP_TYPE ON APPLICATIONS(APP_TYPE);
CREATE INDEX IDX_APPLICATIONS_MINIMUM_ROLE ON APPLICATIONS(MINIMUM_ROLE);
CREATE INDEX IDX_APPLICATIONS_SORT_ORDER ON APPLICATIONS(SORT_ORDER);

CREATE INDEX IDX_USER_SETTINGS_USER_ID ON USER_SETTINGS(USER_ID);

CREATE INDEX IDX_AUDIT_LOGS_TIMESTAMP_UTC ON AUDIT_LOGS(TIMESTAMP_UTC);
CREATE INDEX IDX_AUDIT_LOGS_USER_ID ON AUDIT_LOGS(USER_ID);
CREATE INDEX IDX_AUDIT_LOGS_ACTION ON AUDIT_LOGS(ACTION);
CREATE INDEX IDX_AUDIT_LOGS_USERNAME ON AUDIT_LOGS(USERNAME);

CREATE INDEX IDX_MIGRATION_HISTORY_VERSION ON MIGRATION_HISTORY(VERSION);
CREATE INDEX IDX_MIGRATION_HISTORY_APPLIED_AT ON MIGRATION_HISTORY(APPLIED_AT);

-- Индексы для email таблиц
CREATE UNIQUE INDEX IX_CONTACTS_EMAIL_ACTIVE ON CONTACTS (EMAIL, IS_ACTIVE);
CREATE INDEX IX_CONTACTS_FIRST_NAME ON CONTACTS (FIRST_NAME);
CREATE INDEX IX_CONTACTS_LAST_NAME ON CONTACTS (LAST_NAME);
CREATE INDEX IX_CONTACTS_EMAIL ON CONTACTS (EMAIL);
CREATE INDEX IX_CONTACTS_GROUP ON CONTACTS (GROUP_NAME);
CREATE INDEX IX_CONTACTS_DEPARTMENT ON CONTACTS (DEPARTMENT);
CREATE INDEX IX_CONTACTS_ACTIVE_CREATED ON CONTACTS (IS_ACTIVE, CREATED_AT);

CREATE UNIQUE INDEX IX_SMTP_SETTINGS_TYPE_ACTIVE ON SMTP_SETTINGS (SERVER_TYPE, IS_ACTIVE);
CREATE INDEX IX_SMTP_SETTINGS_HOST ON SMTP_SETTINGS (HOST);
CREATE INDEX IX_SMTP_SETTINGS_ACTIVE ON SMTP_SETTINGS (IS_ACTIVE);
CREATE INDEX IX_SMTP_SETTINGS_ERROR_MONITORING ON SMTP_SETTINGS (IS_ACTIVE, CONSECUTIVE_ERRORS, LAST_SUCCESSFUL_SEND);

-- ===== НАЧАЛЬНЫЕ ДАННЫЕ =====

-- Устанавливаем текущую версию БД (1.1.0.001 - включает SMTP функциональность)
INSERT INTO DATABASE_VERSION (VERSION, APPLIED_AT, APPLICATION_VERSION)
VALUES ('1.1.0.001', CURRENT_TIMESTAMP, '1.1.0');

-- Записываем историю миграций (обе миграции как выполненные)
INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
VALUES ('1.0.0.001', 'InitialSchema', 'Create initial database schema with all tables and indexes for Firebird 5.0 embedded deployment', CURRENT_TIMESTAMP);

INSERT INTO MIGRATION_HISTORY (VERSION, NAME, DESCRIPTION, APPLIED_AT)
VALUES ('1.1.0.001', 'AddEmailSupport', 'Add email functionality: contacts address book and SMTP settings with fallback support for embedded deployment', CURRENT_TIMESTAMP);

-- Базовый пользователь (guest) для embedded режима
INSERT INTO USERS (USERNAME, DISPLAY_NAME, EMAIL, ROLE, IS_ACTIVE, IS_SERVICE_ACCOUNT, 
                   PASSWORD_HASH, SALT, CREATED_AT, AUTHENTICATION_TYPE, DOMAIN_USERNAME, 
                   IS_LOCAL_USER, ALLOW_LOCAL_LOGIN, FAILED_LOGIN_ATTEMPTS, IS_LOCKED, 
                   GROUPS_JSON, SETTINGS_JSON, METADATA_JSON)
VALUES ('guest', 'Guest User', 'guest@local', 2, TRUE, FALSE, '', '', CURRENT_TIMESTAMP, 
        0, '', TRUE, TRUE, 0, FALSE, '[]', '{}', '{}');

-- Базовые приложения для embedded установки
INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, 
                         APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, 
                         MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS)
VALUES ('Calculator', 'Windows Calculator', 'calc.exe', '🧮', 'Utilities', 1, 2, TRUE, 1, 
        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, 
                         APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, 
                         MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS)
VALUES ('Notepad', 'Text Editor', 'notepad.exe', '📝', 'Utilities', 1, 2, TRUE, 2, 
        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, 
                         APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, 
                         MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS)
VALUES ('Google', 'Google Search', 'https://www.google.com', '🌐', 'Web', 2, 2, TRUE, 3, 
        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, 
                         APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, 
                         MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS)
VALUES ('Control Panel', 'Windows Control Panel', 'control.exe', '⚙️', 'System', 1, 1, TRUE, 4, 
        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '["LauncherPowerUsers", "LauncherAdmins"]');

INSERT INTO APPLICATIONS (NAME, DESCRIPTION, EXECUTABLE_PATH, ICONTEXT, CATEGORY, 
                         APP_TYPE, MINIMUM_ROLE, IS_ENABLED, SORT_ORDER, CREATED_DATE, 
                         MODIFIED_DATE, CREATED_BY, REQUIRED_GROUPS)
VALUES ('Command Prompt', 'Windows Command Line', 'cmd.exe', '💻', 'System', 1, 2, TRUE, 5, 
        CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'System', '[]');

-- Начальные контакты для демонстрации (embedded установка)
INSERT INTO CONTACTS (FIRST_NAME, LAST_NAME, EMAIL, COMPANY, DEPARTMENT, GROUP_NAME, NOTES, IS_ACTIVE, CREATED_AT) 
SELECT 'Системный', 'Администратор', 'admin@company.local', 'KDV Corporation', 'IT отдел', 'IT отдел', 'Системный контакт для администрирования', 1, CURRENT_TIMESTAMP 
FROM RDB$DATABASE
WHERE NOT EXISTS (SELECT 1 FROM CONTACTS WHERE EMAIL = 'admin@company.local');

INSERT INTO CONTACTS (FIRST_NAME, LAST_NAME, EMAIL, COMPANY, DEPARTMENT, GROUP_NAME, NOTES, IS_ACTIVE, CREATED_AT) 
SELECT 'Техническая', 'Поддержка', 'support@company.local', 'KDV Corporation', 'IT отдел', 'IT отдел', 'Общий контакт технической поддержки', 1, CURRENT_TIMESTAMP 
FROM RDB$DATABASE
WHERE NOT EXISTS (SELECT 1 FROM CONTACTS WHERE EMAIL = 'support@company.local');

-- Добавляем пример локального контакта для embedded установки
INSERT INTO CONTACTS (FIRST_NAME, LAST_NAME, EMAIL, COMPANY, DEPARTMENT, GROUP_NAME, NOTES, IS_ACTIVE, CREATED_AT) 
SELECT 'Local', 'User', 'user@localhost', 'Local Company', 'Local Department', 'Local Users', 'Пример локального контакта для embedded режима', 1, CURRENT_TIMESTAMP 
FROM RDB$DATABASE
WHERE NOT EXISTS (SELECT 1 FROM CONTACTS WHERE EMAIL = 'user@localhost');

COMMIT;

-- Проверяем созданные таблицы
SELECT 'Firebird 5.0 embedded database v1.1.0.001 with SMTP support created successfully!' AS STATUS FROM RDB$DATABASE;

-- Показываем созданные таблицы
SELECT RDB$RELATION_NAME AS TABLE_NAME 
FROM RDB$RELATIONS 
WHERE RDB$VIEW_BLR IS NULL 
AND (RDB$SYSTEM_FLAG IS NULL OR RDB$SYSTEM_FLAG = 0)
ORDER BY RDB$RELATION_NAME;

/*
===== ИНСТРУКЦИИ ПО РАЗВЕРТЫВАНИЮ FIREBIRD EMBEDDED с SMTP =====

ПРЕИМУЩЕСТВА EMBEDDED РЕЖИМА:
- Не требует установки Firebird Server
- Не требует администрирования сервера
- Простое развертывание с приложением
- Полная автономность
- Подходит для локальных/персональных установок

1. ПОДГОТОВКА EMBEDDED ФАЙЛОВ:
   - Скачать Firebird 5.0 Embedded (ZIP архив)
   - Извлечь файлы в каталог приложения:
     * fbclient.dll (основная библиотека)
     * fbembed.dll (embedded движок)  
     * firebird.conf (конфигурация)
     * plugins/ (каталог с плагинами)
     * intl/ (интернационализация)

2. СТРУКТУРА КАТАЛОГОВ:
   WindowsLauncher/
   ├── WindowsLauncher.exe
   ├── fbclient.dll
   ├── fbembed.dll
   ├── firebird.conf
   ├── plugins/
   ├── intl/
   └── Data/
       └── launcher_embedded.fdb (создается скриптом)

3. СОЗДАНИЕ БД:
   ВАЖНО: Отредактируйте путь к БД в скрипте под ваши потребности!
   
   Варианты путей:
   - C:\WindowsLauncher\Data\launcher_embedded.fdb (системная установка)
   - %AppData%\WindowsLauncher\launcher_embedded.fdb (пользовательские данные)
   - .\Data\launcher_embedded.fdb (относительно EXE)
   
   Выполнение скрипта:
   isql -i create_firebird_embedded_v1.1.0.001_FULL.sql

4. НАСТРОЙКА ПРИЛОЖЕНИЯ:
   {
     "DatabaseType": "Firebird",
     "DatabasePath": "C:\\WindowsLauncher\\Data\\launcher_embedded.fdb",
     "Username": "SYSDBA",
     "Password": "embedded",
     "ConnectionMode": "Embedded",
     "ConnectionTimeout": 30
   }

ОСОБЕННОСТИ EMBEDDED РЕЖИМА:
- Пользователь всегда SYSDBA (пароль игнорируется)
- Нет сетевого доступа - только локальные подключения
- Файл БД блокируется во время работы приложения
- Автоматическое управление памятью и кешем
- Меньше настроек безопасности

РЕКОМЕНДАЦИИ ПО РАЗВЕРТЫВАНИЮ:
- Создайте каталог Data\ для файлов БД
- Настройте backup и восстановление БД
- Документируйте путь к файлу БД для поддержки
- Учитывайте права доступа к каталогу БД
- Планируйте миграцию на Server при росте нагрузки

КОМАНДЫ ДЛЯ СОЗДАНИЯ (пример):
1. cd C:\WindowsLauncher
2. mkdir Data
3. isql -i Scripts\create_firebird_embedded_v1.1.0.001_FULL.sql

ПОСЛЕ СОЗДАНИЯ БД:
- Настройте SMTP серверы через админ панель приложения
- Импортируйте контакты из CSV если необходимо
- Проверьте отправку тестового email
- Настройте регулярные backup файла БД

МИГРАЦИЯ НА SERVER (при необходимости):
- Создайте backup: gbak -b launcher_embedded.fdb backup.fbk
- Разверните Server версию Firebird
- Восстановите БД на сервере: gbak -r backup.fbk server_database.fdb
- Измените конфигурацию приложения на Server режим

НОВЫЕ ВОЗМОЖНОСТИ В v1.1.0.001:
- Адресная книга с поддержкой групп и департаментов
- SMTP настройки с fallback серверами (Primary/Backup)
- Шифрование паролей SMTP серверов
- Мониторинг ошибок отправки email
- CSV импорт/экспорт контактов
- Интеграция с системой аудита
- Оптимизировано для embedded режима
*/
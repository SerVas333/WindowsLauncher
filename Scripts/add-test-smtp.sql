-- ===== ТЕСТОВЫЕ SMTP НАСТРОЙКИ ДЛЯ РАЗРАБОТКИ =====
-- ВНИМАНИЕ: Этот файл содержит тестовые настройки и должен использоваться только для разработки!
-- В производственной среде настройки SMTP должны быть настроены через админ-панель

-- Для SQLite база данных:
-- 1. Gmail SMTP (Основной сервер)
INSERT OR IGNORE INTO SMTP_SETTINGS (
    HOST, 
    PORT, 
    USERNAME, 
    ENCRYPTED_PASSWORD,  -- Зашифрованный пароль
    USE_SSL, 
    USE_STARTTLS, 
    SERVER_TYPE, 
    DEFAULT_FROM_EMAIL, 
    DEFAULT_FROM_NAME,
    IS_ACTIVE,
    CONSECUTIVE_ERRORS,
    CREATED_AT
) VALUES (
    'smtp.gmail.com',
    587,
    'test.launcher@gmail.com',
    'ENCRYPTED_TEST_PASSWORD_HERE',  -- Замените на зашифрованный пароль
    0,  -- USE_SSL = false
    1,  -- USE_STARTTLS = true
    0,  -- ServerType.Primary
    'test.launcher@gmail.com',
    'Windows Launcher Test',
    1,  -- IS_ACTIVE = true
    0,  -- CONSECUTIVE_ERRORS = 0
    datetime('now')
);

-- 2. Outlook SMTP (Резервный сервер) 
INSERT OR IGNORE INTO SMTP_SETTINGS (
    HOST, 
    PORT, 
    USERNAME, 
    ENCRYPTED_PASSWORD,
    USE_SSL, 
    USE_STARTTLS, 
    SERVER_TYPE, 
    DEFAULT_FROM_EMAIL, 
    DEFAULT_FROM_NAME,
    IS_ACTIVE,
    CONSECUTIVE_ERRORS,
    CREATED_AT
) VALUES (
    'smtp-mail.outlook.com',
    587,
    'test.launcher@outlook.com',
    'ENCRYPTED_TEST_PASSWORD_HERE_2',  -- Замените на зашифрованный пароль
    0,  -- USE_SSL = false
    1,  -- USE_STARTTLS = true  
    1,  -- ServerType.Backup
    'test.launcher@outlook.com',
    'Windows Launcher Backup',
    1,  -- IS_ACTIVE = true
    0,  -- CONSECUTIVE_ERRORS = 0
    datetime('now')
);

-- ===== Для Firebird база данных (аналогичные записи): =====

-- INSERT INTO SMTP_SETTINGS (
--     HOST, PORT, USERNAME, ENCRYPTED_PASSWORD, USE_SSL, USE_STARTTLS, 
--     SERVER_TYPE, DEFAULT_FROM_EMAIL, DEFAULT_FROM_NAME, IS_ACTIVE, CONSECUTIVE_ERRORS, CREATED_AT
-- ) 
-- SELECT 'smtp.gmail.com', 587, 'test.launcher@gmail.com', 'ENCRYPTED_TEST_PASSWORD_HERE', 0, 1, 0, 
--        'test.launcher@gmail.com', 'Windows Launcher Test', 1, 0, CURRENT_TIMESTAMP
-- FROM RDB$DATABASE
-- WHERE NOT EXISTS (SELECT 1 FROM SMTP_SETTINGS WHERE SERVER_TYPE = 0);
--
-- INSERT INTO SMTP_SETTINGS (
--     HOST, PORT, USERNAME, ENCRYPTED_PASSWORD, USE_SSL, USE_STARTTLS, 
--     SERVER_TYPE, DEFAULT_FROM_EMAIL, DEFAULT_FROM_NAME, IS_ACTIVE, CONSECUTIVE_ERRORS, CREATED_AT
-- )
-- SELECT 'smtp-mail.outlook.com', 587, 'test.launcher@outlook.com', 'ENCRYPTED_TEST_PASSWORD_HERE_2', 0, 1, 1,
--        'test.launcher@outlook.com', 'Windows Launcher Backup', 1, 0, CURRENT_TIMESTAMP  
-- FROM RDB$DATABASE
-- WHERE NOT EXISTS (SELECT 1 FROM SMTP_SETTINGS WHERE SERVER_TYPE = 1);

-- ===== ИНСТРУКЦИИ ПО ИСПОЛЬЗОВАНИЮ =====
--
-- 1. Создайте тестовые email аккаунты в Gmail и Outlook
-- 2. Включите двухфакторную аутентификацию
-- 3. Создайте app-specific пароли для SMTP доступа
-- 4. Зашифруйте пароли с помощью EncryptionService
-- 5. Замените ENCRYPTED_TEST_PASSWORD_HERE на зашифрованные пароли
-- 6. Выполните этот скрипт в SQLite или раскомментируйте Firebird часть
--
-- Для шифрования паролей используйте код:
-- var encryptionService = serviceProvider.GetService<IEncryptionService>();
-- var encryptedPassword = encryptionService.Encrypt("your_app_password");
--
-- ВАЖНО: Никогда не коммитьте реальные пароли в репозиторий!
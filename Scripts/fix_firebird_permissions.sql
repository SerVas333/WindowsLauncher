-- ===== fix_firebird_permissions.sql =====
-- Скрипт для исправления прав доступа к уже созданной БД Firebird
-- Выполните под SYSDBA для предоставления дополнительных прав пользователю KDV_LAUNCHER

-- Подключение к БД (замените на ваш алиас/путь)
CONNECT 'KDV_LAUNCHER' 
USER 'SYSDBA' 
PASSWORD 'Ghtgjyf1';

-- Предоставление дополнительных прав для системы миграций
GRANT SELECT, INSERT, UPDATE, DELETE ON DATABASE_VERSION TO KDV_LAUNCHER;
GRANT SELECT, INSERT, UPDATE, DELETE ON MIGRATION_HISTORY TO KDV_LAUNCHER;

-- Проверка предоставленных прав
SELECT 
    RDB$USER,
    RDB$RELATION_NAME,
    RDB$PRIVILEGE
FROM RDB$USER_PRIVILEGES 
WHERE RDB$USER = 'KDV_LAUNCHER'
  AND RDB$RELATION_NAME IN ('DATABASE_VERSION', 'MIGRATION_HISTORY')
ORDER BY RDB$RELATION_NAME, RDB$PRIVILEGE;

COMMIT;

-- Сообщение об успешном выполнении
SELECT 'Права доступа для KDV_LAUNCHER обновлены успешно!' AS STATUS FROM RDB$DATABASE;

/*
ИНСТРУКЦИЯ ПО ПРИМЕНЕНИЮ:

1. Сохраните этот файл как fix_firebird_permissions.sql
2. Отредактируйте строку подключения (CONNECT) под ваши настройки
3. Выполните: isql -i fix_firebird_permissions.sql
4. Перезапустите приложение

После выполнения пользователь KDV_LAUNCHER будет иметь полные права
на таблицы DATABASE_VERSION и MIGRATION_HISTORY, что необходимо
для корректной работы системы миграций.
*/
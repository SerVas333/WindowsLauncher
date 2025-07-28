-- ===== fix_application_types.sql =====
-- Скрипт для исправления типов приложений в существующей БД
-- Исправляет значения APP_TYPE согласно enum ApplicationType

-- Подключение к БД (замените на ваш алиас/путь)
CONNECT 'KDV_LAUNCHER' 
USER 'SYSDBA' 
PASSWORD 'Ghtgjyf1';

-- ===== ИСПРАВЛЕНИЕ ТИПОВ ПРИЛОЖЕНИЙ =====
-- ApplicationType enum:
-- Desktop = 1  (.exe приложения Windows)
-- Web = 2      (URL ссылки, откроются в браузере)  
-- Folder = 3   (Папки в проводнике)

-- Исправляем Desktop приложения (calc, notepad, control, cmd)
UPDATE APPLICATIONS 
SET APP_TYPE = 1 
WHERE EXECUTABLE_PATH LIKE '%.exe' 
   OR EXECUTABLE_PATH IN ('calc.exe', 'notepad.exe', 'control.exe', 'cmd.exe');

-- Исправляем Web приложения (URL ссылки)
UPDATE APPLICATIONS 
SET APP_TYPE = 2 
WHERE EXECUTABLE_PATH LIKE 'http%' 
   OR EXECUTABLE_PATH LIKE 'https%'
   OR EXECUTABLE_PATH LIKE 'www.%';

-- Исправляем Folder приложения (пути к папкам)
UPDATE APPLICATIONS 
SET APP_TYPE = 3 
WHERE (EXECUTABLE_PATH LIKE 'C:\%' OR EXECUTABLE_PATH LIKE '\\%') 
  AND EXECUTABLE_PATH NOT LIKE '%.exe'
  AND EXECUTABLE_PATH NOT LIKE 'http%';

COMMIT;

-- ===== ПРОВЕРКА РЕЗУЛЬТАТА =====
-- Показываем все приложения с их типами
SELECT 
    NAME,
    EXECUTABLE_PATH,
    APP_TYPE,
    CASE APP_TYPE
        WHEN 1 THEN 'Desktop'
        WHEN 2 THEN 'Web'
        WHEN 3 THEN 'Folder'
        ELSE 'Unknown'
    END AS TYPE_NAME
FROM APPLICATIONS
ORDER BY APP_TYPE, NAME;

-- Статистика по типам
SELECT 
    APP_TYPE,
    CASE APP_TYPE
        WHEN 1 THEN 'Desktop'
        WHEN 2 THEN 'Web'
        WHEN 3 THEN 'Folder'
        ELSE 'Unknown'
    END AS TYPE_NAME,
    COUNT(*) AS COUNT
FROM APPLICATIONS
GROUP BY APP_TYPE
ORDER BY APP_TYPE;

-- Сообщение об успешном выполнении
SELECT 'Типы приложений исправлены успешно!' AS STATUS FROM RDB$DATABASE;

/*
ИНСТРУКЦИЯ ПО ПРИМЕНЕНИЮ:

1. Остановите приложение
2. Выполните скрипт: isql -i fix_application_types.sql
3. Перезапустите приложение

РЕЗУЛЬТАТ:
- Calculator, Notepad, Control Panel, Command Prompt → Desktop (1)
- Google → Web (2)
- Папки (если есть) → Folder (3)

После выполнения все приложения будут запускаться корректно
в соответствии с их типом.
*/
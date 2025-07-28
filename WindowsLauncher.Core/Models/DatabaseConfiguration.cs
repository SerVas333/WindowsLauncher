using System.ComponentModel.DataAnnotations;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Core.Models
{
    /// <summary>
    /// Конфигурация подключения к базе данных
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Тип базы данных (SQLite, Firebird)
        /// </summary>
        public DatabaseType DatabaseType { get; set; } = DatabaseType.SQLite;

        /// <summary>
        /// Режим подключения для Firebird (Embedded, ClientServer)
        /// </summary>
        public FirebirdConnectionMode ConnectionMode { get; set; } = FirebirdConnectionMode.Embedded;

        /// <summary>
        /// Путь к файлу базы данных (для SQLite и Firebird Embedded) или имя БД (для Firebird Client/Server)
        /// </summary>
        public string DatabasePath { get; set; } = "launcher.db";

        /// <summary>
        /// Сервер базы данных (для Firebird Client-Server)
        /// </summary>
        public string? Server { get; set; }

        /// <summary>
        /// Порт сервера (для Firebird Client-Server)
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Имя пользователя базы данных
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Пароль пользователя базы данных
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Диалект Firebird (обычно 3)
        /// </summary>
        public int Dialect { get; set; }

        /// <summary>
        /// Размер страницы для новых баз данных Firebird
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Кодировка для Firebird
        /// </summary>
        public string Charset { get; set; } = string.Empty;

        /// <summary>
        /// Таймаут соединения в секундах
        /// </summary>
        public int ConnectionTimeout { get; set; }

        /// <summary>
        /// Строка подключения для SQLite
        /// </summary>
        public string GetSQLiteConnectionString()
        {
            return $"Data Source={DatabasePath};";
        }

        /// <summary>
        /// Строка подключения для Firebird
        /// </summary>
        public string GetFirebirdConnectionString()
        {
            if (ConnectionMode == FirebirdConnectionMode.Embedded)
            {
                // Для Embedded - путь к файлу
                return $"database={DatabasePath};user={Username};password={Password};dialect={Dialect};charset={Charset};connection timeout={ConnectionTimeout};servertype=1";
            }
            else
            {
                // Для Full Server используем legacy синтаксис: host[/port]:database_or_alias
                string connectionString;
                if (Port != 3050)
                {
                    // Нестандартный порт
                    connectionString = $"database={Server}/{Port}:{DatabasePath};user={Username};password={Password};dialect={Dialect};charset={Charset};connection timeout={ConnectionTimeout}";
                }
                else
                {
                    // Стандартный порт 3050
                    connectionString = $"database={Server}:{DatabasePath};user={Username};password={Password};dialect={Dialect};charset={Charset};connection timeout={ConnectionTimeout}";
                }
                return connectionString;
            }
        }

        /// <summary>
        /// Получить активную строку подключения в зависимости от типа БД
        /// </summary>
        public string GetConnectionString()
        {
            return DatabaseType switch
            {
                DatabaseType.SQLite => GetSQLiteConnectionString(),
                DatabaseType.Firebird => GetFirebirdConnectionString(),
                _ => throw new ArgumentException($"Неподдерживаемый тип базы данных: {DatabaseType}")
            };
        }

        /// <summary>
        /// Валидация конфигурации
        /// </summary>
        public DatabaseValidationResult ValidateConfiguration()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(DatabasePath))
            {
                errors.Add("Путь к базе данных не может быть пустым");
            }

            if (DatabaseType == DatabaseType.Firebird)
            {
                if (ConnectionMode == FirebirdConnectionMode.ClientServer)
                {
                    if (string.IsNullOrEmpty(Server))
                    {
                        errors.Add("Сервер базы данных не может быть пустым для Client-Server режима");
                    }

                    if (Port <= 0 || Port > 65535)
                    {
                        errors.Add("Порт должен быть в диапазоне 1-65535");
                    }

                    // Предупреждение о ограничениях DatabaseAccess
                    if (!string.IsNullOrEmpty(DatabasePath) && 
                        DatabasePath.Contains("\\") && 
                        !DatabasePath.StartsWith("C:\\DataBase", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Путь к базе данных должен находиться в папке C:\\DataBase (ограничение сервера)");
                    }
                }

                if (string.IsNullOrEmpty(Username))
                {
                    errors.Add("Имя пользователя не может быть пустым");
                }

                if (string.IsNullOrEmpty(Password))
                {
                    errors.Add("Пароль не может быть пустым");
                }
            }

            return new DatabaseValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray()
            };
        }
    }

    /// <summary>
    /// Тип базы данных
    /// </summary>
    public enum DatabaseType
    {
        SQLite,
        Firebird
    }

    /// <summary>
    /// Режим подключения к Firebird
    /// </summary>
    public enum FirebirdConnectionMode
    {
        /// <summary>
        /// Встроенный режим (локальная БД)
        /// </summary>
        Embedded,
        
        /// <summary>
        /// Клиент-сервер режим (удаленная БД)
        /// </summary>
        ClientServer
    }

}
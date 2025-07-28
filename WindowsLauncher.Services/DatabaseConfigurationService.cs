using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Data.Sqlite;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис управления конфигурацией базы данных
    /// </summary>
    public class DatabaseConfigurationService : IDatabaseConfigurationService
    {
        private readonly ILogger<DatabaseConfigurationService> _logger;
        private readonly IEncryptionService _encryptionService;
        private readonly string _configPath;
        private const string ConfigFileName = "database-config.json";

        public DatabaseConfigurationService(
            ILogger<DatabaseConfigurationService> logger,
            IEncryptionService encryptionService)
        {
            _logger = logger;
            _encryptionService = encryptionService;
            
            // Сохраняем конфигурацию в папке приложения
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsLauncher"
            );
            
            Directory.CreateDirectory(appDataPath);
            _configPath = Path.Combine(appDataPath, ConfigFileName);
        }

        public async Task<DatabaseConfiguration> GetConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation("Database configuration file not found, creating default configuration");
                    var defaultConfig = GetDefaultConfiguration();
                    await SaveConfigurationAsync(defaultConfig);
                    return defaultConfig;
                }

                var json = await File.ReadAllTextAsync(_configPath);
                var configuration = JsonSerializer.Deserialize<DatabaseConfiguration>(json);
                
                if (configuration == null)
                {
                    _logger.LogWarning("Failed to deserialize database configuration, using default");
                    return GetDefaultConfiguration();
                }

                // Проверяем, нужна ли миграция (если пароль не зашифрован)
                if (!_encryptionService.IsEncrypted(configuration.Password))
                {
                    _logger.LogInformation("Migrating unencrypted password to encrypted format");
                    await MigrateUnencryptedConfigurationAsync(configuration);
                }

                // Расшифровываем пароль при загрузке
                configuration.Password = _encryptionService.Decrypt(configuration.Password);

                _logger.LogDebug("Database configuration loaded successfully");
                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading database configuration");
                return GetDefaultConfiguration();
            }
        }

        public async Task SaveConfigurationAsync(DatabaseConfiguration configuration)
        {
            try
            {
                var validation = configuration.ValidateConfiguration();
                if (!validation.IsValid)
                {
                    throw new ArgumentException($"Invalid configuration: {string.Join(", ", validation.Errors)}");
                }

                // Создаем копию конфигурации для сохранения с зашифрованным паролем
                var configToSave = new DatabaseConfiguration
                {
                    DatabaseType = configuration.DatabaseType,
                    ConnectionMode = configuration.ConnectionMode,
                    DatabasePath = configuration.DatabasePath,
                    Server = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Server : null,
                    Port = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Port : 0,
                    Username = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Username : null,
                    Password = configuration.DatabaseType == DatabaseType.Firebird ? _encryptionService.Encrypt(configuration.Password) : null,
                    Dialect = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Dialect : 0,
                    PageSize = configuration.DatabaseType == DatabaseType.Firebird ? configuration.PageSize : 0,
                    Charset = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Charset : null,
                    ConnectionTimeout = configuration.DatabaseType == DatabaseType.Firebird ? configuration.ConnectionTimeout : 0
                };

                var json = JsonSerializer.Serialize(configToSave, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                await File.WriteAllTextAsync(_configPath, json);
                _logger.LogInformation("Database configuration saved successfully (password encrypted)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving database configuration");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync(DatabaseConfiguration configuration)
        {
            try
            {
                _logger.LogInformation("Testing database connection for {DatabaseType} in {Mode} mode", 
                    configuration.DatabaseType, configuration.ConnectionMode);

                switch (configuration.DatabaseType)
                {
                    case DatabaseType.SQLite:
                        return await TestSQLiteConnectionAsync(configuration);
                    
                    case DatabaseType.Firebird:
                        return await TestFirebirdConnectionAsync(configuration);
                    
                    default:
                        _logger.LogError("Unsupported database type: {DatabaseType}", configuration.DatabaseType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database connection");
                return false;
            }
        }

        private async Task<bool> TestSQLiteConnectionAsync(DatabaseConfiguration configuration)
        {
            try
            {
                using var connection = new SqliteConnection(configuration.GetSQLiteConnectionString());
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                var result = await command.ExecuteScalarAsync();
                
                return result != null && result.ToString() == "1";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQLite connection test failed");
                return false;
            }
        }

        private async Task<bool> TestFirebirdConnectionAsync(DatabaseConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetFirebirdConnectionString();
                _logger.LogInformation("Testing Firebird connection with string: {ConnectionString}", 
                    connectionString.Replace(configuration.Password, "***"));
                
                using var connection = new FbConnection(connectionString);
                await connection.OpenAsync();
                
                _logger.LogInformation("Firebird connection opened successfully");
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM RDB$DATABASE";
                var result = await command.ExecuteScalarAsync();
                
                _logger.LogInformation("Firebird test query executed successfully, result: {Result}", result);
                return result != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firebird connection test failed: {Error}", ex.Message);
                return false;
            }
        }

        public DatabaseConfiguration GetDefaultConfiguration()
        {
            return new DatabaseConfiguration
            {
                DatabaseType = DatabaseType.SQLite,
                DatabasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WindowsLauncher",
                    "launcher.db"
                )
                // Для SQLite остальные поля остаются пустыми/нулевыми
            };
        }

        /// <summary>
        /// Получить конфигурацию Firebird по умолчанию
        /// </summary>
        public static DatabaseConfiguration GetDefaultFirebirdConfiguration()
        {
            return new DatabaseConfiguration
            {
                DatabaseType = DatabaseType.Firebird,
                ConnectionMode = FirebirdConnectionMode.Embedded,
                DatabasePath = "launcher.fdb",
                Server = "localhost",
                Port = 3050,
                Username = "SYSDBA",
                Password = "Ghtgjyf1",
                Dialect = 3,
                PageSize = 8192,
                Charset = "UTF8",
                ConnectionTimeout = 30
            };
        }

        public async Task<bool> IsConfiguredAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return false;

                var configuration = await GetConfigurationAsync();
                var validation = configuration.ValidateConfiguration();
                
                return validation.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if database is configured");
                return false;
            }
        }

        public async Task<bool> EnsureDatabaseExistsAsync(DatabaseConfiguration configuration)
        {
            try
            {
                switch (configuration.DatabaseType)
                {
                    case DatabaseType.SQLite:
                        return await EnsureSQLiteDatabaseExistsAsync(configuration);
                    
                    case DatabaseType.Firebird:
                        return await EnsureFirebirdDatabaseExistsAsync(configuration);
                    
                    default:
                        _logger.LogError("Unsupported database type: {DatabaseType}", configuration.DatabaseType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring database exists");
                return false;
            }
        }

        private async Task<bool> EnsureSQLiteDatabaseExistsAsync(DatabaseConfiguration configuration)
        {
            try
            {
                var dbPath = configuration.DatabasePath;
                var directory = Path.GetDirectoryName(dbPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // SQLite создаст файл автоматически при первом подключении
                using var connection = new SqliteConnection(configuration.GetSQLiteConnectionString());
                await connection.OpenAsync();
                
                _logger.LogInformation("SQLite database ensured at: {Path}", dbPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure SQLite database exists");
                return false;
            }
        }

        private async Task<bool> EnsureFirebirdDatabaseExistsAsync(DatabaseConfiguration configuration)
        {
            try
            {
                // Для Client/Server режима сначала проверяем доступность сервера
                if (configuration.ConnectionMode == FirebirdConnectionMode.ClientServer)
                {
                    _logger.LogInformation("Checking Firebird server availability at {Server}:{Port}", 
                        configuration.Server, configuration.Port);
                    
                    if (!await IsFirebirdServerAvailableAsync(configuration))
                    {
                        _logger.LogError("Firebird server is not available at {Server}:{Port}. Please install and start Firebird server.", 
                            configuration.Server, configuration.Port);
                        return false;
                    }
                }

                // Проверим, существует ли база
                var testResult = await TestFirebirdConnectionAsync(configuration);
                if (testResult)
                {
                    _logger.LogInformation("Firebird database already exists and is accessible");
                    return true;
                }

                _logger.LogInformation("Firebird database does not exist, creating new database...");

                // Создаем директорию для Embedded режима
                if (configuration.ConnectionMode == FirebirdConnectionMode.Embedded)
                {
                    var dbPath = configuration.DatabasePath;
                    var directory = Path.GetDirectoryName(dbPath);
                    
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        _logger.LogInformation("Created directory: {Directory}", directory);
                    }
                }

                // Создание базы данных
                string createConnectionString;
                if (configuration.ConnectionMode == FirebirdConnectionMode.Embedded)
                {
                    // Для Embedded используем путь к файлу
                    createConnectionString = configuration.GetFirebirdConnectionString();
                }
                else
                {
                    // Для Full Server создаем БД с полным путем на сервере
                    var serverDatabasePath = configuration.DatabasePath;
                    
                    // Если это алиас/имя БД, преобразуем в полный путь для создания
                    if (!serverDatabasePath.Contains("/") && !serverDatabasePath.Contains("\\") && !serverDatabasePath.EndsWith(".fdb"))
                    {
                        // Используем папку разрешенную в firebird.conf: DatabaseAccess = Restrict C:\DataBase
                        serverDatabasePath = $"C:\\DataBase\\{serverDatabasePath}.fdb";
                        _logger.LogInformation("Converting database alias '{Alias}' to full server path: {FullPath}", 
                            configuration.DatabasePath, serverDatabasePath);
                    }
                    else if (serverDatabasePath.Contains("\\") && !serverDatabasePath.StartsWith("C:\\DataBase", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Database path '{Path}' may not be accessible due to Firebird DatabaseAccess restriction to C:\\DataBase", 
                            serverDatabasePath);
                    }
                    
                    // Для создания БД используем legacy формат с полным путем
                    if (configuration.Port != 3050)
                    {
                        createConnectionString = $"database={configuration.Server}/{configuration.Port}:{serverDatabasePath};" +
                                               $"user={configuration.Username};password={configuration.Password};" +
                                               $"dialect={configuration.Dialect};charset={configuration.Charset};" +
                                               $"connection timeout={configuration.ConnectionTimeout}";
                    }
                    else
                    {
                        createConnectionString = $"database={configuration.Server}:{serverDatabasePath};" +
                                               $"user={configuration.Username};password={configuration.Password};" +
                                               $"dialect={configuration.Dialect};charset={configuration.Charset};" +
                                               $"connection timeout={configuration.ConnectionTimeout}";
                    }
                }
                
                _logger.LogInformation("Creating Firebird database with connection string: {ConnectionString}", 
                    createConnectionString.Replace(configuration.Password, "***"));
                
                FbConnection.CreateDatabase(createConnectionString, configuration.PageSize, true, true);
                
                _logger.LogInformation("Firebird database created successfully: {DatabasePath}", configuration.DatabasePath);
                
                // Проверяем что база действительно создалась
                var verifyResult = await TestFirebirdConnectionAsync(configuration);
                if (verifyResult)
                {
                    _logger.LogInformation("Firebird database creation verified successfully");
                    return true;
                }
                else
                {
                    _logger.LogError("Firebird database was created but verification test failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure Firebird database exists: {Error}", ex.Message);
                return false;
            }
        }

        private async Task<bool> IsFirebirdServerAvailableAsync(DatabaseConfiguration configuration)
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                await tcpClient.ConnectAsync(configuration.Server, configuration.Port);
                return tcpClient.Connected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<DatabaseInfo> GetDatabaseInfoAsync(DatabaseConfiguration configuration)
        {
            var info = new DatabaseInfo();
            
            try
            {
                switch (configuration.DatabaseType)
                {
                    case DatabaseType.SQLite:
                        return await GetSQLiteDatabaseInfoAsync(configuration);
                    
                    case DatabaseType.Firebird:
                        return await GetFirebirdDatabaseInfoAsync(configuration);
                    
                    default:
                        info.ErrorMessage = $"Unsupported database type: {configuration.DatabaseType}";
                        return info;
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error getting database info");
                return info;
            }
        }

        private async Task<DatabaseInfo> GetSQLiteDatabaseInfoAsync(DatabaseConfiguration configuration)
        {
            var info = new DatabaseInfo();
            
            try
            {
                var dbPath = configuration.DatabasePath;
                info.Exists = File.Exists(dbPath);
                
                if (info.Exists)
                {
                    var fileInfo = new FileInfo(dbPath);
                    info.Size = FormatFileSize(fileInfo.Length);
                    info.Created = fileInfo.CreationTime;
                    info.LastModified = fileInfo.LastWriteTime;
                }

                using var connection = new SqliteConnection(configuration.GetSQLiteConnectionString());
                await connection.OpenAsync();
                
                info.Version = connection.ServerVersion;
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
                var result = await command.ExecuteScalarAsync();
                info.TableCount = Convert.ToInt32(result);
                
                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                return info;
            }
        }

        private async Task<DatabaseInfo> GetFirebirdDatabaseInfoAsync(DatabaseConfiguration configuration)
        {
            var info = new DatabaseInfo();
            
            try
            {
                using var connection = new FbConnection(configuration.GetFirebirdConnectionString());
                await connection.OpenAsync();
                
                info.Exists = true;
                info.Version = connection.ServerVersion;
                
                // Получаем количество таблиц
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) 
                    FROM RDB$RELATIONS 
                    WHERE RDB$VIEW_BLR IS NULL 
                    AND (RDB$SYSTEM_FLAG IS NULL OR RDB$SYSTEM_FLAG = 0)";
                
                var result = await command.ExecuteScalarAsync();
                info.TableCount = Convert.ToInt32(result);
                
                // Для embedded режима можем получить размер файла
                if (configuration.ConnectionMode == FirebirdConnectionMode.Embedded && File.Exists(configuration.DatabasePath))
                {
                    var fileInfo = new FileInfo(configuration.DatabasePath);
                    info.Size = FormatFileSize(fileInfo.Length);
                    info.Created = fileInfo.CreationTime;
                    info.LastModified = fileInfo.LastWriteTime;
                }
                
                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                return info;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Удалить файл конфигурации базы данных
        /// </summary>
        public async Task DeleteConfigurationAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    File.Delete(_configPath);
                    _logger.LogInformation("Database configuration file deleted: {Path}", _configPath);
                }
                else
                {
                    _logger.LogInformation("Database configuration file does not exist: {Path}", _configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete database configuration file: {Path}", _configPath);
                throw;
            }
        }

        /// <summary>
        /// Проверить существование файла конфигурации
        /// </summary>
        public bool ConfigurationFileExists()
        {
            return File.Exists(_configPath);
        }

        /// <summary>
        /// Получить путь к файлу конфигурации
        /// </summary>
        public string GetConfigurationFilePath()
        {
            return _configPath;
        }

        /// <summary>
        /// Мигрировать незашифрованную конфигурацию в зашифрованный формат
        /// </summary>
        private async Task MigrateUnencryptedConfigurationAsync(DatabaseConfiguration configuration)
        {
            try
            {
                _logger.LogInformation("Starting migration of unencrypted database configuration");
                
                // Сохраняем исходный пароль
                var originalPassword = configuration.Password;
                
                // Создаем конфигурацию с зашифрованным паролем для сохранения
                var migratedConfig = new DatabaseConfiguration
                {
                    DatabaseType = configuration.DatabaseType,
                    ConnectionMode = configuration.ConnectionMode,
                    DatabasePath = configuration.DatabasePath,
                    Server = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Server : null,
                    Port = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Port : 0,
                    Username = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Username : null,
                    Password = configuration.DatabaseType == DatabaseType.Firebird ? _encryptionService.Encrypt(originalPassword) : null,
                    Dialect = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Dialect : 0,
                    PageSize = configuration.DatabaseType == DatabaseType.Firebird ? configuration.PageSize : 0,
                    Charset = configuration.DatabaseType == DatabaseType.Firebird ? configuration.Charset : null,
                    ConnectionTimeout = configuration.DatabaseType == DatabaseType.Firebird ? configuration.ConnectionTimeout : 0
                };

                var json = JsonSerializer.Serialize(migratedConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                await File.WriteAllTextAsync(_configPath, json);
                
                // Обновляем текущую конфигурацию с зашифрованным паролем
                configuration.Password = migratedConfig.Password;
                
                _logger.LogInformation("Successfully migrated database configuration to encrypted format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate unencrypted configuration");
                throw;
            }
        }
    }
}
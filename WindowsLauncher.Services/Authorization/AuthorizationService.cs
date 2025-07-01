// WindowsLauncher.Services/Authentication/AuthenticationConfigurationService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Services.Authentication
{
    /// <summary>
    /// Унифицированный сервис для управления конфигурацией аутентификации
    /// </summary>
    public class AuthenticationConfigurationService : IAuthenticationConfigurationService
    {
        private readonly ILogger<AuthenticationConfigurationService> _logger;
        private readonly string _configPath;
        private AuthenticationConfiguration _currentConfig;
        private readonly object _configLock = new();

        public AuthenticationConfigurationService(ILogger<AuthenticationConfigurationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Путь к файлу конфигурации в папке данных приложения
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "WindowsLauncher");
            Directory.CreateDirectory(appFolder);
            _configPath = Path.Combine(appFolder, "auth-config.json");

            LoadConfiguration();
        }

        /// <summary>
        /// Получение текущей конфигурации
        /// </summary>
        public AuthenticationConfiguration GetConfiguration()
        {
            lock (_configLock)
            {
                return _currentConfig ?? CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Сохранение конфигурации
        /// </summary>
        public async Task SaveConfigurationAsync(AuthenticationConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var validation = await ValidateConfigurationAsync(config);
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid configuration: {errors}");
            }

            try
            {
                lock (_configLock)
                {
                    _currentConfig = config;
                }

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_configPath, json);

                _logger.LogInformation("Authentication configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save authentication configuration");
                throw;
            }
        }

        /// <summary>
        /// Проверить, настроена ли система
        /// </summary>
        public bool IsConfigured()
        {
            var config = GetConfiguration();
            return config.IsConfigured &&
                   !string.IsNullOrEmpty(config.Domain) &&
                   !string.IsNullOrEmpty(config.LdapServer);
        }

        /// <summary>
        /// Сброс конфигурации к настройкам по умолчанию
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            try
            {
                var defaultConfig = CreateDefaultConfiguration();
                await SaveConfigurationAsync(defaultConfig);

                _logger.LogInformation("Configuration reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting configuration to defaults");
                throw;
            }
        }

        /// <summary>
        /// Валидировать конфигурацию
        /// </summary>
        public async Task<ValidationResult> ValidateConfigurationAsync(AuthenticationConfiguration configuration)
        {
            var result = new ValidationResult();

            try
            {
                // Валидация домена
                if (string.IsNullOrWhiteSpace(configuration.Domain))
                {
                    result.Errors.Add("Доменное имя не может быть пустым");
                }

                // Валидация LDAP сервера
                if (string.IsNullOrWhiteSpace(configuration.LdapServer))
                {
                    result.Errors.Add("LDAP сервер не может быть пустым");
                }

                // Валидация порта
                if (configuration.Port <= 0 || configuration.Port > 65535)
                {
                    result.Errors.Add("Порт должен быть в диапазоне 1-65535");
                }

                // Валидация сервисного администратора
                if (string.IsNullOrWhiteSpace(configuration.ServiceAdmin.Username))
                {
                    result.Errors.Add("Имя пользователя сервисного администратора не может быть пустым");
                }

                if (configuration.ServiceAdmin.SessionTimeoutMinutes <= 0 ||
                    configuration.ServiceAdmin.SessionTimeoutMinutes > 1440)
                {
                    result.Errors.Add("Время сессии должно быть в диапазоне 1-1440 минут");
                }

                // Предупреждения
                if (configuration.Port == 389 && configuration.UseTLS)
                {
                    result.Warnings.Add("Порт 389 обычно используется без TLS. Рассмотрите использование порта 636 для LDAPS");
                }

                if (configuration.CacheLifetimeMinutes < 5)
                {
                    result.Warnings.Add("Короткое время жизни кэша может повлиять на производительность");
                }

                result.IsValid = result.Errors.Count == 0;

                _logger.LogDebug("Configuration validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                    result.IsValid, result.Errors.Count, result.Warnings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration");
                result.Errors.Add($"Ошибка валидации: {ex.Message}");
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Экспортировать конфигурацию
        /// </summary>
        public async Task<string> ExportConfigurationAsync()
        {
            try
            {
                var config = GetConfiguration();

                // Создаем копию без чувствительных данных
                var exportConfig = new
                {
                    config.Domain,
                    config.LdapServer,
                    config.Port,
                    config.UseTLS,
                    config.ConnectionTimeoutSeconds,
                    config.BaseDN,
                    config.DefaultUserGroups,
                    config.AdminGroups,
                    config.PowerUserGroups,
                    config.EnableCaching,
                    config.CacheLifetimeMinutes,
                    config.EnableFallbackMode,
                    ServiceAdmin = new
                    {
                        config.ServiceAdmin.Username,
                        config.ServiceAdmin.SessionTimeoutMinutes,
                        config.ServiceAdmin.MaxLoginAttempts,
                        config.ServiceAdmin.LockoutDurationMinutes,
                        config.ServiceAdmin.RequirePasswordChange
                    },
                    ExportedAt = DateTime.UtcNow,
                    Version = "1.0"
                };

                var json = JsonSerializer.Serialize(exportConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation("Configuration exported successfully");
                return await Task.FromResult(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting configuration");
                throw;
            }
        }

        /// <summary>
        /// Импортировать конфигурацию
        /// </summary>
        public async Task ImportConfigurationAsync(string configurationJson)
        {
            try
            {
                var importedData = JsonSerializer.Deserialize<JsonElement>(configurationJson);

                var config = GetConfiguration(); // Начинаем с текущей конфигурации

                // Импортируем только безопасные настройки
                if (importedData.TryGetProperty("domain", out var domain))
                    config.Domain = domain.GetString();

                if (importedData.TryGetProperty("ldapServer", out var ldapServer))
                    config.LdapServer = ldapServer.GetString();

                if (importedData.TryGetProperty("port", out var port))
                    config.Port = port.GetInt32();

                if (importedData.TryGetProperty("useTLS", out var useTLS))
                    config.UseTLS = useTLS.GetBoolean();

                if (importedData.TryGetProperty("connectionTimeoutSeconds", out var timeout))
                    config.ConnectionTimeoutSeconds = timeout.GetInt32();

                if (importedData.TryGetProperty("baseDN", out var baseDN))
                    config.BaseDN = baseDN.GetString() ?? string.Empty;

                if (importedData.TryGetProperty("defaultUserGroups", out var userGroups))
                    config.DefaultUserGroups = userGroups.GetString() ?? string.Empty;

                if (importedData.TryGetProperty("adminGroups", out var adminGroups))
                    config.AdminGroups = adminGroups.GetString() ?? string.Empty;

                if (importedData.TryGetProperty("powerUserGroups", out var powerUserGroups))
                    config.PowerUserGroups = powerUserGroups.GetString() ?? string.Empty;

                // Валидируем импортированную конфигурацию
                var validationResult = await ValidateConfigurationAsync(config);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Импортированная конфигурация некорректна: {string.Join(", ", validationResult.Errors)}");
                }

                await SaveConfigurationAsync(config);

                _logger.LogInformation("Configuration imported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing configuration");
                throw;
            }
        }

        /// <summary>
        /// Загрузить конфигурацию из файла
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _currentConfig = JsonSerializer.Deserialize<AuthenticationConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    _logger.LogDebug("Configuration loaded from file");
                }
                else
                {
                    _currentConfig = CreateDefaultConfiguration();
                    _logger.LogDebug("Using default configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading configuration, using defaults");
                _currentConfig = CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Получить конфигурацию по умолчанию
        /// </summary>
        private AuthenticationConfiguration CreateDefaultConfiguration()
        {
            return new AuthenticationConfiguration
            {
                Domain = "company.local",
                LdapServer = "dc.company.local",
                Port = 389,
                UseTLS = true,
                ConnectionTimeoutSeconds = 30,
                BaseDN = string.Empty,
                DefaultUserGroups = "LauncherUsers",
                AdminGroups = "LauncherAdmins",
                PowerUserGroups = "LauncherPowerUsers",
                EnableCaching = true,
                CacheLifetimeMinutes = 60,
                EnableFallbackMode = true,
                ServiceAdmin = new ServiceAdminConfiguration
                {
                    Username = "serviceadmin",
                    SessionTimeoutMinutes = 60,
                    MaxLoginAttempts = 5,
                    LockoutDurationMinutes = 15,
                    RequirePasswordChange = true
                },
                IsConfigured = false,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Результат валидации конфигурации
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
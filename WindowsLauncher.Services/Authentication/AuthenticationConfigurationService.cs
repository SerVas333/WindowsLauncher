using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using System.Linq;
using System.Collections.Generic;
using ValidationResult = WindowsLauncher.Core.Interfaces.ValidationResult;

namespace WindowsLauncher.Services.Authentication
{
    /// <summary>
    /// Исправленный сервис для управления конфигурацией аутентификации
    /// </summary>
    public class AuthenticationConfigurationService : IAuthenticationConfigurationService
    {
        private readonly ILogger<AuthenticationConfigurationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _configPath;
        private AuthenticationConfiguration? _currentConfig;
        private readonly object _configLock = new();

        public AuthenticationConfigurationService(
            ILogger<AuthenticationConfigurationService> logger,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Путь к файлу конфигурации в папке данных приложения
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "WindowsLauncher");
            Directory.CreateDirectory(appFolder);
            _configPath = Path.Combine(appFolder, "auth-config.json");

            LoadConfiguration();
        }

        /// <summary>
        /// Получение текущей конфигурации AD
        /// </summary>
        public AuthenticationConfiguration GetConfiguration()
        {
            lock (_configLock)
            {
                return _currentConfig ?? CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Сохранение конфигурации AD
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
                    _currentConfig.LastModified = DateTime.UtcNow;
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
            try
            {
                var config = GetConfiguration();

                // Проверяем базовые настройки
                var hasBasicConfig = !string.IsNullOrEmpty(config.Domain) &&
                                   !string.IsNullOrEmpty(config.LdapServer) &&
                                   config.Port > 0;

                // Проверяем настройки сервисного администратора
                var hasServiceAdmin = !string.IsNullOrEmpty(config.ServiceAdmin?.Username) &&
                                    config.ServiceAdmin.IsPasswordSet;

                _logger.LogDebug("Configuration check: Basic={Basic}, ServiceAdmin={ServiceAdmin}, IsConfigured={IsConfigured}",
                    hasBasicConfig, hasServiceAdmin, config.IsConfigured);

                // Система считается настроенной если есть базовая конфигурация ИЛИ сервисный администратор
                return config.IsConfigured || hasBasicConfig || hasServiceAdmin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if system is configured");
                return false;
            }
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

                _logger.LogInformation("Authentication configuration reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset authentication configuration");
                throw;
            }
        }

        /// <summary>
        /// Валидировать конфигурацию
        /// </summary>
        public async Task<ValidationResult> ValidateConfigurationAsync(AuthenticationConfiguration config)
        {
            return await Task.FromResult(ValidateConfiguration(config));
        }

        /// <summary>
        /// Экспорт конфигурации (без паролей для безопасности)
        /// </summary>
        public async Task<string> ExportConfigurationAsync()
        {
            return await Task.FromResult(ExportConfiguration());
        }

        /// <summary>
        /// Импорт конфигурации
        /// </summary>
        public async Task ImportConfigurationAsync(string configurationJson)
        {
            var success = await ImportConfigurationInternalAsync(configurationJson);
            if (!success)
            {
                throw new ArgumentException("Failed to import configuration");
            }
        }

        #region Private Methods

        /// <summary>
        /// Загрузка конфигурации из файла или appsettings.json
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // Сначала пытаемся загрузить из пользовательского файла
                if (File.Exists(_configPath))
                {
                    _logger.LogInformation("Loading configuration from user file: {ConfigPath}", _configPath);
                    var json = File.ReadAllText(_configPath);
                    _currentConfig = JsonSerializer.Deserialize<AuthenticationConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    });

                    if (_currentConfig != null)
                    {
                        _logger.LogInformation("User configuration loaded successfully");
                        return;
                    }
                }

                // Если пользовательского файла нет, загружаем из appsettings.json
                _logger.LogInformation("Loading configuration from appsettings.json");
                _currentConfig = LoadFromAppSettings();

                if (_currentConfig == null)
                {
                    _logger.LogWarning("Failed to load configuration from appsettings.json, using defaults");
                    _currentConfig = CreateDefaultConfiguration();
                }
                else
                {
                    _logger.LogInformation("Configuration loaded from appsettings.json successfully: LdapServer={LdapServer}, Domain={Domain}", 
                        _currentConfig.LdapServer, _currentConfig.Domain);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load authentication configuration, using defaults");
                _currentConfig = CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Загрузка конфигурации из appsettings.json
        /// </summary>
        private AuthenticationConfiguration? LoadFromAppSettings()
        {
            try
            {
                var adSection = _configuration.GetSection("ActiveDirectory");
                if (!adSection.Exists())
                {
                    _logger.LogWarning("ActiveDirectory section not found in configuration");
                    return null;
                }

                _logger.LogInformation("ActiveDirectory section found, binding configuration...");

                var config = new AuthenticationConfiguration();
                adSection.Bind(config);

                _logger.LogInformation("After binding: Domain={Domain}, LdapServer={LdapServer}, Port={Port}, TestMode={TestMode}",
                    config.Domain, config.LdapServer, config.Port, config.TestMode);

                // Дополнительная обработка списков
                var trustedDomains = adSection.GetSection("TrustedDomains").Get<List<string>>();
                if (trustedDomains != null)
                {
                    config.TrustedDomains = trustedDomains;
                }

                // Обработка ServiceAdmin секции
                var serviceAdminSection = adSection.GetSection("ServiceAdmin");
                if (serviceAdminSection.Exists())
                {
                    config.ServiceAdmin = new ServiceAdminConfiguration();
                    serviceAdminSection.Bind(config.ServiceAdmin);
                }

                _logger.LogInformation("Final loaded configuration: Domain={Domain}, LdapServer={LdapServer}, Port={Port}, TestMode={TestMode}",
                    config.Domain, config.LdapServer, config.Port, config.TestMode);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration from appsettings.json");
                return null;
            }
        }

        /// <summary>
        /// Создание конфигурации по умолчанию
        /// </summary>
        private AuthenticationConfiguration CreateDefaultConfiguration()
        {
            // Пытаемся определить домен автоматически
            var defaultDomain = GetDefaultDomain();
            var defaultLdapServer = string.IsNullOrEmpty(defaultDomain) ? "dc.company.local" : $"dc.{defaultDomain}";

            _logger.LogWarning("Creating default configuration: Domain={Domain}, LdapServer={LdapServer}", 
                defaultDomain, defaultLdapServer);

            var config = new AuthenticationConfiguration
            {
                Domain = defaultDomain,
                LdapServer = defaultLdapServer,
                Port = 389,
                UseTLS = true,
                TimeoutSeconds = 30,
                RequireDomainMembership = false,
                AdminGroups = "LauncherAdmins,Domain Admins",
                PowerUserGroups = "LauncherPowerUsers",
                EnableFallbackMode = true,
                TrustedDomains = new List<string> { defaultDomain ?? "company.local" },
                ServiceAdmin = new ServiceAdminConfiguration
                {
                    Username = "serviceadmin",
                    IsEnabled = true,
                    SessionTimeoutMinutes = 60,
                    MaxLoginAttempts = 5,
                    LockoutDurationMinutes = 15,
                    // ✅ ПАРОЛЬ НЕ УСТАНОВЛЕН - будет создан при первом запуске
                    PasswordHash = "",
                    Salt = "",
                    IsPasswordSet = false,
                    RequirePasswordChange = false,
                    CreatedAt = DateTime.UtcNow,
                    LastPasswordChange = null
                },
                IsConfigured = false,
                LastModified = DateTime.UtcNow
            };

            _logger.LogInformation("Created default configuration with domain: {Domain}", config.Domain);
            return config;
        }

        /// <summary>
        /// Генерация хеша пароля для дефолтной конфигурации
        /// </summary>
        private static string GetDefaultPasswordHash()
        {
            // Фиксированная соль для воспроизводимости
            var saltBytes = new byte[32]; // все нули
            var salt = Convert.ToBase64String(saltBytes);

            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes("vfc11Nth!", saltBytes, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Определение домена по умолчанию
        /// </summary>
        private string GetDefaultDomain()
        {
            try
            {
                // Пытаемся получить домен из переменных окружения
                var userDomain = Environment.UserDomainName;
                var computerName = Environment.MachineName;

                // Если домен не равен имени компьютера, то компьютер в домене
                if (!string.Equals(userDomain, computerName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Detected domain from environment: {Domain}", userDomain);
                    return userDomain.ToLower();
                }

                // Пытаемся получить DNS домен
                var dnsDomain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
                if (!string.IsNullOrEmpty(dnsDomain))
                {
                    _logger.LogDebug("Detected domain from DNS: {Domain}", dnsDomain);
                    return dnsDomain.ToLower();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to determine default domain");
            }

            _logger.LogDebug("Using fallback domain: company.local");
            return "company.local"; // Fallback значение
        }

        /// <summary>
        /// Валидация конфигурации
        /// </summary>
        private ValidationResult ValidateConfiguration(AuthenticationConfiguration config)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (config == null)
            {
                errors.Add("Configuration is null");
                return new ValidationResult { IsValid = false, Errors = errors.ToArray() };
            }

            // Валидация основных настроек домена (опциональна для fallback режима)
            if (!string.IsNullOrWhiteSpace(config.Domain) && !IsValidDomainName(config.Domain))
                warnings.Add("Domain name format may be invalid");

            // Валидация LDAP сервера (опциональна)
            if (!string.IsNullOrWhiteSpace(config.LdapServer) && !IsValidServerName(config.LdapServer))
                warnings.Add("LDAP server name format may be invalid");

            // Валидация порта
            if (config.Port <= 0 || config.Port > 65535)
                warnings.Add("Port should be between 1 and 65535");

            // Валидация таймаута
            if (config.TimeoutSeconds <= 0 || config.TimeoutSeconds > 300)
                warnings.Add("Timeout should be between 1 and 300 seconds");

            // Валидация сервисного администратора (опциональна)
            if (config.ServiceAdmin != null)
            {
                if (string.IsNullOrWhiteSpace(config.ServiceAdmin.Username))
                    warnings.Add("Service admin username is empty");

                if (config.ServiceAdmin.SessionTimeoutMinutes <= 0)
                    warnings.Add("Service admin session timeout should be greater than 0");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray(),
                Warnings = warnings.ToArray()
            };
        }

        /// <summary>
        /// Экспорт конфигурации
        /// </summary>
        private string ExportConfiguration()
        {
            try
            {
                var config = GetConfiguration();

                // Создаем копию конфигурации без чувствительных данных
                var exportConfig = new AuthenticationConfiguration
                {
                    Domain = config.Domain,
                    LdapServer = config.LdapServer,
                    Port = config.Port,
                    UseTLS = config.UseTLS,
                    ServiceUser = config.ServiceUser,
                    ServicePassword = string.IsNullOrEmpty(config.ServicePassword) ? string.Empty : "***HIDDEN***",
                    TimeoutSeconds = config.TimeoutSeconds,
                    RequireDomainMembership = config.RequireDomainMembership,
                    AdminGroups = config.AdminGroups,
                    PowerUserGroups = config.PowerUserGroups,
                    EnableFallbackMode = config.EnableFallbackMode,
                    TrustedDomains = config.TrustedDomains?.ToList() ?? new List<string>(),
                    ServiceAdmin = new ServiceAdminConfiguration
                    {
                        Username = config.ServiceAdmin?.Username ?? string.Empty,
                        IsEnabled = config.ServiceAdmin?.IsEnabled ?? false,
                        SessionTimeoutMinutes = config.ServiceAdmin?.SessionTimeoutMinutes ?? 60,
                        PasswordHash = string.IsNullOrEmpty(config.ServiceAdmin?.PasswordHash) ? string.Empty : "***HIDDEN***",
                        Salt = string.IsNullOrEmpty(config.ServiceAdmin?.Salt) ? string.Empty : "***HIDDEN***"
                    },
                    IsConfigured = config.IsConfigured,
                    LastModified = config.LastModified
                };

                return JsonSerializer.Serialize(exportConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export authentication configuration");
                throw;
            }
        }

        /// <summary>
        /// Импорт конфигурации
        /// </summary>
        private async Task<bool> ImportConfigurationInternalAsync(string configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
                return false;

            try
            {
                var importedConfig = JsonSerializer.Deserialize<AuthenticationConfiguration>(configJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (importedConfig == null)
                    return false;

                var validation = await ValidateConfigurationAsync(importedConfig);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Imported configuration is invalid: {Errors}", string.Join(", ", validation.Errors));
                    return false;
                }

                await SaveConfigurationAsync(importedConfig);

                _logger.LogInformation("Authentication configuration imported successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import authentication configuration");
                return false;
            }
        }

        /// <summary>
        /// Валидация имени домена
        /// </summary>
        private static bool IsValidDomainName(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // Базовая проверка формата домена
            var parts = domain.Split('.');
            if (parts.Length < 2)
                return false;

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) || part.Length > 63)
                    return false;

                // Проверяем, что содержит только допустимые символы
                if (!System.Text.RegularExpressions.Regex.IsMatch(part, @"^[a-zA-Z0-9-]+$"))
                    return false;

                // Не должно начинаться или заканчиваться дефисом
                if (part.StartsWith("-") || part.EndsWith("-"))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Валидация имени сервера
        /// </summary>
        private static bool IsValidServerName(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                return false;

            // Может быть IP адресом
            if (System.Net.IPAddress.TryParse(serverName, out _))
                return true;

            // Или доменным именем
            return IsValidDomainName(serverName);
        }


        #endregion
    }
}
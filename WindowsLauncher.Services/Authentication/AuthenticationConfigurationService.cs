using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using System.Linq;
using System.Collections.Generic;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Сервис для управления конфигурацией аутентификации
    /// </summary>
    public class AuthenticationConfigurationService : IAuthenticationConfigurationService
    {
        private readonly ILogger<AuthenticationConfigurationService> _logger;
        private readonly string _configPath;
        private ActiveDirectoryConfiguration _currentConfig;
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
        /// Получение текущей конфигурации AD
        /// </summary>
        public ActiveDirectoryConfiguration GetConfiguration()
        {
            lock (_configLock)
            {
                return _currentConfig ?? CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Сохранение конфигурации AD
        /// </summary>
        public async Task SaveConfigurationAsync(ActiveDirectoryConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var validation = ValidateConfiguration(config);
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
        /// Сброс конфигурации к настройкам по умолчанию
        /// </summary>
        public async Task ResetConfigurationAsync()
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
        /// Экспорт конфигурации (без паролей для безопасности)
        /// </summary>
        public string ExportConfiguration()
        {
            try
            {
                var config = GetConfiguration();

                // Создаем копию конфигурации без чувствительных данных
                var exportConfig = new ActiveDirectoryConfiguration
                {
                    Domain = config.Domain,
                    LdapServer = config.LdapServer,
                    Port = config.Port,
                    UseTLS = config.UseTLS,
                    ServiceUser = config.ServiceUser,
                    // Пароли не экспортируем для безопасности
                    ServicePassword = string.IsNullOrEmpty(config.ServicePassword) ? null : "***HIDDEN***",
                    TimeoutSeconds = config.TimeoutSeconds,
                    RequireDomainMembership = config.RequireDomainMembership,
                    TrustedDomains = config.TrustedDomains?.ToList() ?? new List<string>(),
                    ServiceAdmin = new ServiceAdminConfiguration
                    {
                        Username = config.ServiceAdmin.Username,
                        IsEnabled = config.ServiceAdmin.IsEnabled,
                        SessionTimeoutMinutes = config.ServiceAdmin.SessionTimeoutMinutes,
                        // Пароли не экспортируем
                        PasswordHash = string.IsNullOrEmpty(config.ServiceAdmin.PasswordHash) ? null : "***HIDDEN***",
                        Salt = string.IsNullOrEmpty(config.ServiceAdmin.Salt) ? null : "***HIDDEN***"
                    }
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
        public async Task<bool> ImportConfigurationAsync(string configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
                return false;

            try
            {
                var importedConfig = JsonSerializer.Deserialize<ActiveDirectoryConfiguration>(configJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (importedConfig == null)
                    return false;

                // Сохраняем существующие пароли, если в импортируемой конфигурации они скрыты
                var currentConfig = GetConfiguration();

                if (importedConfig.ServicePassword == "***HIDDEN***")
                    importedConfig.ServicePassword = currentConfig.ServicePassword;

                if (importedConfig.ServiceAdmin.PasswordHash == "***HIDDEN***")
                {
                    importedConfig.ServiceAdmin.PasswordHash = currentConfig.ServiceAdmin.PasswordHash;
                    importedConfig.ServiceAdmin.Salt = currentConfig.ServiceAdmin.Salt;
                }

                var validation = ValidateConfiguration(importedConfig);
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
        /// Валидация конфигурации
        /// </summary>
        public ValidationResult ValidateConfiguration(ActiveDirectoryConfiguration config)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (config == null)
            {
                errors.Add("Configuration is null");
                return new ValidationResult { IsValid = false, Errors = errors.ToArray() };
            }

            // Валидация основных настроек домена
            if (string.IsNullOrWhiteSpace(config.Domain))
                errors.Add("Domain is required");
            else if (!IsValidDomainName(config.Domain))
                errors.Add("Domain name format is invalid");

            // Валидация LDAP сервера
            if (!string.IsNullOrWhiteSpace(config.LdapServer) && !IsValidServerName(config.LdapServer))
                warnings.Add("LDAP server name format may be invalid");

            // Валидация порта
            if (config.Port <= 0 || config.Port > 65535)
                errors.Add("Port must be between 1 and 65535");

            // Валидация таймаута
            if (config.TimeoutSeconds <= 0 || config.TimeoutSeconds > 300)
                warnings.Add("Timeout should be between 1 and 300 seconds");

            // Валидация сервисного аккаунта
            if (!string.IsNullOrEmpty(config.ServiceUser) && string.IsNullOrEmpty(config.ServicePassword))
                warnings.Add("Service user is specified but password is empty");

            // Валидация доверенных доменов
            if (config.TrustedDomains?.Any() == true)
            {
                foreach (var domain in config.TrustedDomains)
                {
                    if (!IsValidDomainName(domain))
                        warnings.Add($"Trusted domain '{domain}' has invalid format");
                }
            }

            // Валидация настроек сервисного администратора
            if (config.ServiceAdmin != null)
            {
                if (string.IsNullOrWhiteSpace(config.ServiceAdmin.Username))
                    errors.Add("Service admin username is required");

                if (config.ServiceAdmin.SessionTimeoutMinutes <= 0 || config.ServiceAdmin.SessionTimeoutMinutes > 1440)
                    warnings.Add("Service admin session timeout should be between 1 and 1440 minutes");

                if (config.ServiceAdmin.IsEnabled && !config.ServiceAdmin.IsPasswordSet)
                    warnings.Add("Service admin is enabled but password is not set");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray(),
                Warnings = warnings.ToArray()
            };
        }

        #region Private Methods

        /// <summary>
        /// Загрузка конфигурации из файла
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation("Configuration file not found, creating default configuration");
                    _currentConfig = CreateDefaultConfiguration();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                _currentConfig = JsonSerializer.Deserialize<ActiveDirectoryConfiguration>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (_currentConfig == null)
                {
                    _logger.LogWarning("Failed to deserialize configuration, using defaults");
                    _currentConfig = CreateDefaultConfiguration();
                    return;
                }

                _logger.LogInformation("Authentication configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load authentication configuration, using defaults");
                _currentConfig = CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Создание конфигурации по умолчанию
        /// </summary>
        private ActiveDirectoryConfiguration CreateDefaultConfiguration()
        {
            // Пытаемся определить домен автоматически
            var defaultDomain = GetDefaultDomain();

            return new ActiveDirectoryConfiguration
            {
                Domain = defaultDomain,
                LdapServer = string.IsNullOrEmpty(defaultDomain) ? "dc.company.local" : $"dc.{defaultDomain}",
                Port = 389,
                UseTLS = true,
                TimeoutSeconds = 30,
                RequireDomainMembership = false,
                TrustedDomains = new List<string>(),
                ServiceAdmin = new ServiceAdminConfiguration
                {
                    Username = "serviceadmin",
                    IsEnabled = true,
                    SessionTimeoutMinutes = 60
                }
            };
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
                    return userDomain.ToLower();
                }

                // Пытаемся получить DNS домен
                var dnsDomain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
                if (!string.IsNullOrEmpty(dnsDomain))
                {
                    return dnsDomain.ToLower();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to determine default domain: {Error}", ex.Message);
            }

            return "company.local"; // Fallback значение
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
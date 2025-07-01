using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using System.Linq;
using System.Collections.Generic;

namespace WindowsLauncher.Services.Authentication
{
    /// <summary>
    /// Сервис для управления конфигурацией аутентификации
    /// </summary>
    public class AuthenticationConfigurationService : IAuthenticationConfigurationService
    {
        private readonly ILogger<AuthenticationConfigurationService> _logger;
        private readonly string _configPath;
        private AuthenticationConfiguration? _currentConfig;
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
            return !string.IsNullOrEmpty(config.Domain) &&
                   !string.IsNullOrEmpty(config.LdapServer) &&
                   config.ServiceAdmin.IsPasswordSet;
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
                _currentConfig = JsonSerializer.Deserialize<AuthenticationConfiguration>(json, new JsonSerializerOptions
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
        private AuthenticationConfiguration CreateDefaultConfiguration()
        {
            // Пытаемся определить домен автоматически
            var defaultDomain = GetDefaultDomain();

            return new AuthenticationConfiguration
            {
                Domain = defaultDomain,
                LdapServer = string.IsNullOrEmpty(defaultDomain) ? "dc.company.local" : $"dc.{defaultDomain}",
                Port = 389,
                UseTLS = true,
                TimeoutSeconds = 30,
                RequireDomainMembership = false,
                AdminGroups = "LauncherAdmins",
                PowerUserGroups = "LauncherPowerUsers",
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
                    TrustedDomains = config.TrustedDomains?.ToList() ?? new List<string>(),
                    ServiceAdmin = new ServiceAdminConfiguration
                    {
                        Username = config.ServiceAdmin.Username,
                        IsEnabled = config.ServiceAdmin.IsEnabled,
                        SessionTimeoutMinutes = config.ServiceAdmin.SessionTimeoutMinutes,
                        PasswordHash = string.IsNullOrEmpty(config.ServiceAdmin.PasswordHash) ? string.Empty : "***HIDDEN***",
                        Salt = string.IsNullOrEmpty(config.ServiceAdmin.Salt) ? string.Empty : "***HIDDEN***"
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
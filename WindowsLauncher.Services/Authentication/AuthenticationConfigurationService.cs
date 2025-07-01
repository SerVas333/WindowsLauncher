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
        public async Task Re
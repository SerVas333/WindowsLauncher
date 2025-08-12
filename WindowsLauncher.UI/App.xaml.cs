// ===== WindowsLauncher.UI/App.xaml.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ БЕЗ ОШИБОК =====
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FirebirdSql.EntityFrameworkCore.Firebird.Extensions;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Repositories;
using WindowsLauncher.Services.ActiveDirectory;
using WindowsLauncher.Services.Applications;
using WindowsLauncher.Services.Audit;
using WindowsLauncher.Services.Authorization;
using WindowsLauncher.Services.Authentication;
using WindowsLauncher.Services;
using WindowsLauncher.Services.Android;
using WindowsLauncher.Core.Interfaces.Android;
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.Core.Services;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Views;
using WindowsLauncher.Core.Configuration;

// Новая архитектура ApplicationLifecycleService
using WindowsLauncher.Services.Lifecycle;
using WindowsLauncher.Services.Lifecycle.Windows;
using WindowsLauncher.Services.Lifecycle.Monitoring;
using WindowsLauncher.Services.Lifecycle.Management;
using WindowsLauncher.Services.Lifecycle.Launchers;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI
{
    public partial class App : WpfApplication
    {
        private IHost? _host;
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // ✅ ИНИЦИАЛИЗАЦИЯ ЛОКАЛИЗАЦИИ В ПЕРВУЮ ОЧЕРЕДЬ
                InitializeLocalization();

                // Настройка Host с конфигурацией и DI
                _host = CreateHostBuilder(e.Args).Build();
                ServiceProvider = _host.Services;

                // Инициализация языка через новый сервис
                await InitializeLanguageServiceAsync();

                // Инициализация базы данных
                await InitializeDatabaseAsync();

                // Настройка глобальной обработки исключений
                SetupExceptionHandling();

                // Инициализация глобального менеджера сенсорной клавиатуры
                InitializeGlobalTouchKeyboardManager();

                // ✅ ЗАПУСК ЧЕРЕЗ LoginWindow ВМЕСТО ПРЯМОГО ПОКАЗА MainWindow
                await StartApplicationAsync();

                // Запускаем мониторинг системных событий сессии
                try
                {
                    var sessionEventService = ServiceProvider.GetService<WindowsLauncher.Services.SessionEventService>();
                    sessionEventService?.StartMonitoring();
                }
                catch (Exception ex)
                {
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogWarning(ex, "Failed to start session event monitoring");
                }

                await _host.StartAsync();
            }
            catch (Exception ex)
            {
                ShowStartupError(ex);
                Shutdown(1);
            }

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogInformation("Application shutdown started");

                // Останавливаем мониторинг системных событий сессии
                try
                {
                    var sessionEventService = ServiceProvider?.GetService<WindowsLauncher.Services.SessionEventService>();
                    sessionEventService?.StopMonitoring();
                    
                    if (sessionEventService is IDisposable disposableSessionService)
                    {
                        disposableSessionService.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Error stopping session event monitoring");
                }

                // Останавливаем ApplicationLifecycleService
                try
                {
                    var lifecycleService = ServiceProvider?.GetService<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService>();
                    if (lifecycleService != null)
                    {
                        logger?.LogInformation("Closing all running applications before exit");
                        
                        // Комбинированное закрытие: graceful + force
                        var shutdownResult = await lifecycleService.ShutdownAllAsync(gracefulTimeoutMs: 3000, finalTimeoutMs: 1000);
                        
                        logger?.LogInformation("Application shutdown completed: {Success}, {Total} apps, {Graceful} graceful, {Forced} forced, {Failed} failed", 
                            shutdownResult.Success, 
                            shutdownResult.TotalApplications,
                            shutdownResult.Applications.Count(a => a.Success && a.Method == WindowsLauncher.Core.Models.Lifecycle.ShutdownMethod.Graceful),
                            shutdownResult.Applications.Count(a => a.Success && a.Method == WindowsLauncher.Core.Models.Lifecycle.ShutdownMethod.Forced),
                            shutdownResult.Applications.Count(a => !a.Success));
                        
                        // Останавливаем мониторинг и очищаем ресурсы
                        await lifecycleService.StopMonitoringAsync();
                        await lifecycleService.CleanupAsync();
                        
                        if (lifecycleService is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        
                        logger?.LogInformation("ApplicationLifecycleService stopped and disposed");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Error stopping ApplicationLifecycleService");
                }

                // Очищаем глобальный менеджер сенсорной клавиатуры
                try
                {
                    var globalManager = ServiceProvider?.GetService<WindowsLauncher.UI.Services.GlobalTouchKeyboardManager>();
                    globalManager?.Dispose();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Ошибка при очистке GlobalTouchKeyboardManager");
                }

                // Сохраняем настройки локализации
                LocalizationHelper.Instance.SaveLanguageSettings();

                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }

                logger?.LogInformation("Application shutdown completed");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error during application shutdown");
            }

            base.OnExit(e);
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, context.Configuration);
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                });

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Конфигурация
            services.Configure<ActiveDirectoryConfiguration>(
                configuration.GetSection("ActiveDirectory"));
            services.Configure<LocalUserConfiguration>(
                configuration.GetSection("LocalUsers"));
            services.Configure<ChromeWindowSearchOptions>(
                configuration.GetSection("ChromeWindowSearch"));

            // База данных
            services.AddSingleton<IEncryptionService, WindowsLauncher.Services.Security.EncryptionService>();
            services.AddSingleton<IDatabaseConfigurationService, DatabaseConfigurationService>();
            services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
            services.AddDbContext<LauncherDbContext>((serviceProvider, options) =>
            {
                try
                {
                    // Получаем сервис конфигурации БД
                    var dbConfigService = serviceProvider.GetRequiredService<IDatabaseConfigurationService>();
                    
                    // Проверяем существует ли файл конфигурации
                    DatabaseConfiguration dbConfig;
                    if (dbConfigService.ConfigurationFileExists())
                    {
                        // Загружаем синхронно для избежания deadlock в DI
                        var configPath = dbConfigService.GetConfigurationFilePath();
                        var json = File.ReadAllText(configPath);
                        dbConfig = System.Text.Json.JsonSerializer.Deserialize<DatabaseConfiguration>(json) 
                                   ?? dbConfigService.GetDefaultConfiguration();
                        
                        // Простая расшифровка пароля если нужно
                        var encryptionService = serviceProvider.GetRequiredService<IEncryptionService>();
                        if (encryptionService.IsEncrypted(dbConfig.Password))
                        {
                            dbConfig.Password = encryptionService.Decrypt(dbConfig.Password);
                        }
                    }
                    else
                    {
                        // Используем дефолтную конфигурацию
                        dbConfig = dbConfigService.GetDefaultConfiguration();
                    }

                    // Настраиваем провайдер в зависимости от типа БД
                    switch (dbConfig.DatabaseType)
                    {
                        case Core.Models.DatabaseType.SQLite:
                            options.UseSqlite(dbConfig.GetSQLiteConnectionString(), sqliteOptions =>
                            {
                                sqliteOptions.CommandTimeout(dbConfig.ConnectionTimeout);
                            });
                            break;

                        case Core.Models.DatabaseType.Firebird:
                            options.UseFirebird(dbConfig.GetFirebirdConnectionString(), firebirdOptions =>
                            {
                                firebirdOptions.CommandTimeout(dbConfig.ConnectionTimeout);
                            });
                            break;

                        default:
                            // Fallback к SQLite
                            var fallbackConnectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? GetDefaultConnectionString();
                            options.UseSqlite(fallbackConnectionString);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // В случае ошибки загрузки конфигурации используем SQLite fallback
                    System.Diagnostics.Debug.WriteLine($"Error loading DB config, using SQLite fallback: {ex.Message}");
                    var fallbackConnectionString = configuration.GetConnectionString("DefaultConnection")
                        ?? GetDefaultConnectionString();
                    options.UseSqlite(fallbackConnectionString);
                }
            });

            // Репозитории
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
            
            // Email репозитории
            services.AddScoped<IContactRepository, WindowsLauncher.Data.Repositories.ContactRepository>();
            services.AddScoped<ISmtpSettingsRepository, WindowsLauncher.Data.Repositories.SmtpSettingsRepository>();

            // Основные сервисы
            services.AddSingleton<IAuthenticationConfigurationService, AuthenticationConfigurationService>();
            services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IAuthorizationService, AuthorizationService>();
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<ICategoryManagementService, WindowsLauncher.Services.Categories.CategoryManagementService>();
            services.AddScoped<IAuditService, AuditService>();
            
            // Email сервисы
            services.AddScoped<WindowsLauncher.Core.Interfaces.Email.IEmailService, WindowsLauncher.Services.Email.EmailService>();
            services.AddScoped<WindowsLauncher.Core.Interfaces.Email.IAddressBookService, WindowsLauncher.Services.Email.AddressBookService>();
            
            // Новый сервис локальных пользователей
            services.AddScoped<ILocalUserService, LocalUserService>();
            
            // Управление сессиями
            services.AddSingleton<ISessionManagementService, SessionManagementService>();
            
            // Сервисы виртуальной клавиатуры с адаптивным выбором по версии Windows
            services.AddScoped<VirtualKeyboardService>(); // Универсальный сервис
            services.AddScoped<Windows10TouchKeyboardService>(); // Windows 10 специфичный
            services.AddScoped<VirtualKeyboardServiceFactory>(); // Фабрика для выбора подходящего сервиса
            services.AddScoped<IVirtualKeyboardService, AdaptiveVirtualKeyboardService>(); // Адаптивный сервис
            services.AddSingleton<WindowsLauncher.UI.Services.GlobalTouchKeyboardManager>();

            // Infrastructure сервисы
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<INavigationService, WpfNavigationService>();
            
            // Сервисы версионирования
            services.AddSingleton<IVersionService, VersionService>();
            services.AddScoped<IApplicationStartupService, ApplicationStartupService>();
            services.AddScoped<IApplicationVersionService, ApplicationVersionService>();
            
            // Сервис управления данными приложения
            services.AddScoped<ApplicationDataManager>();

            // ===== ANDROID ИНТЕГРАЦИЯ СЕРВИСЫ (РЕФАКТОРИРОВАННАЯ АРХИТЕКТУРА) =====
            
            // Базовые зависимости для Android сервисов
            services.AddSingleton<IProcessExecutor, ProcessExecutor>();
            
            // Специализированные Android сервисы (новая архитектура)
            services.AddSingleton<IWSAConnectionService, WSAConnectionService>();
            services.AddSingleton<IApkManagementService, ApkManagementService>();
            services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
            
            // Композитный сервис для обратной совместимости
            services.AddScoped<IWSAIntegrationService, WSAIntegrationService>();
            
            // Высокоуровневые сервисы
            services.AddScoped<IAndroidApplicationManager, AndroidApplicationManager>();
            services.AddSingleton<IAndroidSubsystemService, AndroidSubsystemService>();
            services.AddHostedService<AndroidSubsystemService>(provider => 
                (AndroidSubsystemService)provider.GetRequiredService<IAndroidSubsystemService>());

            // ✅ ДОБАВЛЯЕМ ЛОКАЛИЗАЦИЮ КАК СИНГЛ
            services.AddSingleton<LocalizationHelper>(_ => LocalizationHelper.Instance);

            // ===== НОВАЯ АРХИТЕКТУРА APPLICATION LIFECYCLE SERVICES =====
            
            // Основные сервисы жизненного цикла приложений
            services.AddSingleton<WindowsLauncher.Core.Interfaces.Lifecycle.IWindowManager, 
                WindowsLauncher.Services.Lifecycle.Windows.WindowManager>();
            services.AddSingleton<WindowsLauncher.Core.Interfaces.Lifecycle.IProcessMonitor, 
                WindowsLauncher.Services.Lifecycle.Monitoring.ProcessMonitor>();
            services.AddSingleton<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationInstanceManager, 
                WindowsLauncher.Services.Lifecycle.Management.ApplicationInstanceManager>();
            
            // Специализированные лаунчеры приложений
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.Services.Lifecycle.Launchers.DesktopApplicationLauncher>();
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.Services.Lifecycle.Launchers.ChromeAppLauncher>();
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.Services.Lifecycle.Launchers.WebApplicationLauncher>();
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.Services.Lifecycle.Launchers.FolderLauncher>();
            
            // WebView2 лаунчер для замены Chrome Apps
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.UI.Services.WebView2ApplicationLauncher>();
            
            // TextEditor лаунчер для встроенного текстового редактора
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.UI.Services.TextEditorApplicationLauncher>();
            
            // WSA лаунчер для Android приложений (APK)
            services.AddScoped<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLauncher, 
                WindowsLauncher.Services.Lifecycle.Launchers.WSAApplicationLauncher>();
            
            // Главный сервис управления жизненным циклом приложений
            services.AddSingleton<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService, 
                WindowsLauncher.Services.Lifecycle.ApplicationLifecycleService>();
            
            // ===== LEGACY SERVICES (для совместимости) =====
            
            // Сервисы управления запущенными приложениями удалены - используется ApplicationLifecycleService
            
            // Сервисы переключения приложений
            services.AddSingleton<WindowsLauncher.UI.Services.GlobalHotKeyService>();
            services.AddSingleton<WindowsLauncher.UI.Services.AppSwitcherService>();
            services.AddSingleton<WindowsLauncher.Core.Services.ShellModeDetectionService>();
            
            // Системная панель задач для Shell-режима
            services.AddSingleton<WindowsLauncher.UI.Components.SystemTaskbar.ISystemTaskbarService, 
                WindowsLauncher.UI.Components.SystemTaskbar.SystemTaskbarService>();
            
            // Сервис мониторинга системных событий сессии
            services.AddSingleton<WindowsLauncher.Services.SessionEventService>();

            // Сервис конфигурации языка
            services.AddSingleton<WindowsLauncher.Services.Configuration.ILanguageConfigurationService,
                WindowsLauncher.Services.Configuration.LanguageConfigurationService>();

            // WebView2 безопасность
            services.AddSingleton<WindowsLauncher.Core.Interfaces.Security.IWebView2SecurityConfigurationService,
                WindowsLauncher.Services.Security.WebView2SecurityConfigurationService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<AddressBookViewModel>();
            services.AddTransient<ComposeEmailViewModel>();
            services.AddTransient<ContactEditViewModel>();
            services.AddTransient<SmtpSettingsViewModel>();
            services.AddTransient<SmtpEditViewModel>();
            services.AddTransient<AdminViewModel>();

            // Memory Cache для авторизации
            services.AddMemoryCache();
        }

        private static string GetDefaultConnectionString()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appDataPath, "WindowsLauncher", "launcher.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// ✅ ИНИЦИАЛИЗАЦИЯ ЛОКАЛИЗАЦИИ
        /// </summary>
        private void InitializeLocalization()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🌐 App: Starting localization initialization");

                // ✅ ИСПРАВЛЕНИЕ: Устанавливаем системный язык сразу для правильного старта UI
                LocalizationHelper.Instance.SetSystemLanguage();
                System.Diagnostics.Debug.WriteLine($"🌐 App: System language set to {LocalizationHelper.Instance.CurrentLanguage}");

                // Подписываемся на изменения языка для обновления UI
                LocalizationHelper.Instance.LanguageChanged += OnLanguageChanged;
                
                System.Diagnostics.Debug.WriteLine("🌐 App: Localization initialization completed");
            }
            catch (Exception ex)
            {
                // При ошибке устанавливаем английский как fallback
                System.Diagnostics.Debug.WriteLine($"🌐 App: Localization initialization error: {ex.Message}");
                
                try
                {
                    LocalizationHelper.Instance.SetLanguage("en");
                    System.Diagnostics.Debug.WriteLine("🌐 App: Fallback to English language set");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("🌐 App: Failed to set fallback language");
                }
            }
        }

        /// <summary>
        /// ✅ ИНИЦИАЛИЗАЦИЯ ЯЗЫКА ЧЕРЕЗ НОВЫЙ СЕРВИС (вызывается после создания ServiceProvider)
        /// </summary>
        private async Task InitializeLanguageServiceAsync()
        {
            try
            {
                var languageService = ServiceProvider.GetService<WindowsLauncher.Services.Configuration.ILanguageConfigurationService>();
                if (languageService != null)
                {
                    var recommendedLanguage = await languageService.InitializeLanguageAsync();
                    
                    // Применяем рекомендованный язык через LocalizationHelper
                    LocalizationHelper.Instance.SetLanguage(recommendedLanguage);
                    
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogInformation("Language service initialized successfully with language: {Language}", recommendedLanguage);
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error initializing language service");
                
                // Fallback - используем системный язык
                try
                {
                    LocalizationHelper.Instance.SetSystemLanguage();
                }
                catch
                {
                    // Последний fallback - английский
                    LocalizationHelper.Instance.SetLanguage("en");
                }
            }
        }

        /// <summary>
        /// Обработчик изменения языка
        /// </summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try
            {
                // Обновляем заголовки окон и другие элементы UI
                UpdateAllWindowTitles();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogWarning(ex, "Error updating UI after language change");
            }
        }

        /// <summary>
        /// Обновление заголовков всех открытых окон
        /// </summary>
        private void UpdateAllWindowTitles()
        {
            foreach (Window window in Windows)
            {
                try
                {
                    // Обновляем заголовки через Binding или прямое обновление
                    if (window is LoginWindow)
                    {
                        window.Title = LocalizationHelper.Instance.GetString("LoginWindow_Title");
                    }
                    else if (window is MainWindow)
                    {
                        window.Title = LocalizationHelper.Instance.GetString("MainWindow_Title");
                    }
                    // Добавьте другие окна по необходимости
                }
                catch
                {
                    // Игнорируем ошибки обновления отдельных окон
                }
            }
        }

        private void InitializeGlobalTouchKeyboardManager()
        {
            try
            {
                var globalManager = ServiceProvider.GetService<WindowsLauncher.UI.Services.GlobalTouchKeyboardManager>();
                if (globalManager != null)
                {
                    globalManager.Initialize();
                    
                    var logger = ServiceProvider.GetService<ILogger<App>>();
                    logger?.LogInformation("GlobalTouchKeyboardManager успешно инициализирован");
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Ошибка инициализации GlobalTouchKeyboardManager");
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            using var scope = ServiceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
            
            try
            {
                logger.LogInformation("Starting database initialization...");
                
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();

                // Обновляем роль существующего пользователя guest если нужно
                var dbContext = scope.ServiceProvider.GetRequiredService<LauncherDbContext>();
                await UpdateGuestUserRole.UpdateGuestUserIfNeededAsync(dbContext);

                logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize database: {Message}", ex.Message);
                
                // Пытаемся fallback к SQLite если была Firebird конфигурация
                try
                {
                    logger?.LogWarning("Attempting fallback to SQLite database...");
                    var dbConfigService = scope.ServiceProvider.GetService<IDatabaseConfigurationService>();
                    if (dbConfigService != null)
                    {
                        var defaultConfig = dbConfigService.GetDefaultConfiguration(); // SQLite по умолчанию
                        await dbConfigService.SaveConfigurationAsync(defaultConfig);
                        logger?.LogInformation("Fallback to SQLite configuration saved, restart required");
                    }
                }
                catch (Exception fallbackEx)
                {
                    logger?.LogError(fallbackEx, "Fallback to SQLite also failed");
                }
                
                // Показываем пользователю ошибку но не останавливаем приложение
                System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
                
                // В debug режиме можно продолжить без БД
                #if DEBUG
                logger?.LogWarning("Continuing in debug mode without database");
                #else
                MessageBox.Show($"Ошибка инициализации базы данных:\n{ex.Message}\n\nПриложение будет закрыто.", 
                    "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
                #endif
            }
        }

        /// <summary>
        /// ✅ ПРАВИЛЬНЫЙ ЗАПУСК С ПРОВЕРКОЙ КОНФИГУРАЦИИ И БД
        /// </summary>
        private async Task StartApplicationAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting application with proper lifecycle checks");

                var startupService = ServiceProvider.GetRequiredService<IApplicationStartupService>();
                var action = await startupService.DetermineStartupActionAsync();
                
                switch (action)
                {
                    case StartupAction.ShowSetup:
                        logger.LogInformation("Showing setup window - initial configuration required");
                        ShowSetupWindow();
                        break;
                        
                    case StartupAction.PerformMigrations:
                        logger.LogInformation("Performing database migrations before login");
                        await PerformMigrationsAndShowLogin();
                        break;
                        
                    case StartupAction.ShowLogin:
                        logger.LogInformation("Application ready - showing login screen");
                        ShowLoginWindow();
                        break;
                        
                    default:
                        throw new InvalidOperationException($"Unknown startup action: {action}");
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start application");
                throw;
            }
        }
        
        /// <summary>
        /// Выполнить миграции БД и показать логин
        /// </summary>
        private async Task PerformMigrationsAndShowLogin()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting database migrations...");
                
                var startupService = ServiceProvider.GetRequiredService<IApplicationStartupService>();
                await startupService.PrepareApplicationAsync();
                
                logger.LogInformation("Database migrations completed, showing login");
                ShowLoginWindow();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Database migration failed");
                ShowStartupError(ex);
                Shutdown(1);
            }
        }

        /// <summary>
        /// ✅ ПОКАЗ ОКНА НАСТРОЙКИ (ПЕРВЫЙ ЗАПУСК)
        /// </summary>
        private void ShowSetupWindow()
        {
            try
            {
                var setupWindow = new SetupWindow(
                    ServiceProvider.GetRequiredService<IAuthenticationConfigurationService>(),
                    ServiceProvider.GetRequiredService<IAuthenticationService>(),
                    ServiceProvider.GetRequiredService<IActiveDirectoryService>(),
                    ServiceProvider.GetRequiredService<ILogger<SetupWindow>>(),
                    ServiceProvider
                );

                // Показываем модально
                var result = setupWindow.ShowDialog();

                if (result == true)
                {
                    // Настройка завершена - показываем окно входа
                    ShowLoginWindow();
                }
                else
                {
                    // Пользователь отменил настройку
                    Shutdown(0);
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to show setup window");
                ShowStartupError(ex);
                Shutdown(1);
            }
        }

        /// <summary>
        /// ✅ ПОКАЗ ОКНА ВХОДА
        /// </summary>
        public async void ShowLoginWindow()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Showing login window");
                
                var loginWindow = new LoginWindow();
                
                // Показываем модально
                var result = loginWindow.ShowDialog();
                logger.LogDebug("LoginWindow closed with result: {Result}", result);

                if (result == true && loginWindow.AuthenticatedUser != null)
                {
                    // Успешная аутентификация - показываем главное окно
                    logger.LogInformation("Calling ShowMainWindow for user: {Username}", loginWindow.AuthenticatedUser.Username);
                    ShowMainWindow(loginWindow.AuthenticatedUser);
                }
                else
                {
                    logger.LogWarning("Login failed or cancelled - result: {Result}, user: {User}", 
                        result, loginWindow.AuthenticatedUser?.Username ?? "null");
                    
                    // Проверяем режим Shell
                    var sessionManager = ServiceProvider?.GetService<ISessionManagementService>();
                    bool isShellMode = false;
                    
                    if (sessionManager != null)
                    {
                        await sessionManager.LoadConfigurationAsync();
                        isShellMode = sessionManager.Configuration.RunAsShell;
                    }

                    if (isShellMode)
                    {
                        // В Shell режиме при отмене входа показываем диалог снова
                        logger.LogInformation("Shell mode: login cancelled, showing login window again");
                        
                        // Небольшая задержка чтобы избежать бесконечного цикла
                        await Task.Delay(500);
                        
                        // Рекурсивно показываем окно входа снова
                        ShowLoginWindow();
                    }
                    else
                    {
                        // В обычном режиме при отмене входа:
                        // - Если есть MainWindow - показываем его (возврат к рабочему окну)
                        // - Если нет MainWindow - завершаем приложение (закрытие крестиком при старте)
                        if (MainWindow != null && !MainWindow.IsVisible)
                        {
                            logger.LogInformation("Normal mode: login cancelled, returning to MainWindow");
                            MainWindow.Show();
                            MainWindow.Activate();
                        }
                        else if (MainWindow == null)
                        {
                            logger.LogInformation("Normal mode: login cancelled at startup, shutting down");
                            Shutdown(0);
                        }
                        else
                        {
                            // MainWindow уже видно, просто активируем его
                            logger.LogInformation("Normal mode: login cancelled, activating existing MainWindow");
                            MainWindow.Activate();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to show login window");
                ShowStartupError(ex);
                Shutdown(1);
            }
        }

        /// <summary>
        /// ✅ ПОКАЗ ГЛАВНОГО ОКНА С ПЕРЕДАННЫМ ПОЛЬЗОВАТЕЛЕМ
        /// </summary>
        private async void ShowMainWindow(User authenticatedUser)
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting MainWindow for user {Username}", authenticatedUser.Username);

                // Инициализируем сессию через SessionManagementService
                var sessionManager = ServiceProvider.GetRequiredService<ISessionManagementService>();
                await sessionManager.LoadConfigurationAsync();
                await sessionManager.StartSessionAsync(authenticatedUser);

                var mainWindow = new MainWindow();
                logger.LogInformation("Main window created successfully");

                // Передаем аутентифицированного пользователя в ViewModel
                if (mainWindow.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.CurrentUser = authenticatedUser;
                    logger.LogInformation("ViewModel configured successfully");
                }


                // ShutdownMode уже настроен по умолчанию
                
                // Устанавливаем главное окно
                MainWindow = mainWindow;
                
                // Добавляем обработчик закрытия главного окна
                mainWindow.Closed += async (s, e) => 
                {
                    await HandleMainWindowClosedAsync(logger);
                };
                
                logger.LogInformation("About to show main window, ShutdownMode: {ShutdownMode}", ShutdownMode);
                
                mainWindow.Show();
                logger.LogInformation("Main window shown for user {Username}", authenticatedUser.Username);
                
                // Инициализируем системный трей после показа главного окна
                await InitializeSystemTrayAsync();
                
                // Запускаем мониторинг запущенных приложений
                await InitializeRunningApplicationsMonitoringAsync();
                
                // Инициализируем системную панель задач для Shell-режима
                await InitializeSystemTaskbarAsync();
                
                // Проверяем что окно действительно открыто
                logger.LogInformation("MainWindow state - IsVisible: {IsVisible}, IsLoaded: {IsLoaded}", 
                                    mainWindow.IsVisible, mainWindow.IsLoaded);
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to show main window: {Message}", ex.Message);
                logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                ShowStartupError(ex);
            }
        }

        /// <summary>
        /// Инициализация системного трея
        /// </summary>
        private async Task InitializeSystemTrayAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                // SystemTrayManager удален - используется встроенный трей MainWindow
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to initialize system tray manager");
                // Не прерываем работу приложения из-за ошибки системного трея
            }
        }

        /// <summary>
        /// Инициализация мониторинга запущенных приложений
        /// </summary>
        private async Task InitializeRunningApplicationsMonitoringAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting application lifecycle monitoring");

                // ===== НОВАЯ АРХИТЕКТУРА: ApplicationLifecycleService =====
                var lifecycleService = ServiceProvider.GetRequiredService<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService>();
                await lifecycleService.StartMonitoringAsync();
                logger.LogInformation("ApplicationLifecycleService monitoring started successfully");

                // Legacy RunningApplicationsService удален - используется ApplicationLifecycleService
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start application monitoring services");
                // Не прерываем работу приложения из-за ошибки мониторинга
            }
        }

        /// <summary>
        /// Инициализация системной панели задач
        /// </summary>
        private async Task InitializeSystemTaskbarAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting system taskbar initialization");

                var taskbarService = ServiceProvider.GetRequiredService<WindowsLauncher.UI.Components.SystemTaskbar.ISystemTaskbarService>();
                await taskbarService.InitializeAsync();
                
                logger.LogInformation("System taskbar initialized successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to initialize system taskbar");
                // Не прерываем работу приложения из-за ошибки панели задач
            }
        }

        private void SetupExceptionHandling()
        {
            DispatcherUnhandledException += (sender, e) =>
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unhandled dispatcher exception: {Message}", e.Exception.Message);
                logger?.LogError(e.Exception, "Stack trace: {StackTrace}", e.Exception.StackTrace);

                MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}", 
                               "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                Shutdown(1);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                var exception = e.ExceptionObject as Exception;
                logger?.LogError(exception, "Unhandled domain exception: {Message}", exception?.Message);
                
                MessageBox.Show($"Unhandled domain exception: {exception?.Message}", 
                               "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void ShowStartupError(Exception ex)
        {
            var locHelper = LocalizationHelper.Instance;

            var message = locHelper.GetFormattedString("Error_StartupFailed", ex.Message);
            if (ex.InnerException != null)
            {
                message += "\n\n" + locHelper.GetFormattedString("Error_Details", ex.InnerException.Message);
            }

            MessageBox.Show(
                message,
                locHelper.GetString("Error_StartupError"),
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        /// <summary>
        /// ✅ ПУБЛИЧНЫЙ МЕТОД ДЛЯ СМЕНЫ ЯЗЫКА ИЗ UI
        /// </summary>
        public void ChangeLanguage(string languageCode)
        {
            try
            {
                LocalizationHelper.Instance.SetLanguage(languageCode);
                LocalizationHelper.Instance.SaveLanguageSettings();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogWarning(ex, "Failed to change language to {Language}", languageCode);
            }
        }

        /// <summary>
        /// ✅ МЕТОД ДЛЯ ВЫХОДА С ПОДТВЕРЖДЕНИЕМ
        /// </summary>
        public void LogoutAndShowLogin()
        {
            try
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogInformation("Logout requested via LogoutAndShowLogin method");

                // Закрываем главное окно - обработчик HandleMainWindowClosedAsync сам решит что делать
                MainWindow?.Close();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error during logout");
                Shutdown(1);
            }
        }

        /// <summary>
        /// Обработка закрытия главного окна с учетом режима Shell
        /// </summary>
        private async Task HandleMainWindowClosedAsync(ILogger<App> logger)
        {
            try
            {
                logger.LogInformation("MainWindow closed, handling cleanup");

                // Определяем режим Shell через ShellModeDetectionService
                var shellModeDetectionService = ServiceProvider?.GetService<WindowsLauncher.Core.Services.ShellModeDetectionService>();
                var currentShellMode = ShellMode.Normal;
                
                if (shellModeDetectionService != null)
                {
                    currentShellMode = await shellModeDetectionService.DetectShellModeAsync();
                    var modeDescription = shellModeDetectionService.GetModeDescription(currentShellMode);
                    logger.LogDebug("Detected shell mode: {ModeDescription}", modeDescription);
                }
                else
                {
                    logger.LogWarning("ShellModeDetectionService is null, using Normal mode");
                }

                // Также получаем конфигурацию сессии для дополнительных настроек
                logger.LogInformation("App Step 4: Getting SessionManagementService");
                var sessionManager = ServiceProvider?.GetService<ISessionManagementService>();
                if (sessionManager != null)
                {
                    logger.LogInformation("App Step 5: Loading session configuration");
                    await sessionManager.LoadConfigurationAsync();
                }
                else
                {
                    logger.LogWarning("App Step 5: SessionManagementService is null");
                }

                // ИСПРАВЛЕНИЕ: Закрываем все запущенные приложения перед закрытием MainWindow
                var lifecycleService = ServiceProvider?.GetService<WindowsLauncher.Core.Interfaces.Lifecycle.IApplicationLifecycleService>();
                if (lifecycleService != null)
                {
                    logger.LogInformation("Closing all applications before MainWindow closed");
                    var shutdownResult = await lifecycleService.ShutdownAllAsync(gracefulTimeoutMs: 3000, finalTimeoutMs: 1000);
                    
                    logger.LogInformation("Application shutdown for MainWindow close: {Success}, {Total} apps, {Graceful} graceful, {Forced} forced", 
                        shutdownResult.Success, 
                        shutdownResult.TotalApplications,
                        shutdownResult.Applications.Count(a => a.Success && a.Method == WindowsLauncher.Core.Models.Lifecycle.ShutdownMethod.Graceful),
                        shutdownResult.Applications.Count(a => a.Success && a.Method == WindowsLauncher.Core.Models.Lifecycle.ShutdownMethod.Forced));
                }

                // ИСПРАВЛЕНИЕ: В любом режиме (Shell/Normal) возвращаемся к LoginWindow
                logger.LogInformation("App Step 8: Returning to login window after logout (Mode: {Mode})", 
                    currentShellMode == ShellMode.Shell ? "Shell" : "Normal");
                
                // ВАЖНО: Явно завершаем сессию если она еще активна
                if (sessionManager != null && sessionManager.IsSessionActive)
                {
                    logger.LogInformation("App Step 9: Session is still active, ending session");
                    await sessionManager.EndSessionAsync($"MainWindow closed in {currentShellMode} mode");
                    logger.LogInformation("App Step 10: Session ended successfully");
                }
                else
                {
                    logger.LogInformation("App Step 9: No active session to end");
                }
                
                // Небольшая задержка для гарантии завершения всех операций
                logger.LogInformation("App Step 11: Waiting 200ms for cleanup");
                await Task.Delay(200);
                
                // Сбрасываем MainWindow чтобы не было конфликтов
                logger.LogInformation("App Step 12: Resetting MainWindow to null");
                MainWindow = null;
                
                // Возвращаемся к окну входа (приложение НЕ завершается)
                logger.LogInformation("App Step 13: Dispatching ShowLoginWindow");
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        logger.LogInformation("App Step 14: Inside Dispatcher - showing login window");
                        ShowLoginWindow();
                        logger.LogInformation("App Step 15: ShowLoginWindow called successfully");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "App Step 14 FAILED: Error returning to login window after logout");
                        Shutdown(1);
                    }
                });
                
                logger.LogInformation("=== APP HANDLEMAINWINDOWCLOSED COMPLETED ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "=== APP HANDLEMAINWINDOWCLOSED FAILED ===");
                logger.LogError(ex, "Error handling MainWindow close");
                Shutdown(1);
            }
        }
    }
}
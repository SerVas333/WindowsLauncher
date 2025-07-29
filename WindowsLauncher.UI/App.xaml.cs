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
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.Core.Services;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Views;
using WindowsLauncher.Core.Configuration;

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

                // Инициализация базы данных
                await InitializeDatabaseAsync();

                // Настройка глобальной обработки исключений
                SetupExceptionHandling();

                // Инициализация глобального менеджера сенсорной клавиатуры
                InitializeGlobalTouchKeyboardManager();

                // ✅ ЗАПУСК ЧЕРЕЗ LoginWindow ВМЕСТО ПРЯМОГО ПОКАЗА MainWindow
                await StartApplicationAsync();

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
                // Очищаем глобальный менеджер сенсорной клавиатуры
                try
                {
                    var globalManager = ServiceProvider?.GetService<WindowsLauncher.UI.Services.GlobalTouchKeyboardManager>();
                    globalManager?.Dispose();
                }
                catch (Exception ex)
                {
                    var logger = ServiceProvider?.GetService<ILogger<App>>();
                    logger?.LogWarning(ex, "Ошибка при очистке GlobalTouchKeyboardManager");
                }

                // Сохраняем настройки локализации
                LocalizationHelper.Instance.SaveLanguageSettings();

                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }
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

            // Основные сервисы
            services.AddSingleton<IAuthenticationConfigurationService, AuthenticationConfigurationService>();
            services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IAuthorizationService, AuthorizationService>();
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<IAuditService, AuditService>();
            
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

            // ✅ ДОБАВЛЯЕМ ЛОКАЛИЗАЦИЮ КАК СИНГЛ
            services.AddSingleton<LocalizationHelper>(_ => LocalizationHelper.Instance);

            // Сервисы управления запущенными приложениями
            services.AddSingleton<IRunningApplicationsService, RunningApplicationsService>();
            services.AddSingleton<WindowsLauncher.UI.Components.SystemTray.SystemTrayManager>();
            
            // Сервисы переключения приложений
            services.AddSingleton<WindowsLauncher.UI.Services.GlobalHotKeyService>();
            services.AddSingleton<WindowsLauncher.UI.Services.AppSwitcherService>();
            services.AddSingleton<WindowsLauncher.UI.Services.ShellModeDetectionService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
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
                // Загружаем настройки языка
                LocalizationHelper.Instance.LoadLanguageSettings();

                // Подписываемся на изменения языка для обновления UI
                LocalizationHelper.Instance.LanguageChanged += OnLanguageChanged;
            }
            catch (Exception ex)
            {
                // При ошибке используем системный язык
                LocalizationHelper.Instance.SetSystemLanguage();

                // Логируем ошибку (если логгер доступен)
                System.Diagnostics.Debug.WriteLine($"Localization initialization error: {ex.Message}");
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
        private async void ShowLoginWindow()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                var loginWindow = new LoginWindow();

                // Показываем модально
                var result = loginWindow.ShowDialog();

                logger.LogInformation("LoginWindow closed with result: {Result}", result);
                logger.LogInformation("AuthenticationResult: {AuthResult}", loginWindow.AuthenticationResult != null ? "not null" : "null");
                logger.LogInformation("AuthenticatedUser: {AuthUser}", loginWindow.AuthenticatedUser?.Username ?? "null");

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
                        // В обычном режиме завершаем приложение
                        logger.LogInformation("Standard mode: login cancelled, shutting down");
                        Shutdown(0);
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
                logger.LogInformation("=== STARTING ShowMainWindow for user {Username} ===", authenticatedUser.Username);

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
                logger.LogInformation("Initializing system tray manager");

                var systemTrayManager = ServiceProvider.GetRequiredService<WindowsLauncher.UI.Components.SystemTray.SystemTrayManager>();
                await systemTrayManager.InitializeAsync();

                logger.LogInformation("System tray manager initialized successfully");
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
                logger.LogInformation("Starting running applications monitoring");

                var runningAppsService = ServiceProvider.GetRequiredService<IRunningApplicationsService>();
                await runningAppsService.StartMonitoringAsync();

                logger.LogInformation("Running applications monitoring started successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start running applications monitoring");
                // Не прерываем работу приложения из-за ошибки мониторинга
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
                logger.LogInformation("MainWindow closed, checking shell mode configuration");

                // Получаем конфигурацию сессии
                var sessionManager = ServiceProvider?.GetService<ISessionManagementService>();
                if (sessionManager != null)
                {
                    await sessionManager.LoadConfigurationAsync();
                    var config = sessionManager.Configuration;

                    logger.LogInformation("Shell mode configuration - RunAsShell: {RunAsShell}, ReturnToLoginOnLogout: {ReturnToLogin}", 
                        config.RunAsShell, config.ReturnToLoginOnLogout);

                    if (config.RunAsShell && config.ReturnToLoginOnLogout)
                    {
                        logger.LogInformation("Shell mode: ending current session and returning to login window");
                        
                        // ВАЖНО: Явно завершаем сессию перед переходом к LoginWindow
                        await sessionManager.EndSessionAsync("MainWindow closed in shell mode");
                        logger.LogInformation("Session ended successfully");
                        
                        // Небольшая задержка для гарантии завершения всех операций
                        await Task.Delay(200);
                        
                        // Сбрасываем MainWindow чтобы не было конфликтов
                        MainWindow = null;
                        
                        // Возвращаемся к окну входа
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                ShowLoginWindow();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error returning to login window in shell mode");
                                Shutdown(1);
                            }
                        });
                        return;
                    }
                }

                // Обычный режим - завершаем приложение
                logger.LogInformation("Standard mode: shutting down application");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling MainWindow close");
                Shutdown(1);
            }
        }
    }
}
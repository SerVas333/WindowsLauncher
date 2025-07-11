// ===== WindowsLauncher.UI/App.xaml.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ БЕЗ ОШИБОК =====
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
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
using WindowsLauncher.UI.ViewModels;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.Infrastructure.Localization;
using WindowsLauncher.UI.Views;

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI
{
    public partial class App : WpfApplication
    {
        private IHost _host;
        public IServiceProvider ServiceProvider { get; private set; }

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

            // База данных
            services.AddDbContext<LauncherDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? GetDefaultConnectionString();
                options.UseSqlite(connectionString);
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

            // Infrastructure сервисы
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<INavigationService, WpfNavigationService>();

            // ✅ ДОБАВЛЯЕМ ЛОКАЛИЗАЦИЮ КАК СИНГЛ
            services.AddSingleton<LocalizationHelper>(_ => LocalizationHelper.Instance);

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
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
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
        private void OnLanguageChanged(object sender, EventArgs e)
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

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
                
                logger.LogInformation("Starting database initialization...");
                
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();

                logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize database: {Message}", ex.Message);
                
                // Показываем пользователю ошибку но не останавливаем приложение
                System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
                
                // В debug режиме можно продолжить без БД
                #if DEBUG
                logger?.LogWarning("Continuing in debug mode without database");
                #else
                throw;
                #endif
            }
        }

        /// <summary>
        /// ✅ ЗАПУСК ЧЕРЕЗ LoginWindow
        /// </summary>
        private async Task StartApplicationAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting application");

                // Проверяем, настроен ли сервисный администратор
                var configService = ServiceProvider.GetRequiredService<IAuthenticationConfigurationService>();
                var config = configService.GetConfiguration();

                if (!config.ServiceAdmin.IsPasswordSet)
                {
                    logger.LogInformation("Service admin not configured, showing setup window");
                    ShowSetupWindow();
                }
                else
                {
                    logger.LogInformation("Service admin configured, showing login screen");
                    ShowLoginWindow();
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
                    ServiceProvider.GetRequiredService<ILogger<SetupWindow>>()
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
        private void ShowLoginWindow()
        {
            try
            {
                var loginWindow = new LoginWindow();

                // Показываем модально
                var result = loginWindow.ShowDialog();

                if (result == true && loginWindow.AuthenticatedUser != null)
                {
                    // Успешная аутентификация - показываем главное окно
                    ShowMainWindow(loginWindow.AuthenticatedUser);
                }
                else
                {
                    // Пользователь отменил вход или ошибка аутентификации
                    Shutdown(0);
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
        private void ShowMainWindow(User authenticatedUser)
        {
            try
            {
                var mainWindow = new MainWindow();

                // Передаем аутентифицированного пользователя в ViewModel
                if (mainWindow.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.CurrentUser = authenticatedUser;
                }

                MainWindow = mainWindow;
                mainWindow.Show();

                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Main window shown for user {Username}", authenticatedUser.Username);
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to show main window");
                ShowStartupError(ex);
            }
        }

        private void SetupExceptionHandling()
        {
            DispatcherUnhandledException += (sender, e) =>
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unhandled dispatcher exception");

                var errorMessage = LocalizationHelper.Instance.GetFormattedString(
                    "Error_UnhandledException",
                    e.Exception.Message
                );

                MessageBox.Show(
                    errorMessage,
                    LocalizationHelper.Instance.GetString("Error_CriticalError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                e.Handled = true;
                Shutdown(1);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(e.ExceptionObject as Exception, "Unhandled domain exception");
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
                // Закрываем главное окно
                MainWindow?.Close();

                // Показываем окно входа снова
                ShowLoginWindow();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error during logout");
                Shutdown(1);
            }
        }
    }
}
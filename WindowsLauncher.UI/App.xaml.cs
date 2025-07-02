// App.xaml.cs - Исправленная конфигурация DI
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

// ✅ РЕШЕНИЕ КОНФЛИКТА: Явные алиасы для Application
using WpfApplication = System.Windows.Application;
using CoreApplication = WindowsLauncher.Core.Models.Application;

namespace WindowsLauncher.UI
{
    public partial class App : WpfApplication // ✅ Явно указываем WpfApplication
    {
        private IHost _host;
        public IServiceProvider ServiceProvider { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Настройка Host с конфигурацией и DI
                _host = CreateHostBuilder(e.Args).Build();
                ServiceProvider = _host.Services;

                // Инициализация базы данных
                await InitializeDatabaseAsync();

                // Настройка глобальной обработки исключений
                SetupExceptionHandling();

                // Запуск приложения
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
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
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

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();

                var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "Failed to initialize database");
                throw;
            }
        }

        private async Task StartApplicationAsync()
        {
            try
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Starting application");

                // Показываем главное окно
                ShowMainWindow();
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to start application");
                throw;
            }
        }

        private void ShowMainWindow()
        {
            try
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
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

                MessageBox.Show(
                    $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПриложение будет закрыто.",
                    "Критическая ошибка",
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
            var message = $"Не удалось запустить приложение:\n\n{ex.Message}";
            if (ex.InnerException != null)
            {
                message += $"\n\nДетали: {ex.InnerException.Message}";
            }

            MessageBox.Show(message, "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
// App.xaml.cs - Обновленная конфигурация DI

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
using WindowsLauncher.Services;
using WindowsLauncher.Services.Applications;
using WindowsLauncher.Services.Audit;
using WindowsLauncher.Services.Authorization;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI
{
    public partial class App : Application
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

        /// <summary>
        /// Создание Host Builder с конфигурацией
        /// </summary>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Конфигурация из файлов
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

                    // Добавляем файловое логирование
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "WindowsLauncher", "Logs"
                    );
                    Directory.CreateDirectory(logPath);

                    // TODO: Добавить файловый провайдер логирования
                    // logging.AddFile(Path.Combine(logPath, "app.log"));
                });

        /// <summary>
        /// Конфигурация сервисов DI
        /// </summary>
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

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<AdminViewModel>();
            services.AddTransient<LoginViewModel>();

            // Дополнительные сервисы
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
        }

        /// <summary>
        /// Получение строки подключения по умолчанию
        /// </summary>
        private static string GetDefaultConnectionString()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appDataPath, "WindowsLauncher", "launcher.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// Инициализация базы данных
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<LauncherDbContext>();

                // Применяем миграции
                await context.Database.MigrateAsync();

                // Инициализируем начальные данные
                await context.SeedDataAsync();

                var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to initialize database");
                throw;
            }
        }

        /// <summary>
        /// Запуск приложения с аутентификацией
        /// </summary>
        private async Task StartApplicationAsync()
        {
            try
            {
                var authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();

                logger.LogInformation("Starting application authentication");

                // Проверяем, требуется ли первоначальная настройка
                if (Views.SetupWindow.IsSetupRequired())
                {
                    logger.LogInformation("Initial setup required");

                    var setupCompleted = Views.SetupWindow.ShowSetupDialog();
                    if (!setupCompleted)
                    {
                        logger.LogInformation("Setup was skipped or cancelled");
                        // Продолжаем с настройками по умолчанию
                    }
                    else
                    {
                        logger.LogInformation("Initial setup completed successfully");
                    }
                }

                // Попытка автоматической аутентификации
                var authResult = await authService.AuthenticateAsync();

                if (authResult.IsSuccess)
                {
                    // Успешная аутентификация - показываем главное окно
                    ShowMainWindow();
                }
                else
                {
                    // Требуется ручная аутентификация
                    await ShowLoginProcessAsync(authResult);
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
        /// Процесс ручной аутентификации
        /// </summary>
        private async Task ShowLoginProcessAsync(AuthenticationResult initialResult)
        {
            var authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
            var logger = ServiceProvider.GetRequiredService<ILogger<App>>();

            // Определяем, нужно ли показать окно входа
            var shouldShowLogin = initialResult.Status switch
            {
                AuthenticationStatus.DomainUnavailable => true,
                AuthenticationStatus.InvalidCredentials => true,
                AuthenticationStatus.UserNotFound => true,
                AuthenticationStatus.NetworkError => true,
                AuthenticationStatus.ServiceModeRequired => true,
                _ => true
            };

            if (!shouldShowLogin)
            {
                logger.LogError("Authentication failed with status {Status}: {Error}",
                    initialResult.Status, initialResult.ErrorMessage);
                ShowStartupError(new Exception(initialResult.ErrorMessage));
                return;
            }

            // Показываем окно входа
            var loginWindow = new Views.LoginWindow(initialResult.ErrorMessage);

            var loginResult = loginWindow.ShowDialog();

            if (loginResult == true && loginWindow.AuthenticationResult?.IsSuccess == true)
            {
                logger.LogInformation("Manual authentication successful");
                ShowMainWindow();
            }
            else
            {
                logger.LogInformation("Authentication cancelled by user");
                Shutdown(0);
            }
        }

        /// <summary>
        /// Показ главного окна
        /// </summary>
        private void ShowMainWindow()
        {
            try
            {
                var mainWindow = new Views.MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Main window displayed successfully");
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Failed to show main window");
                ShowStartupError(ex);
            }
        }

        /// <summary>
        /// Настройка глобальной обработки исключений
        /// </summary>
        private void SetupExceptionHandling()
        {
            // Обработка исключений в UI потоке
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

            // Обработка исключений в других потоках
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(e.ExceptionObject as Exception, "Unhandled domain exception");

                if (e.IsTerminating)
                {
                    MessageBox.Show(
                        $"Критическая ошибка приложения:\n\n{e.ExceptionObject}\n\nПриложение будет завершено.",
                        "Критическая ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            };
        }

        /// <summary>
        /// Показ ошибки запуска
        /// </summary>
        private void ShowStartupError(Exception ex)
        {
            var message = $"Не удалось запустить приложение:\n\n{ex.Message}";

            if (ex.InnerException != null)
            {
                message += $"\n\nДетали: {ex.InnerException.Message}";
            }

            MessageBox.Show(
                message,
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    /// <summary>
    /// Дополнительные сервисы для UI
    /// </summary>
    public interface IDialogService
    {
        bool ShowConfirmDialog(string message, string title = "Подтверждение");
        void ShowInfoDialog(string message, string title = "Информация");
        void ShowErrorDialog(string message, string title = "Ошибка");
        string ShowInputDialog(string message, string title = "Ввод", string defaultValue = "");
    }

    public class DialogService : IDialogService
    {
        public bool ShowConfirmDialog(string message, string title = "Подтверждение")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void ShowInfoDialog(string message, string title = "Информация")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowErrorDialog(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public string ShowInputDialog(string message, string title = "Ввод", string defaultValue = "")
        {
            // TODO: Реализовать собственное окно ввода или использовать стороннюю библиотеку
            return Microsoft.VisualBasic.Interaction.InputBox(message, title, defaultValue);
        }
    }

    public interface INotificationService
    {
        void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationService : INotificationService
    {
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            // TODO: Реализовать Toast уведомления
            // Пока используем MessageBox
            var icon = type switch
            {
                NotificationType.Success => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.Information
            };

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }

    public interface IFileDialogService
    {
        string ShowOpenFileDialog(string filter = "All files (*.*)|*.*", string title = "Открыть файл");
        string ShowSaveFileDialog(string filter = "All files (*.*)|*.*", string title = "Сохранить файл");
        string ShowFolderDialog(string title = "Выбрать папку");
    }

    public class FileDialogService : IFileDialogService
    {
        public string ShowOpenFileDialog(string filter = "All files (*.*)|*.*", string title = "Открыть файл")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string ShowSaveFileDialog(string filter = "All files (*.*)|*.*", string title = "Сохранить файл")
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string ShowFolderDialog(string title = "Выбрать папку")
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = title
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}
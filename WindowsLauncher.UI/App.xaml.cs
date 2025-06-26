// ===== WindowsLauncher.UI/App.xaml.cs - ОБНОВЛЕННЫЙ =====
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;
using WindowsLauncher.Data;
using WindowsLauncher.Data.Repositories;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Services.Authentication;
using WindowsLauncher.Services.Authorization;
using WindowsLauncher.Services.Applications;
using WindowsLauncher.Services.Audit;
using WindowsLauncher.UI.Infrastructure.Services;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Инициализируем язык перед созданием Host
            LocalizationManager.InitializeLanguage();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            base.OnStartup(e);
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Database
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.db");
            services.AddDbContext<LauncherDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // 🆕 Database Initializer
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();

            // Core Services
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IAuthorizationService, AuthorizationService>();
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<IAuditService, AuditService>();

            // UI Infrastructure Services
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<INavigationService, WpfNavigationService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Windows
            services.AddTransient<MainWindow>();

            // Memory cache for authorization
            services.AddMemoryCache();

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }

        public IServiceProvider ServiceProvider =>
            _host?.Services ?? throw new InvalidOperationException("Host not initialized");
    }
}
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data.Configurations;

namespace WindowsLauncher.Data
{
    public class LauncherDbContext : DbContext
    {
        public LauncherDbContext(DbContextOptions<LauncherDbContext> options) : base(options)
        {
        }

        // DbSets для наших моделей
        public DbSet<User> Users { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Применяем конфигурации
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
            modelBuilder.ApplyConfiguration(new UserSettingsConfiguration());
            modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

            // Убираем seed данные - добавим их программно
        }
    }
}
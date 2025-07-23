// WindowsLauncher.Data/Configurations/AuditLogConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        private readonly DatabaseType _databaseType;

        public AuditLogConfiguration(DatabaseType databaseType = DatabaseType.SQLite)
        {
            _databaseType = databaseType;
        }

        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(l => l.Action)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(l => l.ApplicationName)
                .HasMaxLength(200);

            builder.Property(l => l.Details)
                .HasMaxLength(1000);

            builder.Property(l => l.ErrorMessage)
                .HasMaxLength(1000);

            builder.Property(l => l.ComputerName)
                .HasMaxLength(100);

            builder.Property(l => l.IPAddress)
                .HasMaxLength(50);

            // Индексы для поиска логов
            builder.HasIndex(l => l.Username);
            builder.HasIndex(l => l.Action);
            builder.HasIndex(l => l.Timestamp);
            builder.HasIndex(l => new { l.Username, l.Timestamp });

            builder.ToTable("AuditLogs");
        }
    }
}
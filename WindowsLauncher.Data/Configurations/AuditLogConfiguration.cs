// WindowsLauncher.Data/Configurations/AuditLogConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            // ПРОСТОЕ РЕШЕНИЕ: Все имена в UPPERCASE для всех БД
            builder.ToTable("AUDIT_LOGS");
            builder.HasKey(l => l.Id);

            // Настройки свойств - UPPERCASE имена колонок
            builder.Property(l => l.Id).HasColumnName("ID");
            builder.Property(l => l.UserId).HasColumnName("USER_ID");
            builder.Property(l => l.Username).IsRequired().HasMaxLength(100).HasColumnName("USERNAME");
            builder.Property(l => l.Action).IsRequired().HasMaxLength(100).HasColumnName("ACTION");
            builder.Property(l => l.ApplicationName).HasMaxLength(200).HasColumnName("APPLICATION_NAME");
            builder.Property(l => l.Details).HasMaxLength(2000).HasColumnName("DETAILS");
            builder.Property(l => l.Timestamp).HasColumnName("TIMESTAMP_UTC");
            builder.Property(l => l.Success).HasColumnName("SUCCESS");
            builder.Property(l => l.ErrorMessage).HasMaxLength(1000).HasColumnName("ERROR_MESSAGE");
            builder.Property(l => l.ComputerName).HasMaxLength(100).HasColumnName("COMPUTER_NAME");
            builder.Property(l => l.IPAddress).HasMaxLength(45).HasColumnName("IP_ADDRESS");
            builder.Property(l => l.UserAgent).HasMaxLength(500).HasColumnName("USER_AGENT");
            builder.Property(l => l.MetadataJson).HasMaxLength(2000).HasColumnName("METADATA_JSON");

            // Индексы для поиска логов
            builder.HasIndex(l => l.Username);
            builder.HasIndex(l => l.Action);
            builder.HasIndex(l => l.Timestamp);
            builder.HasIndex(l => new { l.Username, l.Timestamp });
        }
    }
}
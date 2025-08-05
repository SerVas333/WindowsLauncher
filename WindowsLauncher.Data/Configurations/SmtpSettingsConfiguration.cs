using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Data.Configurations
{
    /// <summary>
    /// Конфигурация Entity Framework для таблицы SMTP_SETTINGS
    /// Совместима с SQLite и Firebird БД
    /// </summary>
    public class SmtpSettingsConfiguration : IEntityTypeConfiguration<SmtpSettings>
    {
        public void Configure(EntityTypeBuilder<SmtpSettings> builder)
        {
            // Имя таблицы в UPPERCASE для совместимости с Firebird
            builder.ToTable("SMTP_SETTINGS");
            
            // Первичный ключ
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id)
                .HasColumnName("ID")
                .ValueGeneratedOnAdd()
                .IsRequired();
            
            // Обязательные настройки сервера
            builder.Property(s => s.Host)
                .HasColumnName("HOST")
                .HasMaxLength(200)
                .IsRequired();
            
            builder.Property(s => s.Port)
                .HasColumnName("PORT")
                .IsRequired();
            
            builder.Property(s => s.Username)
                .HasColumnName("USERNAME")
                .HasMaxLength(200)
                .IsRequired();
            
            builder.Property(s => s.EncryptedPassword)
                .HasColumnName("ENCRYPTED_PASSWORD")
                .HasMaxLength(1000) // Зашифрованный пароль может быть длинным
                .IsRequired();
            
            // Настройки безопасности
            builder.Property(s => s.UseSSL)
                .HasColumnName("USE_SSL")
                .HasDefaultValue(true) // По умолчанию используем SSL
                .IsRequired();
            
            builder.Property(s => s.UseStartTLS)
                .HasColumnName("USE_STARTTLS")
                .HasDefaultValue(false)
                .IsRequired();
            
            // Тип сервера (Primary/Backup)
            builder.Property(s => s.ServerType)
                .HasColumnName("SERVER_TYPE")
                .HasConversion<int>() // Enum → int для совместимости с БД
                .IsRequired();
            
            // Настройки отправителя по умолчанию
            builder.Property(s => s.DefaultFromEmail)
                .HasColumnName("DEFAULT_FROM_EMAIL")
                .HasMaxLength(250)
                .IsRequired(false);
            
            builder.Property(s => s.DefaultFromName)
                .HasColumnName("DEFAULT_FROM_NAME")
                .HasMaxLength(200)
                .IsRequired(false);
            
            // Системные поля
            builder.Property(s => s.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasDefaultValue(true)
                .IsRequired();
            
            builder.Property(s => s.ConsecutiveErrors)
                .HasColumnName("CONSECUTIVE_ERRORS")
                .HasDefaultValue(0)
                .IsRequired();
            
            builder.Property(s => s.LastSuccessfulSend)
                .HasColumnName("LAST_SUCCESSFUL_SEND")
                .HasColumnType("TIMESTAMP")
                .IsRequired(false);
            
            builder.Property(s => s.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasColumnType("TIMESTAMP")
                .IsRequired();
            
            builder.Property(s => s.UpdatedAt)
                .HasColumnName("UPDATED_AT")
                .HasColumnType("TIMESTAMP")
                .IsRequired(false);
            
            // Индексы и ограничения
            
            // Уникальный индекс на тип сервера для активных настроек
            // Гарантирует что активен только один Primary и один Backup сервер
            builder.HasIndex(s => new { s.ServerType, s.IsActive })
                .HasDatabaseName("IX_SMTP_SETTINGS_TYPE_ACTIVE")
                .IsUnique()
                .HasFilter("IS_ACTIVE = 1");
            
            // Индекс для быстрого поиска по хосту
            builder.HasIndex(s => s.Host)
                .HasDatabaseName("IX_SMTP_SETTINGS_HOST");
            
            // Индекс для фильтрации активных настроек
            builder.HasIndex(s => s.IsActive)
                .HasDatabaseName("IX_SMTP_SETTINGS_ACTIVE");
            
            // Композитный индекс для мониторинга ошибок
            builder.HasIndex(s => new { s.IsActive, s.ConsecutiveErrors, s.LastSuccessfulSend })
                .HasDatabaseName("IX_SMTP_SETTINGS_ERROR_MONITORING");
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models.Email;

namespace WindowsLauncher.Data.Configurations
{
    /// <summary>
    /// Конфигурация Entity Framework для таблицы CONTACTS
    /// Совместима с SQLite и Firebird БД
    /// </summary>
    public class ContactConfiguration : IEntityTypeConfiguration<Contact>
    {
        public void Configure(EntityTypeBuilder<Contact> builder)
        {
            // Имя таблицы в UPPERCASE для совместимости с Firebird
            builder.ToTable("CONTACTS");
            
            // Первичный ключ
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .HasColumnName("ID")
                .ValueGeneratedOnAdd()
                .IsRequired();
            
            // Обязательные поля
            builder.Property(c => c.FirstName)
                .HasColumnName("FIRST_NAME")
                .HasMaxLength(50)
                .IsRequired();
            
            builder.Property(c => c.LastName)
                .HasColumnName("LAST_NAME")
                .HasMaxLength(50)
                .IsRequired();
            
            builder.Property(c => c.Email)
                .HasColumnName("EMAIL")
                .HasMaxLength(200)
                .IsRequired();
            
            // Необязательные поля
            builder.Property(c => c.Phone)
                .HasColumnName("PHONE")
                .HasMaxLength(20)
                .IsRequired(false);
            
            builder.Property(c => c.Company)
                .HasColumnName("COMPANY")
                .HasMaxLength(100)
                .IsRequired(false);
            
            builder.Property(c => c.Department)
                .HasColumnName("DEPARTMENT")
                .HasMaxLength(50)
                .IsRequired(false);
            
            builder.Property(c => c.Group)
                .HasColumnName("GROUP_NAME") // GROUP - зарезервированное слово в SQL
                .HasMaxLength(50)
                .IsRequired(false);
            
            builder.Property(c => c.Notes)
                .HasColumnName("NOTES")
                .HasMaxLength(500)
                .IsRequired(false);
            
            builder.Property(c => c.CreatedBy)
                .HasColumnName("CREATED_BY")
                .HasMaxLength(100)
                .IsRequired(false);
            
            // Системные поля
            builder.Property(c => c.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasDefaultValue(true) // По умолчанию контакт активен
                .IsRequired();
            
            builder.Property(c => c.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasColumnType("TIMESTAMP") // Универсальный тип для обеих БД
                .IsRequired();
            
            builder.Property(c => c.UpdatedAt)
                .HasColumnName("UPDATED_AT")
                .HasColumnType("TIMESTAMP")
                .IsRequired(false);
            
            // Индексы для производительности
            
            // Уникальный индекс на email для активных контактов
            // Позволяет несколько неактивных контактов с одинаковым email
            builder.HasIndex(c => new { c.Email, c.IsActive })
                .HasDatabaseName("IX_CONTACTS_EMAIL_ACTIVE")
                .IsUnique()
                .HasFilter("IS_ACTIVE = 1"); // SQLite/Firebird синтаксис
            
            // Индексы для быстрого поиска по имени
            builder.HasIndex(c => c.FirstName)
                .HasDatabaseName("IX_CONTACTS_FIRST_NAME");
            
            builder.HasIndex(c => c.LastName)
                .HasDatabaseName("IX_CONTACTS_LAST_NAME");
            
            // Индекс для поиска по email
            builder.HasIndex(c => c.Email)
                .HasDatabaseName("IX_CONTACTS_EMAIL");
            
            // Индекс для группировки по группам
            builder.HasIndex(c => c.Group)
                .HasDatabaseName("IX_CONTACTS_GROUP");
            
            // Индекс для отдела
            builder.HasIndex(c => c.Department)
                .HasDatabaseName("IX_CONTACTS_DEPARTMENT");
            
            // Композитный индекс для фильтрации активных контактов
            builder.HasIndex(c => new { c.IsActive, c.CreatedAt })
                .HasDatabaseName("IX_CONTACTS_ACTIVE_CREATED");
        }
    }
}
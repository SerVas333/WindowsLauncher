using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // ПРОСТОЕ РЕШЕНИЕ: Все имена в UPPERCASE для всех БД
            builder.ToTable("USERS");
            builder.HasKey(u => u.Id);

            // Настройки свойств - UPPERCASE имена колонок
            builder.Property(u => u.Id).HasColumnName("ID");
            builder.Property(u => u.Username).IsRequired().HasMaxLength(100).HasColumnName("USERNAME");
            builder.Property(u => u.DisplayName).HasMaxLength(200).HasColumnName("DISPLAY_NAME");
            builder.Property(u => u.Email).HasMaxLength(320).HasColumnName("EMAIL");
            builder.Property(u => u.Role).HasColumnName("ROLE");
            builder.Property(u => u.IsActive).HasColumnName("IS_ACTIVE");
            builder.Property(u => u.IsServiceAccount).HasColumnName("IS_SERVICE_ACCOUNT");
            builder.Property(u => u.PasswordHash).HasMaxLength(500).HasColumnName("PASSWORD_HASH");
            builder.Property(u => u.Salt).HasMaxLength(500).HasColumnName("SALT");
            builder.Property(u => u.CreatedAt).HasColumnName("CREATED_AT");
            builder.Property(u => u.LastLoginAt).HasColumnName("LAST_LOGIN_AT");
            builder.Property(u => u.LastActivityAt).HasColumnName("LAST_ACTIVITY_AT");
            builder.Property(u => u.FailedLoginAttempts).HasColumnName("FAILED_LOGIN_ATTEMPTS");
            builder.Property(u => u.IsLocked).HasColumnName("IS_LOCKED");
            builder.Property(u => u.LockoutEnd).HasColumnName("LOCKOUT_END");
            builder.Property(u => u.LastPasswordChange).HasColumnName("LAST_PASSWORD_CHANGE");
            builder.Property(u => u.GroupsJson).HasColumnName("GROUPS_JSON");
            builder.Property(u => u.SettingsJson).HasColumnName("SETTINGS_JSON");
            builder.Property(u => u.MetadataJson).HasColumnName("METADATA_JSON");
            
            // Гибридная авторизация
            builder.Property(u => u.AuthenticationType).HasColumnName("AUTHENTICATION_TYPE");
            builder.Property(u => u.DomainUsername).HasMaxLength(100).HasColumnName("DOMAIN_USERNAME");
            builder.Property(u => u.LastDomainSync).HasColumnName("LAST_DOMAIN_SYNC");
            builder.Property(u => u.IsLocalUser).HasColumnName("IS_LOCAL_USER");
            builder.Property(u => u.AllowLocalLogin).HasColumnName("ALLOW_LOCAL_LOGIN");

            // Индексы
            builder.HasIndex(u => u.Username).IsUnique();
            builder.HasIndex(u => u.IsActive);
            builder.HasIndex(u => u.IsServiceAccount);
            builder.HasIndex(u => u.AuthenticationType);
            builder.HasIndex(u => u.IsLocalUser);
            builder.HasIndex(u => u.DomainUsername);
            builder.HasIndex(u => u.LastDomainSync);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.DisplayName)
                .HasMaxLength(200);

            builder.Property(u => u.Email)
                .HasMaxLength(300);

            builder.Property(u => u.PasswordHash)
                .HasMaxLength(500);

            builder.Property(u => u.Salt)
                .HasMaxLength(500);

            // ✅ ИСПРАВЛЕНО: Убираем конфликтующие настройки
            // Groups уже настроены в модели как NotMapped свойство
            // которое работает с GroupsJson

            // ✅ ИСПРАВЛЕНО: НЕ игнорируем свойства, которые существуют в модели
            // Эти свойства уже правильно настроены в самой модели User

            // Индексы
            builder.HasIndex(u => u.Username).IsUnique();
            builder.HasIndex(u => u.IsActive);
            builder.HasIndex(u => u.IsServiceAccount);

            builder.ToTable("Users");
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;
using Newtonsoft.Json;

namespace WindowsLauncher.Data.Configurations
{
    public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
    {
        public void Configure(EntityTypeBuilder<UserSettings> builder)
        {
            // ПРОСТОЕ РЕШЕНИЕ: Все имена в UPPERCASE для всех БД
            builder.ToTable("USER_SETTINGS");
            builder.HasKey(s => s.Id);

            // Настройки свойств - UPPERCASE имена колонок
            builder.Property(s => s.Id).HasColumnName("ID");
            builder.Property(s => s.UserId).IsRequired().HasColumnName("USER_ID");
            builder.Property(s => s.Theme).HasMaxLength(50).HasColumnName("THEME");
            builder.Property(s => s.AccentColor).HasMaxLength(50).HasColumnName("ACCENT_COLOR");
            builder.Property(s => s.TileSize).HasColumnName("TILE_SIZE");
            builder.Property(s => s.ShowCategories).HasColumnName("SHOW_CATEGORIES");
            builder.Property(s => s.DefaultCategory).HasMaxLength(100).HasColumnName("DEFAULT_CATEGORY");
            builder.Property(s => s.AutoRefresh).HasColumnName("AUTO_REFRESH");
            builder.Property(s => s.RefreshIntervalMinutes).HasColumnName("REFRESH_INTERVAL_MINUTES");
            builder.Property(s => s.ShowDescriptions).HasColumnName("SHOW_DESCRIPTIONS");
            builder.Property(s => s.LastModified).HasColumnName("LAST_MODIFIED");

            // Сериализуем список скрытых категорий в JSON с компаратором
            builder.Property(s => s.HiddenCategories)
                .HasColumnName("HIDDEN_CATEGORIES")
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Связь с пользователем
            builder.HasOne(s => s.User)
                .WithOne(u => u.UserSettings)
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Индексы
            builder.HasIndex(s => s.UserId).IsUnique();
        }
    }
}
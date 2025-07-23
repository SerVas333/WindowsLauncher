using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;
using Newtonsoft.Json;

namespace WindowsLauncher.Data.Configurations
{
    public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
    {
        private readonly DatabaseType _databaseType;

        public UserSettingsConfiguration(DatabaseType databaseType = DatabaseType.SQLite)
        {
            _databaseType = databaseType;
        }

        public void Configure(EntityTypeBuilder<UserSettings> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.Theme)
                .HasMaxLength(50);

            builder.Property(s => s.AccentColor)
                .HasMaxLength(50);

            builder.Property(s => s.DefaultCategory)
                .HasMaxLength(100);

            // Сериализуем список скрытых категорий в JSON с компаратором
            builder.Property(s => s.HiddenCategories)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.HasIndex(s => s.Username).IsUnique();
            builder.ToTable("UserSettings");
        }
    }
}
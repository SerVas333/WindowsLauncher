using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;
using Newtonsoft.Json;

namespace WindowsLauncher.Data.Configurations
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        private readonly DatabaseType _databaseType;

        public ApplicationConfiguration(DatabaseType databaseType = DatabaseType.SQLite)
        {
            _databaseType = databaseType;
        }

        public void Configure(EntityTypeBuilder<Application> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(a => a.Description)
                .HasMaxLength(500);

            builder.Property(a => a.ExecutablePath)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(a => a.Arguments)
                .HasMaxLength(500);

            builder.Property(a => a.IconPath)
                .HasMaxLength(1000);

            builder.Property(a => a.Category)
                .HasMaxLength(100);

            builder.Property(a => a.CreatedBy)
                .HasMaxLength(100);

            // Сериализуем список требуемых групп в JSON с компаратором
            builder.Property(a => a.RequiredGroups)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            builder.HasIndex(a => a.Name);
            builder.HasIndex(a => a.Category);
            builder.HasIndex(a => a.IsEnabled);

            builder.ToTable("Applications");
        }
    }
}
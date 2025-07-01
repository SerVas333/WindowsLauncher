using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;
using Newtonsoft.Json;

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

            // Сериализуем список групп в JSON с компаратором
            builder.Property(u => u.Groups)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // Игнорируем вычисляемые свойства
            builder.Ignore(u => u.LastLoginAt);
            builder.Ignore(u => u.CreatedAt);

            // Индексы
            builder.HasIndex(u => u.Username).IsUnique();
            builder.HasIndex(u => u.IsActive);
            builder.HasIndex(u => u.IsServiceAccount);

            builder.ToTable("Users");
        }
    }
}
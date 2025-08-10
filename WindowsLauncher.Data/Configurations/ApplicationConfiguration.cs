using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WindowsLauncher.Core.Models;
using Newtonsoft.Json;

namespace WindowsLauncher.Data.Configurations
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public void Configure(EntityTypeBuilder<Application> builder)
        {
            // ПРОСТОЕ РЕШЕНИЕ: Все имена в UPPERCASE для всех БД
            builder.ToTable("APPLICATIONS");
            builder.HasKey(a => a.Id);

            // Настройки свойств - UPPERCASE имена колонок
            builder.Property(a => a.Id).HasColumnName("ID");
            builder.Property(a => a.Name).IsRequired().HasMaxLength(200).HasColumnName("NAME");
            builder.Property(a => a.Description).HasMaxLength(500).HasColumnName("DESCRIPTION");
            builder.Property(a => a.ExecutablePath).IsRequired().HasMaxLength(1000).HasColumnName("EXECUTABLE_PATH");
            builder.Property(a => a.Arguments).HasMaxLength(500).HasColumnName("ARGUMENTS");
            builder.Property(a => a.WorkingDirectory).HasMaxLength(1000).HasColumnName("WORKING_DIRECTORY");
            builder.Property(a => a.IconPath).HasMaxLength(1000).HasColumnName("ICON_PATH");
            builder.Property(a => a.IconText).HasMaxLength(50).HasColumnName("ICONTEXT").HasDefaultValue("📱");
            builder.Property(a => a.Category).HasMaxLength(100).HasColumnName("CATEGORY");
            builder.Property(a => a.Type).HasColumnName("APP_TYPE");
            builder.Property(a => a.MinimumRole).HasColumnName("MINIMUM_ROLE");
            builder.Property(a => a.IsEnabled).HasColumnName("IS_ENABLED");
            builder.Property(a => a.SortOrder).HasColumnName("SORT_ORDER");
            builder.Property(a => a.CreatedDate).HasColumnName("CREATED_DATE");
            builder.Property(a => a.ModifiedDate).HasColumnName("MODIFIED_DATE");
            builder.Property(a => a.CreatedBy).HasMaxLength(100).HasColumnName("CREATED_BY");

            // Сериализуем список требуемых групп в JSON с компаратором
            builder.Property(a => a.RequiredGroups)
                .HasColumnName("REQUIRED_GROUPS")
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            // APK метаданные (только для Android приложений) - маппинг на правильные имена колонок
            builder.Property(a => a.ApkPackageName).HasColumnName("APK_PACKAGE_NAME").HasMaxLength(200);
            builder.Property(a => a.ApkVersionCode).HasColumnName("APK_VERSION_CODE"); 
            builder.Property(a => a.ApkVersionName).HasColumnName("APK_VERSION_NAME").HasMaxLength(50);
            builder.Property(a => a.ApkMinSdk).HasColumnName("APK_MIN_SDK");
            builder.Property(a => a.ApkTargetSdk).HasColumnName("APK_TARGET_SDK");
            builder.Property(a => a.ApkFilePath).HasColumnName("APK_FILE_PATH").HasMaxLength(1000);
            builder.Property(a => a.ApkFileHash).HasColumnName("APK_FILE_HASH").HasMaxLength(64);
            builder.Property(a => a.ApkInstallStatus).HasColumnName("APK_INSTALL_STATUS").HasMaxLength(50).HasDefaultValue("NotInstalled");

            // Индексы
            builder.HasIndex(a => a.Name);  
            builder.HasIndex(a => a.Category);
            builder.HasIndex(a => a.IsEnabled);
            
            // Индексы для Android приложений
            builder.HasIndex(a => a.ApkPackageName);
            builder.HasIndex(a => a.ApkFileHash);
        }
    }
}
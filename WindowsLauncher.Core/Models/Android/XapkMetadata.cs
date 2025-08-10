using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WindowsLauncher.Core.Models.Android
{
    /// <summary>
    /// Метаданные XAPK файла (manifest.json внутри XAPK архива)
    /// </summary>
    public class XapkMetadata
    {
        [JsonPropertyName("package_name")]
        public string? PackageName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version_code")]
        public string? VersionCode { get; set; }

        [JsonPropertyName("version_name")]
        public string? VersionName { get; set; }

        [JsonPropertyName("min_sdk_version")]
        public string? MinSdkVersion { get; set; }

        [JsonPropertyName("target_sdk_version")]
        public string? TargetSdkVersion { get; set; }

        [JsonPropertyName("permissions")]
        public List<string>? Permissions { get; set; }

        [JsonPropertyName("split_apks")]
        public List<XapkSplitApk>? SplitApks { get; set; }

        [JsonPropertyName("expansions")]
        public List<XapkExpansion>? Expansions { get; set; }

        /// <summary>
        /// Проверяет, являются ли метаданные валидными
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(PackageName) && 
                   !string.IsNullOrWhiteSpace(Name);
        }

        /// <summary>
        /// Конвертирует XAPK метаданные в стандартные APK метаданные
        /// </summary>
        public ApkMetadata ToApkMetadata()
        {
            return new ApkMetadata
            {
                PackageName = PackageName ?? "",
                AppName = Name ?? "",
                VersionName = VersionName ?? "",
                VersionCode = int.TryParse(VersionCode, out int versionCodeInt) ? versionCodeInt : 0,
                MinSdkVersion = int.TryParse(MinSdkVersion, out int minSdkInt) ? minSdkInt : 0,
                TargetSdkVersion = int.TryParse(TargetSdkVersion, out int targetSdkInt) ? targetSdkInt : 0,
                FileSizeBytes = 0 // Будет установлен позже
            };
        }
    }

    /// <summary>
    /// Split APK файл в XAPK пакете
    /// </summary>
    public class XapkSplitApk
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    /// <summary>
    /// Expansion (OBB) файл в XAPK
    /// </summary>
    public class XapkExpansion
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("install_location")]
        public string? InstallLocation { get; set; }

        [JsonPropertyName("install_path")]
        public string? InstallPath { get; set; }
    }
}
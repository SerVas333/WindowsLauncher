using System.Reflection;
using WindowsLauncher.Core.Services;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Реализация сервиса версионирования
    /// </summary>
    public class VersionService : IVersionService
    {
        private readonly Assembly _assembly;
        
        public VersionService()
        {
            _assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        }
        
        public Version GetCurrentVersion()
        {
            return _assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        }
        
        public string GetVersionString()
        {
            var version = GetCurrentVersion();
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        
        public ApplicationVersionInfo GetVersionInfo()
        {
            var version = GetCurrentVersion();
            
            return new ApplicationVersionInfo
            {
                Version = version,
                Title = GetAssemblyAttribute<AssemblyTitleAttribute>()?.Title ?? "Windows Launcher",
                Description = GetAssemblyAttribute<AssemblyDescriptionAttribute>()?.Description ?? "",
                Company = GetAssemblyAttribute<AssemblyCompanyAttribute>()?.Company ?? "",
                Product = GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? "",
                Copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "",
                Configuration = GetAssemblyAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "",
                BuildDate = GetBuildDate()
            };
        }
        
        public int CompareVersion(Version version)
        {
            return GetCurrentVersion().CompareTo(version);
        }
        
        public bool RequiresDatabaseUpdate(string currentDbVersion)
        {
            if (string.IsNullOrEmpty(currentDbVersion))
                return true;
                
            if (!Version.TryParse(currentDbVersion, out var dbVersion))
                return true;
                
            var appVersion = GetCurrentVersion();
            
            // Проверяем изменение мажорной или минорной версии
            return appVersion.Major > dbVersion.Major || 
                   (appVersion.Major == dbVersion.Major && appVersion.Minor > dbVersion.Minor);
        }
        
        private T? GetAssemblyAttribute<T>() where T : Attribute
        {
            return _assembly.GetCustomAttribute<T>();
        }
        
        private DateTime GetBuildDate()
        {
            // Для .NET 8+ используем время сборки из метаданных
            var location = _assembly.Location;
            if (System.IO.File.Exists(location))
            {
                return System.IO.File.GetCreationTime(location);
            }
            
            return DateTime.Now;
        }
    }
}
// WindowsLauncher.UI/Properties/Settings.cs
namespace WindowsLauncher.UI.Properties
{
    /// <summary>
    /// Заглушка для настроек приложения
    /// В будущем можно заменить на полноценную систему настроек
    /// </summary>
    public sealed class Settings
    {
        private static Settings _default;

        public static Settings Default => _default ??= new Settings();

        public string LastDomain { get; set; } = "";
        public string LastUsername { get; set; } = "";
        public string LastLoginMode { get; set; } = "Domain";

        public void Save()
        {
            // TODO: Реализовать сохранение настроек в реестр или файл
            // Пока используем заглушку
        }
    }
}
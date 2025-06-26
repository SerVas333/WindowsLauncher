using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// WindowsLauncher.Core/Models/UserSettings.cs
namespace WindowsLauncher.Core.Models
{
    public class UserSettings
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;

        // Настройки темы
        public string Theme { get; set; } = "Light"; // Light, Dark
        public string AccentColor { get; set; } = "Blue"; // Blue, Red, Green, Orange

        // Настройки интерфейса
        public int TileSize { get; set; } = 150; // Размер плиток приложений
        public bool ShowCategories { get; set; } = true;
        public string DefaultCategory { get; set; } = "All";
        public List<string> HiddenCategories { get; set; } = new();

        // Настройки поведения
        public bool AutoRefresh { get; set; } = true;
        public int RefreshIntervalMinutes { get; set; } = 30;
        public bool ShowDescriptions { get; set; } = true;

        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}
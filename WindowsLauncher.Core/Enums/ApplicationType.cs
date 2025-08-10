using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// WindowsLauncher.Core/Enums/ApplicationType.cs
namespace WindowsLauncher.Core.Enums
{
    public enum ApplicationType
    {
        Desktop = 1,    // .exe приложения Windows
        Web = 2,        // URL ссылки (откроются в браузере)
        Folder = 3,     // Папки в проводнике
        ChromeApp = 4,  // Chrome приложения в режиме --app (управление через title)
        Android = 5     // Android APK приложения через Windows Subsystem for Android
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// WindowsLauncher.Core/Enums/UserRole.cs
namespace WindowsLauncher.Core.Enums
{
    public enum UserRole
    {
        Guest = 0,          // Гостевой пользователь - ограниченный доступ
        Standard = 1,       // Обычный пользователь - базовые приложения
        PowerUser = 2,      // Расширенные права - больше приложений
        Administrator = 3   // Полные права - все приложения + настройки
    }
}
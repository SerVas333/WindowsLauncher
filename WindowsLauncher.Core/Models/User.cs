using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// WindowsLauncher.Core/Models/User.cs
using WindowsLauncher.Core.Enums;

namespace WindowsLauncher.Core.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public UserRole Role { get; set; } = UserRole.Standard;
        public DateTime LastLogin { get; set; }
        public bool IsActive { get; set; } = true;

        // Метод для проверки принадлежности к группе
        public bool IsInGroup(string groupName)
        {
            return Groups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        // Метод для проверки минимальной роли
        public bool HasMinimumRole(UserRole minRole)
        {
            return Role >= minRole;
        }
    }
}
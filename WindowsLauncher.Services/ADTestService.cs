using System;
using System.DirectoryServices.AccountManagement;

namespace WindowsLauncher.Services
{
    public class ADTestService
    {
        public string GetCurrentUser()
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Domain);
                var user = UserPrincipal.Current;
                return $"User: {user?.DisplayName} ({user?.SamAccountName})";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}

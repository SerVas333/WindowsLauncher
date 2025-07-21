// WindowsLauncher.Services/UpdateGuestUserRole.cs - Утилита для обновления роли пользователя guest
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Enums;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Утилита для обновления роли существующего пользователя guest на UserRole.Guest
    /// </summary>
    public static class UpdateGuestUserRole
    {
        /// <summary>
        /// Метод для вызова из приложения при первом запуске
        /// </summary>
        public static async Task UpdateGuestUserIfNeededAsync(LauncherDbContext context)
        {
            try
            {
                var guestUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == "guest");

                if (guestUser != null && guestUser.Role != UserRole.Guest)
                {
                    System.Diagnostics.Debug.WriteLine($"Обновляем роль пользователя guest с {guestUser.Role} на Guest");
                    
                    guestUser.Role = UserRole.Guest;
                    guestUser.AuthenticationType = AuthenticationType.Guest;
                    
                    await context.SaveChangesAsync();
                    
                    System.Diagnostics.Debug.WriteLine("Роль пользователя guest успешно обновлена");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении роли пользователя guest: {ex.Message}");
            }
        }
    }
}
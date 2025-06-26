// WindowsLauncher.Data/Repositories/UserRepository.cs
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(LauncherDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User> UpsertUserAsync(User user)
        {
            var existingUser = await GetByUsernameAsync(user.Username);

            if (existingUser == null)
            {
                await AddAsync(user);
            }
            else
            {
                // Обновляем существующего пользователя
                existingUser.DisplayName = user.DisplayName;
                existingUser.Email = user.Email;
                existingUser.Groups = user.Groups;
                existingUser.Role = user.Role;
                existingUser.LastLogin = user.LastLogin;
                existingUser.IsActive = user.IsActive;

                await UpdateAsync(existingUser);
                user = existingUser;
            }

            await SaveChangesAsync();
            return user;
        }

        public async Task UpdateLastLoginAsync(string username)
        {
            var user = await GetByUsernameAsync(username);
            if (user != null)
            {
                user.LastLogin = DateTime.Now;
                await UpdateAsync(user);
                await SaveChangesAsync();
            }
        }
    }
}

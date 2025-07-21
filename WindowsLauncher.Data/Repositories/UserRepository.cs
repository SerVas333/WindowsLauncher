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

        #region Методы для гибридной авторизации

        /// <summary>
        /// Получить пользователей определенного типа аутентификации
        /// </summary>
        public async Task<List<User>> GetByAuthenticationTypeAsync(AuthenticationType authType)
        {
            return await _dbSet.Where(u => u.AuthenticationType == authType).ToListAsync();
        }

        /// <summary>
        /// Получить локальных пользователей (включая сервисных и гостевых)
        /// </summary>
        public async Task<List<User>> GetLocalUsersAsync()
        {
            return await _dbSet.Where(u => (u.IsLocalUser && 
                                           (u.AuthenticationType == AuthenticationType.LocalUsers || 
                                            u.AuthenticationType == AuthenticationType.LocalService)) ||
                                           u.AuthenticationType == AuthenticationType.Guest)
                              .OrderBy(u => u.Username)
                              .ToListAsync();
        }

        /// <summary>
        /// Получить кэшированных доменных пользователей
        /// </summary>
        public async Task<List<User>> GetCachedDomainUsersAsync()
        {
            return await _dbSet.Where(u => u.AuthenticationType == AuthenticationType.CachedDomain)
                              .OrderBy(u => u.LastDomainSync)
                              .ToListAsync();
        }

        /// <summary>
        /// Найти пользователя по доменному имени
        /// </summary>
        public async Task<User?> GetByDomainUsernameAsync(string domainUsername)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.DomainUsername == domainUsername);
        }

        /// <summary>
        /// Получить пользователей, которым нужна синхронизация с доменом
        /// </summary>
        public async Task<List<User>> GetUsersRequiringSyncAsync(TimeSpan maxAge)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            return await _dbSet.Where(u => u.AuthenticationType == AuthenticationType.CachedDomain &&
                                          (u.LastDomainSync == null || u.LastDomainSync < cutoffDate))
                              .ToListAsync();
        }

        /// <summary>
        /// Обновить время синхронизации с доменом
        /// </summary>
        public async Task UpdateDomainSyncAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                user.UpdateDomainSync();
                await UpdateAsync(user);
                await SaveChangesAsync();
            }
        }

        /// <summary>
        /// Создать или обновить кэшированного доменного пользователя
        /// </summary>
        public async Task<User> UpsertCachedDomainUserAsync(User domainUser, string domainUsername)
        {
            var existingUser = await GetByDomainUsernameAsync(domainUsername);

            if (existingUser == null)
            {
                // Создаем нового кэшированного пользователя
                domainUser.AuthenticationType = AuthenticationType.CachedDomain;
                domainUser.DomainUsername = domainUsername;
                domainUser.IsLocalUser = false;
                domainUser.AllowLocalLogin = true;
                domainUser.UpdateDomainSync();
                
                await AddAsync(domainUser);
            }
            else
            {
                // Обновляем существующего
                existingUser.DisplayName = domainUser.DisplayName;
                existingUser.Email = domainUser.Email;
                existingUser.Groups = domainUser.Groups;
                existingUser.Role = domainUser.Role;
                existingUser.IsActive = domainUser.IsActive;
                existingUser.UpdateDomainSync();

                await UpdateAsync(existingUser);
                domainUser = existingUser;
            }

            await SaveChangesAsync();
            return domainUser;
        }

        #endregion
    }
}

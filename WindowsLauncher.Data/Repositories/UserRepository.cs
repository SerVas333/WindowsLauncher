// WindowsLauncher.Data/Repositories/UserRepository.cs - ВЕРСИЯ С BaseRepositoryWithFactory
using Microsoft.EntityFrameworkCore;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;

namespace WindowsLauncher.Data.Repositories
{
    public class UserRepository : BaseRepositoryWithFactory<User>, IUserRepository
    {
        public UserRepository(IDbContextFactory<LauncherDbContext> contextFactory) : base(contextFactory)
        {
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await ExecuteWithContextAsync(async context =>
                await context.Users.FirstOrDefaultAsync(u => u.Username == username));
        }

        public async Task<User> UpsertUserAsync(User user)
        {
            return await ExecuteWithContextAsync(async context =>
            {
                var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUser == null)
                {
                    context.Users.Add(user);
                    await context.SaveChangesAsync();
                    return user;
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

                    context.Users.Update(existingUser);
                    await context.SaveChangesAsync();
                    return existingUser;
                }
            });
        }

        public async Task UpdateLastLoginAsync(string username)
        {
            await ExecuteWithContextAsync(async context =>
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user != null)
                {
                    user.LastLogin = DateTime.Now;
                    context.Users.Update(user);
                    await context.SaveChangesAsync();
                }
            });
        }

        #region Методы для гибридной авторизации

        /// <summary>
        /// Получить пользователей определенного типа аутентификации
        /// </summary>
        public async Task<List<User>> GetByAuthenticationTypeAsync(AuthenticationType authType)
        {
            return await ExecuteWithContextAsync(async context =>
                await context.Users.Where(u => u.AuthenticationType == authType).ToListAsync());
        }

        /// <summary>
        /// Получить локальных пользователей (включая сервисных и гостевых)
        /// </summary>
        public async Task<List<User>> GetLocalUsersAsync()
        {
            return await ExecuteWithContextAsync(async context =>
                await context.Users.Where(u => (u.IsLocalUser && 
                                               (u.AuthenticationType == AuthenticationType.LocalUsers || 
                                                u.AuthenticationType == AuthenticationType.LocalService)) ||
                                               u.AuthenticationType == AuthenticationType.Guest)
                                  .OrderBy(u => u.Username)
                                  .ToListAsync());
        }

        /// <summary>
        /// Получить кэшированных доменных пользователей
        /// </summary>
        public async Task<List<User>> GetCachedDomainUsersAsync()
        {
            return await ExecuteWithContextAsync(async context =>
                await context.Users.Where(u => u.AuthenticationType == AuthenticationType.CachedDomain)
                                  .OrderBy(u => u.LastDomainSync)
                                  .ToListAsync());
        }

        /// <summary>
        /// Найти пользователя по доменному имени
        /// </summary>
        public async Task<User?> GetByDomainUsernameAsync(string domainUsername)
        {
            return await ExecuteWithContextAsync(async context =>
                await context.Users.FirstOrDefaultAsync(u => u.DomainUsername == domainUsername));
        }

        /// <summary>
        /// Получить пользователей, которым нужна синхронизация с доменом
        /// </summary>
        public async Task<List<User>> GetUsersRequiringSyncAsync(TimeSpan maxAge)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            return await ExecuteWithContextAsync(async context =>
                await context.Users.Where(u => u.AuthenticationType == AuthenticationType.CachedDomain &&
                                              (u.LastDomainSync == null || u.LastDomainSync < cutoffDate))
                                  .ToListAsync());
        }

        /// <summary>
        /// Обновить время синхронизации с доменом
        /// </summary>
        public async Task UpdateDomainSyncAsync(int userId)
        {
            await ExecuteWithContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.UpdateDomainSync();
                    context.Users.Update(user);
                    await context.SaveChangesAsync();
                }
            });
        }

        /// <summary>
        /// Создать или обновить кэшированного доменного пользователя
        /// </summary>
        public async Task<User> UpsertCachedDomainUserAsync(User domainUser, string domainUsername)
        {
            return await ExecuteWithContextAsync(async context =>
            {
                var existingUser = await context.Users.FirstOrDefaultAsync(u => u.DomainUsername == domainUsername);

                if (existingUser == null)
                {
                    // Создаем нового кэшированного пользователя
                    domainUser.AuthenticationType = AuthenticationType.CachedDomain;
                    domainUser.DomainUsername = domainUsername;
                    domainUser.IsLocalUser = false;
                    domainUser.AllowLocalLogin = true;
                    domainUser.UpdateDomainSync();
                    
                    context.Users.Add(domainUser);
                    await context.SaveChangesAsync();
                    return domainUser;
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

                    context.Users.Update(existingUser);
                    await context.SaveChangesAsync();
                    return existingUser;
                }
            });
        }

        #endregion
    }
}

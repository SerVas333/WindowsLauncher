using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Data.Repositories
{
    /// <summary>
    /// Базовый репозиторий с использованием IDbContextFactory для Singleton сервисов
    /// Каждая операция создает свой короткоживущий контекст для потокобезопасности
    /// </summary>
    public abstract class BaseRepositoryWithFactory<T> : IRepository<T> where T : class
    {
        protected readonly IDbContextFactory<LauncherDbContext> _contextFactory;

        protected BaseRepositoryWithFactory(IDbContextFactory<LauncherDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().FindAsync(id);
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Set<T>().Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Set<T>().Update(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.Set<T>().FindAsync(id);
            if (entity != null)
            {
                context.Set<T>().Remove(entity);
                await context.SaveChangesAsync();
            }
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.Set<T>().FindAsync(id);
            return entity != null;
        }

        public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<int> CountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().CountAsync();
        }

        public virtual async Task SaveChangesAsync()
        {
            // В паттерне DbContextFactory каждая операция уже сохраняет изменения в своем контексте
            // Этот метод остается пустым для совместимости с интерфейсом IRepository<T>
            await Task.CompletedTask;
        }

        /// <summary>
        /// Вспомогательный метод для выполнения операций с контекстом
        /// Используется в наследующих классах для специфичных операций
        /// </summary>
        protected async Task<TResult> ExecuteWithContextAsync<TResult>(Func<LauncherDbContext, Task<TResult>> operation)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await operation(context);
        }

        /// <summary>
        /// Вспомогательный метод для выполнения операций с контекстом без возвращаемого значения
        /// </summary>
        protected async Task ExecuteWithContextAsync(Func<LauncherDbContext, Task> operation)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await operation(context);
        }
    }
}
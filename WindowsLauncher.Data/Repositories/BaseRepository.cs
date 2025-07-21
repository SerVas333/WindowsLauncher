// WindowsLauncher.Data/Repositories/BaseRepository.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WindowsLauncher.Core.Interfaces;

namespace WindowsLauncher.Data.Repositories
{
    public class BaseRepository<T> : IRepository<T> where T : class
    {
        protected readonly LauncherDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(LauncherDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            // Получаем ID сущности через рефлексию (предполагаем, что есть свойство Id)
            var entityType = typeof(T);
            var idProperty = entityType.GetProperty("Id");
            
            if (idProperty != null)
            {
                var entityId = idProperty.GetValue(entity);
                
                if (entityId != null && !entityId.Equals(0))
                {
                    // Ищем уже отслеживаемую сущность с таким же ID
                    var trackedEntity = _context.ChangeTracker.Entries<T>()
                        .FirstOrDefault(e => idProperty.GetValue(e.Entity)?.Equals(entityId) == true);
                    
                    if (trackedEntity != null && !ReferenceEquals(trackedEntity.Entity, entity))
                    {
                        // Отключаем старую сущность от отслеживания
                        trackedEntity.State = EntityState.Detached;
                    }
                }
            }
            
            // Теперь безопасно обновляем сущность
            _dbSet.Update(entity);
            return entity;
        }

        public virtual async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await _dbSet.FindAsync(id) != null;
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public virtual async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

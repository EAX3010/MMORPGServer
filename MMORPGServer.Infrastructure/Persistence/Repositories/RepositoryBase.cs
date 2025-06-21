using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces.Repositories;
using MMORPGServer.Domain.Common.Interfaces;
using System.Linq.Expressions;

namespace MMORPGServer.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Base repository implementation providing common CRUD operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : BaseEntity
        where TKey : notnull
    {
        protected readonly GameDbContext Context;
        protected readonly DbSet<TEntity> DbSet;
        protected readonly ILogger Logger;

        protected RepositoryBase(GameDbContext context, ILogger logger)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            DbSet = context.Set<TEntity>();
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // === Query Operations ===

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            return await DbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = DbSet;

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id").Equals(id));
        }

        public virtual async Task<TEntity?> GetFirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual async Task<IReadOnlyList<TEntity>> GetAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = DbSet;

            // Apply includes
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            // Apply filter
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            // Apply ordering
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            return await query.ToListAsync();
        }

        public virtual async Task<IReadOnlyList<TEntity>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = DbSet;

            // Apply includes
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            // Apply filter
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            // Apply ordering (required for paging)
            if (orderBy != null)
            {
                query = orderBy(query);
            }
            else
            {
                // Default ordering by Id if none specified
                query = query.OrderBy(e => EF.Property<TKey>(e, "Id"));
            }

            // Apply paging
            var skip = (pageNumber - 1) * pageSize;
            return await query.Skip(skip).Take(pageSize).ToListAsync();
        }

        public virtual async Task<int> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            return predicate == null
                ? await DbSet.CountAsync(cancellationToken)
                : await DbSet.CountAsync(predicate, cancellationToken);
        }

        public virtual async Task<bool> ExistsAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(predicate, cancellationToken);
        }

        // === Command Operations ===

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var result = await DbSet.AddAsync(entity, cancellationToken);
            return result.Entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await DbSet.AddRangeAsync(entities, cancellationToken);
        }

        public virtual void Update(TEntity entity)
        {
            DbSet.Update(entity);
        }

        public virtual void UpdateRange(IEnumerable<TEntity> entities)
        {
            DbSet.UpdateRange(entities);
        }

        public virtual void Remove(TEntity entity)
        {
            DbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<TEntity> entities)
        {
            DbSet.RemoveRange(entities);
        }

        public virtual async Task<bool> RemoveByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            Remove(entity);
            return true;
        }
    }
}
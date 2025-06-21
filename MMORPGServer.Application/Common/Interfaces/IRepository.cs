using MMORPGServer.Domain.Common.Interfaces;
using System.Linq.Expressions;

namespace MMORPGServer.Application.Common.Interfaces.Repositories
{
    /// <summary>
    /// Base repository interface providing common CRUD operations.
    /// All entity repositories should inherit from this interface.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IRepository<TEntity, TKey>
        where TEntity : BaseEntity
        where TKey : notnull
    {
        // === Query Operations ===

        /// <summary>
        /// Gets an entity by its primary key.
        /// </summary>
        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an entity by its primary key with specified includes.
        /// </summary>
        Task<TEntity?> GetByIdAsync(TKey id, params Expression<Func<TEntity, object>>[] includes);

        /// <summary>
        /// Gets the first entity matching the predicate.
        /// </summary>
        Task<TEntity?> GetFirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all entities matching the predicate.
        /// </summary>
        Task<IReadOnlyList<TEntity>> GetAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            params Expression<Func<TEntity, object>>[] includes);

        /// <summary>
        /// Gets paged results matching the predicate.
        /// </summary>
        Task<IReadOnlyList<TEntity>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            params Expression<Func<TEntity, object>>[] includes);

        /// <summary>
        /// Counts entities matching the predicate.
        /// </summary>
        Task<int> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity matches the predicate.
        /// </summary>
        Task<bool> ExistsAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        // === Command Operations ===

        /// <summary>
        /// Adds a new entity to the repository.
        /// </summary>
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple entities to the repository.
        /// </summary>
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity.
        /// </summary>
        void Update(TEntity entity);

        /// <summary>
        /// Updates multiple entities.
        /// </summary>
        void UpdateRange(IEnumerable<TEntity> entities);

        /// <summary>
        /// Removes an entity from the repository.
        /// </summary>
        void Remove(TEntity entity);

        /// <summary>
        /// Removes multiple entities from the repository.
        /// </summary>
        void RemoveRange(IEnumerable<TEntity> entities);

        /// <summary>
        /// Removes an entity by its primary key.
        /// </summary>
        Task<bool> RemoveByIdAsync(TKey id, CancellationToken cancellationToken = default);
    }
}
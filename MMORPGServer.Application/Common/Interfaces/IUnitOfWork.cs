using MMORPGServer.Application.Common.Interfaces.Repositories;

namespace MMORPGServer.Application.Common.Interfaces
{
    /// <summary>
    /// Unit of Work pattern interface.
    /// Manages database transactions and coordinates multiple repository operations.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Player repository for managing player data.
        /// </summary>
        IPlayerRepository Players { get; }

        // TODO: Add other repositories as needed
        // IInventoryRepository Inventories { get; }
        // IItemRepository Items { get; }
        // IGuildRepository Guilds { get; }
        // IMapRepository Maps { get; }

        /// <summary>
        /// Saves all changes made in this unit of work to the database.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        Task RollbackTransactionAsync();

        /// <summary>
        /// Executes multiple operations within a transaction scope.
        /// Automatically commits on success or rolls back on failure.
        /// </summary>
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes multiple operations within a transaction scope.
        /// Automatically commits on success or rolls back on failure.
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    }
}

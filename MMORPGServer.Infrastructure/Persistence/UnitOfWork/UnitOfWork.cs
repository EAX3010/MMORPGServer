
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces;
using MMORPGServer.Application.Common.Interfaces.Repositories;
using MMORPGServer.Infrastructure.Persistence.Repositories;
namespace MMORPGServer.Infrastructure.Persistence.UnitOfWork
{
    /// <summary>
    /// Unit of Work implementation managing database transactions and repositories.
    /// Provides a single point of control for database operations.
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly GameDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly ILoggerFactory _factory;
        private IDbContextTransaction? _transaction;
        private bool _disposed;

        // Lazy-loaded repositories
        private IPlayerRepository? _players;

        public UnitOfWork(GameDbContext context, ILogger<UnitOfWork> logger, ILoggerFactory factory)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        // === Repository Properties ===

        public IPlayerRepository Players =>
            _players ??= new PlayerRepository(_context, _factory.CreateLogger<PlayerRepository>());

        // TODO: Add other repositories as needed
        // public IInventoryRepository Inventories => 
        //     _inventories ??= new InventoryRepository(_context, _logger.CreateLogger<InventoryRepository>());

        // === Transaction Management ===

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Saved {Count} changes to database", result);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred while saving changes");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error occurred while saving changes");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while saving changes");
                throw;
            }
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }

            _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            _logger.LogDebug("Database transaction started");
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction in progress");
            }

            try
            {
                await _transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Database transaction committed successfully");
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null)
            {
                return;
            }

            try
            {
                await _transaction.RollbackAsync();
                _logger.LogDebug("Database transaction rolled back");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while rolling back transaction");
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (_transaction != null)
            {
                // Already in transaction, just execute the operation
                return await operation();
            }

            await BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await SaveChangesAsync(cancellationToken);
                await CommitTransactionAsync(cancellationToken);
                return result;
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            }, cancellationToken);
        }

        // === Disposal ===

        public void Dispose()
        {
            if (!_disposed)
            {
                _transaction?.Dispose();
                _disposed = true;
            }
        }
    }
}

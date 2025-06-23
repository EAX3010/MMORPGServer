namespace MMORPGServer.Infrastructure.Persistence.Common.Interfaces
{
    /// <summary>
    /// Interface for database initialization operations.
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Initializes the database, applying migrations if needed.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds initial data into the database.
        /// </summary>
        Task SeedDataAsync(CancellationToken cancellationToken = default);
    }
}
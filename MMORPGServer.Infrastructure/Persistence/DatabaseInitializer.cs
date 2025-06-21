using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Persistence;
using MMORPGServer.Infrastructure.Persistence.Common;

namespace MMORPGServer.Infrastructure.Persistence
{

    /// <summary>
    /// Handles database initialization, migration, and seeding operations.
    /// </summary>
    public sealed class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializer> _logger;
        private readonly IHostEnvironment _environment;

        public DatabaseInitializer(
            IServiceProvider serviceProvider,
            ILogger<DatabaseInitializer> logger,
            IHostEnvironment environment)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Initializes the database based on the current environment.
        /// Development: Recreates database
        /// Production: Applies pending migrations
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Create a new scope for database operations
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

                _logger.LogInformation("Initializing database...");


                // Production environment: Use migrations
                _logger.LogInformation("Checking for pending migrations...");

                // Get list of pending migrations
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count());

                    // Log each pending migration
                    foreach (var migration in pendingMigrations)
                    {
                        _logger.LogInformation("Pending migration: {Migration}", migration);
                    }

                    // Apply all pending migrations
                    await context.Database.MigrateAsync(cancellationToken);

                    _logger.LogInformation("All migrations applied successfully");
                }
                else
                {
                    _logger.LogInformation("Database is up to date. No pending migrations.");
                }

                // Seed initial data
                await SeedDataAsync(cancellationToken);

                _logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Seeds initial data into the database if it's empty.
        /// Creates admin player and test players for development.
        /// </summary>
        public async Task SeedDataAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            // Check if data already exists (including soft-deleted records)
            if (await context.Players.IgnoreQueryFilters().AnyAsync(cancellationToken))
            {
                _logger.LogDebug("Database already contains player data. Skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding initial database data...");

            // Create admin player with special privileges
            var adminPlayer = PlayerEntity.Create(100000, "Admin", 140, 0, 1002, 300, 300, 20000);

            // Add test players for development environment
            var testPlayers = new[]
            {
                  PlayerEntity.Create(1000000, "Admin", 140, 0, 1002, 300, 300, 20000),
                  PlayerEntity.Create(1000001, "محمد خالد", 140, 0, 1002, 300, 300, 20000),
                };

            context.Players.AddRange(testPlayers);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Database seeded with admin and {Count} test players", testPlayers.Length);

        }
    }
}
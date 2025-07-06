using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMORPGServer.Infrastructure.Database.Interceptors;
using Serilog;

namespace MMORPGServer.Infrastructure.Database
{
    public static class DatabaseManager
    {
        private static GameDbContext? _dbContext;
        private static AuditableEntitySaveChangesInterceptor? _auditInterceptor;

        public static GameDbContext DbContext => _dbContext ??
            throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");

        public static async Task InitializeAsync()
        {
            Log.Information("Initializing database...");

            try
            {
                // Create interceptors
                _auditInterceptor = new AuditableEntitySaveChangesInterceptor();

                // Get connection string
                var connectionString = GameServerConfig.GetConnectionString();
                Log.Information("Database connection: {Server}",
                    connectionString.Split(';').FirstOrDefault(x => x.StartsWith("Server="))?.Split('=')[1] ?? "Unknown");

                // Create DbContext with options
                var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
                ConfigureDbContext(optionsBuilder, connectionString);

                _dbContext = new GameDbContext(optionsBuilder.Options);

                // Test database connection
                Log.Information("Testing database connection...");
                _ = await _dbContext.Database.CanConnectAsync();
                Log.Information("Database connection successful");

                // Check if database exists, create if it doesn't
                var databaseExists = await _dbContext.Database.CanConnectAsync();
                if (!databaseExists)
                {
                    Log.Information("Database does not exist, creating...");
                    await _dbContext.Database.EnsureCreatedAsync();
                    Log.Information("Database created successfully");
                }

                // Apply any pending migrations
                var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    Log.Information("Applying {Count} pending migrations...", pendingMigrations.Count());
                    await _dbContext.Database.MigrateAsync();
                    Log.Information("Migrations applied successfully");
                }

                Log.Information("Database initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize database");
                throw;
            }
        }

        public static GameDbContext CreateDbContext()
        {
            var connectionString = GameServerConfig.GetConnectionString();
            var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
            ConfigureDbContext(optionsBuilder, connectionString);
            return new GameDbContext(optionsBuilder.Options);
        }

        private static void ConfigureDbContext(DbContextOptionsBuilder<GameDbContext> options, string connectionString)
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
            {
                // MySQL-specific configuration
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);

                mysqlOptions.CommandTimeout(30);

            });
            if (_auditInterceptor != null)
            {
                options.AddInterceptors(_auditInterceptor);
            }

            options.UseLoggerFactory(LoggerFactory.Create(builder =>
            {
                builder.AddSerilog();
            }));

#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        }

        public static async Task DisposeAsync()
        {
            Log.Information("Disposing database...");

            if (_dbContext != null)
            {
                await _dbContext.DisposeAsync();
                _dbContext = null;
            }

            _auditInterceptor = null;
            Log.Information("Database disposed");
        }
    }
}
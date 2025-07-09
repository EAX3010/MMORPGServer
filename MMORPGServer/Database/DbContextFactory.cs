using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MMORPGServer.Database.Interceptors;
using Serilog;

namespace MMORPGServer.Database
{
    public class DbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args)
        {
            GameServerConfig.Initialize();
            return CreateDbContext([new AuditableEntitySaveChangesInterceptor()]);
        }
        private static GameDbContext? _dbContext;

        public static GameDbContext DbContext => _dbContext ??
            throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");

        public static async Task InitializeAsync()
        {
            Log.Information("Initializing database...");

            try
            {

                _dbContext = CreateDbContext([new AuditableEntitySaveChangesInterceptor()]);

                // Test database connection
                Log.Information("Testing database connection...");
                _ = await _dbContext.Database.CanConnectAsync();
                Log.Information("Database connection successful");

                // Check if database exists, create if it doesn't
                var databaseExists = await _dbContext.Database.CanConnectAsync();
                if (!databaseExists)
                {
                    Log.Information("Database does not exist, creating...");
                    _ = await _dbContext.Database.EnsureCreatedAsync();
                    Log.Information("Database created successfully");
                }

                // Apply any pending migrations
                IEnumerable<string> pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
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

        public static GameDbContext CreateDbContext(IInterceptor[] interceptor)
        {
            var connectionString = GameServerConfig.GetConnectionString();
            var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
            ConfigureDbContext(optionsBuilder, connectionString, interceptor);
            return new GameDbContext(optionsBuilder.Options);
        }

        private static void ConfigureDbContext(DbContextOptionsBuilder<GameDbContext> options, string connectionString, IInterceptor[] interceptor)
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
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
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
            Log.Information("Database disposed");
        }
    }
}
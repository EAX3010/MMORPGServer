using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MMORPGServer.Infrastructure.Database.Interceptors;

namespace MMORPGServer.Infrastructure.Database
{
    /// <summary>
    /// Factory for creating GameDbContext instances during migrations
    /// This is required for EF Core tools (add-migration, update-database, etc.)
    /// </summary>
    public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args)
        {
            // Build configuration the same way as your application
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables("GAMESERVER_")
                .Build();

            // Get connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection string not found. Ensure appsettings.json has ConnectionStrings:DefaultConnection configured.");
            }

            // Create the interceptor (same as in your DatabaseManager)
            var auditInterceptor = new AuditableEntitySaveChangesInterceptor();

            // Configure DbContext options for MySQL
            var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
            {
                // MySQL-specific configuration
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);

                mysqlOptions.CommandTimeout(30);

            });

            // Add interceptors
            optionsBuilder.AddInterceptors(auditInterceptor);


#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif

            return new GameDbContext(optionsBuilder.Options);
        }
    }
}
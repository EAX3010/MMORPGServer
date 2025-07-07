using MMORPGServer.Infrastructure.Database;
using MMORPGServer.Infrastructure.Database.Ini;
using MMORPGServer.Infrastructure.Database.Repositories;
using Serilog;

namespace MMORPGServer.Infrastructure.Repositories
{
    public static class RepositoryManager
    {
        public static MapRepository MapRepository;
        public static SqlPlayerRepository PlayerRepository;

        public async static void Initialize()
        {
            Log.Information("Initializing repositories...");

            var dbContext = DbContextFactory.DbContext;

            PlayerRepository = new SqlPlayerRepository(dbContext);
            MapRepository = new MapRepository();
            await MapRepository.InitializeMapsAsync();


            Log.Information("Repositories initialized successfully");
        }

        public static T CreateRepository<T>() where T : class
        {
            var dbContext = DbContextFactory.CreateDbContext();

            if (typeof(T) == typeof(SqlPlayerRepository))
                return (T)(object)new SqlPlayerRepository(dbContext);

            // Add other repository types as needed
            // if (typeof(T) == typeof(IItemRepository))
            //     return (T)new SqlItemRepository(dbContext);

            throw new InvalidOperationException($"Repository type {typeof(T).Name} is not registered");
        }

    }
}

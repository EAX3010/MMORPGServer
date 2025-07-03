using MMORPGServer.Infrastructure.Database;
using MMORPGServer.Infrastructure.Database.Ini;
using MMORPGServer.Infrastructure.Database.Repositories;
using Serilog;

namespace MMORPGServer.Infrastructure.Repositories
{
    public static class RepositoryManager
    {
        private static SqlPlayerRepository? _playerRepository;
        // Add other repositories as needed
        // private static IItemRepository? _itemRepository;
        // private static IGuildRepository? _guildRepository;
        public static MapRepository MapRepository => MapRepository.Instance;
        public static SqlPlayerRepository PlayerRepository => _playerRepository ??
            throw new InvalidOperationException("Repositories not initialized. Call Initialize() first.");

        public async static void Initialize()
        {
            Log.Information("Initializing repositories...");

            var dbContext = DatabaseManager.DbContext;

            _playerRepository = new SqlPlayerRepository(dbContext);
            await MapRepository.Instance.InitializeMapsAsync();

            Log.Information("Repositories initialized successfully");
        }

        public static T CreateRepository<T>() where T : class
        {
            var dbContext = DatabaseManager.CreateDbContext();

            if (typeof(T) == typeof(SqlPlayerRepository))
                return (T)(object)new SqlPlayerRepository(dbContext);

            // Add other repository types as needed
            // if (typeof(T) == typeof(IItemRepository))
            //     return (T)new SqlItemRepository(dbContext);

            throw new InvalidOperationException($"Repository type {typeof(T).Name} is not registered");
        }

    }
}

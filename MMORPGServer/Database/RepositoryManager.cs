using MMORPGServer.Database.Ini;
using MMORPGServer.Database.Repositories;
using Serilog;

namespace MMORPGServer.Database
{
    public static class RepositoryManager
    {
        public static MapRepository MapRepository;
        public static SqlPlayerRepository PlayerRepository;

        public static async void Initialize()
        {
            Log.Information("Initializing repositories...");

            var dbContext = DbContextFactory.DbContext;

            PlayerRepository = new SqlPlayerRepository(dbContext);
            MapRepository = new MapRepository();
            await MapRepository.InitializeMapsAsync();


            Log.Information("Repositories initialized successfully");
        }
    }
}

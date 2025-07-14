using MMORPGServer.Database.Readers;
using MMORPGServer.Database.Repositories;
using Serilog;

namespace MMORPGServer.Database
{
    public static class RepositoryManager
    {
        public static DMapReader? DMapReader;
        public static SqlPlayerRepository? PlayerRepository;
        public static PointAllotReader? PointAllotReader;
        public static MapDataReader? MapDataReader;
        public static async Task Initialize()
        {
            Log.Information("Initializing repositories...");
            try
            {
                var dbContext = DbContextFactory.DbContext;

                Log.Debug("Initializing SqlPlayerRepository...");
                PlayerRepository = new SqlPlayerRepository(dbContext);

                Log.Debug("Initializing DMapReader...");
                DMapReader = new DMapReader();
                await DMapReader.InitializeDMapsAsync();

                Log.Debug("Initializing PointAllotReader...");
                PointAllotReader = new PointAllotReader(dbContext);
                await PointAllotReader.LoadAllStatsAsync();

                Log.Debug("Initializing MapDataReader...");
                MapDataReader = new MapDataReader(dbContext);
                await MapDataReader.LoadAllMapsAsync();

                Log.Information("Repositories initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize repositories");
                throw;
            }
        }
    }
}

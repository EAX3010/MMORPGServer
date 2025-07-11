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

            var dbContext = DbContextFactory.DbContext;

            PlayerRepository = new SqlPlayerRepository(dbContext);
            DMapReader = new DMapReader();
            PointAllotReader = new PointAllotReader(dbContext);
            MapDataReader = new MapDataReader(dbContext);
            await PointAllotReader.LoadAllStatsAsync();
            await DMapReader.InitializeDMapsAsync();
            await MapDataReader.LoadAllMapsAsync();



            Log.Information("Repositories initialized successfully");
        }
    }
}

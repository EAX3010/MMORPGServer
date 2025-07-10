using MMORPGServer.Database.Readers;
using MMORPGServer.Database.Repositories;
using Serilog;

namespace MMORPGServer.Database
{
    public static class RepositoryManager
    {
        public static DMapReader? DMapReader;
        public static SqlPlayerRepository? PlayerRepository;
        public static CqPointAllotReader? PointAllotReader;
        public static async Task Initialize()
        {
            Log.Information("Initializing repositories...");

            var dbContext = DbContextFactory.DbContext;

            PlayerRepository = new SqlPlayerRepository(dbContext);
            DMapReader = new DMapReader();
            PointAllotReader = new CqPointAllotReader(dbContext);
            await PointAllotReader.LoadAllStatsAsync();
            await DMapReader.InitializeMapsAsync();


            Log.Information("Repositories initialized successfully");
        }
    }
}

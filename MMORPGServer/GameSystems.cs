using Microsoft.Extensions.DependencyInjection;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Persistence.Common.Interfaces;
using Serilog;

namespace MMORPGServer
{
    public class GameSystems
    {
        public static async Task InitializeAsync(IServiceScope scope)
        {
            Log.Information("Initializing database...");
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();

            await initializer.InitializeAsync();

            Log.Information("Initializing game maps...");
            IMapRepository mapRepository = scope.ServiceProvider.GetRequiredService<IMapRepository>();
            await mapRepository.InitializeMapsAsync();
        }
    }
}

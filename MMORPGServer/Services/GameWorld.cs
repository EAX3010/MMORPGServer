using MMORPGServer.Entities;
using Serilog;

namespace MMORPGServer.Services
{


    public class GameWorld
    {
        public Dictionary<int, Player> Maps;
        public GameWorld()
        {
            Log.Information("GameWorld initialized successfully");
        }

        public async Task<Player?> SpawnPlayerAsync(Player player, short mapId)
        {
            try
            {

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to spawn player on map {MapId}", mapId);
                return null;
            }
        }


    }
}
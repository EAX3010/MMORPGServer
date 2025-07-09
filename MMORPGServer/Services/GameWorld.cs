using MMORPGServer.Database;
using MMORPGServer.Entities;
using Serilog;

namespace MMORPGServer.Services
{


    public class GameWorld
    {
        public GameWorld()
        {
            Log.Information("GameWorld initialized successfully");
        }

        public async Task<Player?> SpawnPlayerAsync(Player player, short mapId)
        {
            try
            {
                var map = await RepositoryManager.MapRepository.GetMapAsync(mapId);
                if (map == null)
                {
                    Log.Error("Map {MapId} not found", mapId);
                    return null;
                }

                var spawnPoint = await map.GetValidSpawnPointAsync();
                if (!spawnPoint.HasValue)
                {
                    Log.Error("Could not find valid spawn point on map {MapId}", mapId);
                    return null;
                }

                // Update player position
                player.Position = spawnPoint.Value;
                player.MapId = mapId;
                player.Map = map;

                // Add to map
                if (!map.AddEntity(player))
                {
                    Log.Error("Failed to add player to map {MapId}", mapId);
                    return null;
                }


                Log.Information("Spawned player {Name} on map {MapId} at {Position}",
                    player.Name, mapId, spawnPoint.Value);

                return player;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to spawn player on map {MapId}", mapId);
                return null;
            }
        }


    }
}
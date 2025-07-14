using MMORPGServer.Entities;
using Serilog;

namespace MMORPGServer.Services
{
    public class GameWorld
    {
        public Map TwinCity => MapManager?.GetMap(1002) ?? throw new InvalidOperationException("MapManager not initialized");

        public GameWorld(MapManager mapManager, PlayerManager playerManager)
        {
            MapManager = mapManager;
            PlayerManager = playerManager;
            Log.Information("GameWorld initialized successfully");

        }

        public MapManager MapManager { get; }
        public PlayerManager PlayerManager { get; }

        public async Task<Player?> SpawnPlayerAsync(Player player, int mapId)
        {
            try
            {
                var map = MapManager.GetMap(mapId);
                player.Map = map;
                if (map == null)
                {
                    Log.Warning("Attempted to spawn player {PlayerName} on non-existent map {MapId}", player.Name, mapId);
                    return null;
                }
                if (await map.AddPlayerAsync(player))
                {
                    Log.Information("Player {PlayerName} (ID: {PlayerId}) spawned on map {MapId} at {Position}",
                        player.Name, player.Id, mapId, player.Position);
                    return player;
                }
                else
                {
                    Log.Warning("Failed to add player {PlayerName} to map {MapId}", player.Name, mapId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to spawn player {PlayerName} on map {MapId}", player?.Name ?? "N/A", mapId);
                return null;
            }
        }
    }
}
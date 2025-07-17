using MMORPGServer.Entities;
using Serilog;

namespace MMORPGServer.Services
{
    public class GameWorld
    {
        public static GameWorld Instance;
        public MapManager MapManager { get; }
        public PlayerManager PlayerManager { get; }

        public Map TwinCity;
        public GameWorld()
        {
            MapManager = new MapManager();
            PlayerManager = new PlayerManager();
            TwinCity = MapManager?.GetMap(1002)!;
            Log.Information("GameWorld initialized successfully");

        }
        public async ValueTask<Player?> AddPlayerAsync(Player player, int mapId)
        {
            try
            {
                Map? map = await MapManager.GetMapAsync(mapId);
                if (map == null)
                {
                    Log.Warning("Attempted to spawn player {PlayerName} on non-existent map {MapId}", player.Name, mapId);
                    return null;
                }


                if (await PlayerManager.AddPlayerAsync(player))
                {
                    player.Map = map;
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
                else
                {
                    Log.Warning("Failed to add player {PlayerName} to PlayerManager", player.Name, mapId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to spawn player {PlayerName} on map {MapId}", player?.Name ?? "N/A", mapId);
                return null;
            }
        }
        public async ValueTask<Player?> RemovePlayerAsync(Player player)
        {
            try
            {
                if (await PlayerManager.RemovePlayerAsync(player.Id))
                {
                    if (await player.Map.RemovePlayerAsync(player.Id))
                    {
                        Log.Information("Player {PlayerName} (ID: {PlayerId}) removed from map {MapId}",
                            player.Name, player.Id, player.MapId);
                        return player;
                    }
                    else
                    {
                        Log.Warning("Failed to remove player {PlayerName} from map {MapId}", player.Name, player.MapId);
                        return null;
                    }
                }
                else
                {
                    Log.Warning("Failed to remove player {PlayerName} from PlayerManager", player.Name);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove player {PlayerName} from map {MapId}", player?.Name ?? "N/A", player.MapId);
                return null;
            }
        }

    }
}
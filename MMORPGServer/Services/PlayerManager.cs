using MMORPGServer.Database;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Services
{
    /// <summary>
    /// Memory-based player manager for real-time operations.
    /// Only works with Domain entities.
    /// </summary>
    public class PlayerManager
    {
        public PlayerManager()
        {
            Log.Debug("PlayerManager initialized");
        }
        private readonly ConcurrentDictionary<int, Player> _players = new();
        public async Task<Player?> CreatePlayerAsync(Player player)
        {
            try
            {

                var success = await RepositoryManager.PlayerRepository.UpsertPlayerAsync(player);

                if (success)
                {
                    Log.Information("Created player {Name} (ID: {PlayerId})", player.Name, player.Id);
                    return player;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create player {Name}", player.Name);
                return null;
            }
        }

        public async Task<Player?> LoadPlayerAsync(int playerId, int connectionId)
        {
            try
            {
                // Repository returns Domain Player, handles database mapping internally
                var player = await RepositoryManager.PlayerRepository.GetByIdAsync(playerId);

                if (player != null)
                {
                    player.ConnectionId = connectionId;
                    Log.Information("Loaded player {Name} (ID: {PlayerId})", player.Name, playerId);
                }
                else
                {
                    Log.Warning("Player {PlayerId} not found", playerId);
                }

                return player;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load player {PlayerId}", playerId);
                return null;
            }
        }
        public ValueTask<Player?> GetPlayerAsync(int playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return ValueTask.FromResult(player);
        }

        public ValueTask AddPlayerAsync(Player player)
        {
            if (_players.TryAdd(player.Id, player))
            {
                Log.Debug("Added player {Name} to memory", player.Name);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(int playerId)
        {
            if (_players.TryRemove(playerId, out var player))
            {
                Log.Debug("Removed player {Name} from memory", player.Name);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> GetOnlinePlayerCountAsync()
        {
            return ValueTask.FromResult(_players.Count);
        }

        public ConcurrentDictionary<int, Player> GetPlayers()
        {
            return _players;
        }

        public ValueTask<IEnumerable<Player>> GetPlayersByMapAsync(short mapId)
        {
            var playersOnMap = _players.Values.Where(p => p.MapId == mapId);
            return ValueTask.FromResult(playersOnMap);
        }

        public ValueTask<bool> IsPlayerOnlineAsync(int playerId)
        {
            return ValueTask.FromResult(_players.ContainsKey(playerId));
        }
    }
}
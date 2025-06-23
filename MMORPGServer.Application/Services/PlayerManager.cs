using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Entities;
using System.Collections.Concurrent;

namespace MMORPGServer.Application.Services
{
    /// <summary>
    /// Memory-based player manager for real-time operations.
    /// Only works with Domain entities.
    /// </summary>
    public class PlayerManager
    {
        private readonly ConcurrentDictionary<int, Player> _players = new();
        private readonly ILogger<PlayerManager> _logger;

        public PlayerManager(ILogger<PlayerManager> logger)
        {
            _logger = logger;
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
                _logger.LogDebug("Added player {Name} to memory", player.Name);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(int playerId)
        {
            if (_players.TryRemove(playerId, out var player))
            {
                _logger.LogDebug("Removed player {Name} from memory", player.Name);
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
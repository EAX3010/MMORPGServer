using Microsoft.Extensions.Logging;
using MMORPGServer.Entities;
using MMORPGServer.Infrastructure.Database.Repositories;
using System.Collections.Concurrent;

namespace MMORPGServer.Services
{
    /// <summary>
    /// Memory-based player manager for real-time operations.
    /// Only works with Domain entities.
    /// </summary>
    public class PlayerManager
    {
        private readonly ConcurrentDictionary<int, Player> _players = new();
        private readonly SqlPlayerRepository _playerRepository; // Domain interface
        private readonly ILogger<PlayerManager> _logger;
        public PlayerManager(ILogger<PlayerManager> logger, SqlPlayerRepository playerRepository)
        {
            _playerRepository = playerRepository;
            _logger = logger;
        }
        public async Task<Player?> CreatePlayerAsync(Player player)
        {
            try
            {

                var success = await _playerRepository.UpsertPlayerAsync(player);

                if (success)
                {
                    _logger.LogInformation("Created player {Name} (ID: {PlayerId})", player.Name, player.Id);
                    return player;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create player {Name}", player.Name);
                return null;
            }
        }

        public async Task<Player?> LoadPlayerAsync(int playerId, int connectionId)
        {
            try
            {
                // Repository returns Domain Player, handles database mapping internally
                var player = await _playerRepository.GetByIdAsync(playerId);

                if (player != null)
                {
                    player.ConnectionId = connectionId;
                    _logger.LogInformation("Loaded player {Name} (ID: {PlayerId})", player.Name, playerId);
                }
                else
                {
                    _logger.LogWarning("Player {PlayerId} not found", playerId);
                }

                return player;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load player {PlayerId}", playerId);
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
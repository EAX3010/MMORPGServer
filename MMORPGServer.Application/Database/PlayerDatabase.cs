using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces;
using MMORPGServer.Domain.Entities;

namespace MMORPGServer.Application.Database
{
    /// <summary>
    /// Application service for player operations.
    /// ONLY works with Domain entities and DTOs - NO database entities!
    /// </summary>
    public class PlayerDatabase
    {
        private readonly IPlayerRepository _playerRepository; // Domain interface
        private readonly ILogger<PlayerDatabase> _logger;

        public PlayerDatabase(
            IPlayerRepository playerRepository,
            ILogger<PlayerDatabase> logger)
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
    }
}
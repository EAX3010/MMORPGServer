using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Application.Services
{

    /// <summary>
    /// Corrected GameWorld - uses PlayerManager (memory) for real-time operations.
    /// </summary>
    public class GameWorld
    {
        private readonly ILogger<GameWorld> _logger;
        private readonly IMapRepository _mapRepository;
        private readonly PlayerManager _playerManager; // Memory-based manager

        public GameWorld(
            ILogger<GameWorld> logger,
            IMapRepository mapRepository,
            PlayerManager playerManager)
        {
            _logger = logger;
            _mapRepository = mapRepository;
            _playerManager = playerManager;
        }

        public async Task<Player?> SpawnPlayerAsync(Player player, short mapId)
        {
            try
            {
                var map = await _mapRepository.GetMapAsync(mapId);
                if (map == null)
                {
                    _logger.LogError("Map {MapId} not found", mapId);
                    return null;
                }

                var spawnPoint = await map.GetValidSpawnPointAsync();
                if (!spawnPoint.HasValue)
                {
                    _logger.LogError("Could not find valid spawn point on map {MapId}", mapId);
                    return null;
                }

                // Update player position
                player.Position = spawnPoint.Value;
                player.MapId = mapId;
                player.Map = map;

                // Add to map
                if (!map.AddEntity(player))
                {
                    _logger.LogError("Failed to add player to map {MapId}", mapId);
                    return null;
                }

                // Add to memory manager
                await _playerManager.AddPlayerAsync(player);

                _logger.LogInformation("Spawned player {Name} on map {MapId} at {Position}",
                    player.Name, mapId, spawnPoint.Value);

                return player;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn player on map {MapId}", mapId);
                return null;
            }
        }

        public async Task<bool> MovePlayerAsync(int playerId, Position newPosition)
        {
            var player = await _playerManager.GetPlayerAsync(playerId);
            if (player?.Map == null)
            {
                _logger.LogWarning("Player {PlayerId} or map not found for move", playerId);
                return false;
            }

            return player.Map.TryMoveEntity(player, newPosition);
        }

        public async Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(int playerId, float range)
        {
            var player = await _playerManager.GetPlayerAsync(playerId);
            if (player?.Map == null)
            {
                return Enumerable.Empty<MapObject>();
            }

            return player.Map.GetEntitiesInRange(player.Position, range);
        }

        public async Task UpdateAsync(float deltaTime)
        {
            var maps = await _mapRepository.GetAllMapsAsync();
            foreach (var map in maps)
            {
                map.Update(deltaTime);
            }
        }
    }
}
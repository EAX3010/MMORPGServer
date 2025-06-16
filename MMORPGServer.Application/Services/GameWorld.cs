using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Application.Services
{
    public class GameWorld
    {
        private readonly ILogger<GameWorld> _logger;
        private readonly IMapRepository _mapRepository;
        private readonly IPlayerManager _playerManager;

        public GameWorld(
            ILogger<GameWorld> logger,
            IMapRepository mapRepository,
            IPlayerManager playerManager)
        {
            _logger = logger;
            _mapRepository = mapRepository;
            _playerManager = playerManager;
        }
        public async Task<Player?> SpawnPlayerAsync(Player player, ushort mapId)
        {
            Map map = await _mapRepository.GetMapAsync(mapId);
            if (map is null)
            {
                _logger.LogError("Map {MapId} not found", mapId);
                return null;
            }

            Position? spawnPoint = await map.GetValidSpawnPointAsync();
            if (!spawnPoint.HasValue)
            {
                _logger.LogError("Could not find valid spawn point on map {MapId}", mapId);
                return null;
            }
            player.Position = spawnPoint.Value;
            player.Map = map;
            if (!map.AddEntity(player))
            {
                _logger.LogError("Failed to add player to map {MapId}", mapId);
                return null;
            }

            await _playerManager.AddPlayerAsync(player);
            return player;
        }

        public async Task<bool> MovePlayerAsync(uint playerId, Position newPosition)
        {
            Player player = await _playerManager.GetPlayerAsync(playerId);
            if (player == null)
                return false;

            if (player.Map is null)
            {
                _logger.LogError("Map {MapId} not found", player.MapId);
                return false;
            }
            return player.Map.TryMoveEntity(player, newPosition);
        }

        public async Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(uint playerId, float range)
        {
            Player player = await _playerManager.GetPlayerAsync(playerId);
            if (player == null)
                return Enumerable.Empty<MapObject>();

            if (player.Map is null)
            {
                _logger.LogError("Map {MapId} not found", player.MapId);
                return Enumerable.Empty<MapObject>();
            }

            return player.Map.GetEntitiesInRange(player.Position, range);
        }

        public async void Update(float deltaTime)
        {
            var maps = await _mapRepository.GetAllMapsAsync();
            foreach (Map map in maps)
            {
                map.Update(deltaTime);
            }
        }
    }
}
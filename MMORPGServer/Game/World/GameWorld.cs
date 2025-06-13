using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Domain.ValueObjects;
using MMORPGServer.Game.Entities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MMORPGServer.Game.World
{
    public class GameWorld : IGameWorld
    {
        private readonly ILogger<GameWorld> _logger;
        private readonly IMapRepository _mapRepository;
        private readonly IPlayerManager _playerManager;
        private readonly ConcurrentDictionary<uint, Map> _activeMaps;
        private uint _nextEntityId = 1;

        public GameWorld(
            ILogger<GameWorld> logger,
            IMapRepository mapRepository,
            IPlayerManager playerManager)
        {
            _logger = logger;
            _mapRepository = mapRepository;
            _playerManager = playerManager;
            _activeMaps = new ConcurrentDictionary<uint, Map>();
        }

        public async Task<bool> LoadMapAsync(ushort mapId, string fileName)
        {
            if (_activeMaps.ContainsKey(mapId))
                return true;

            Map map = await _mapRepository.LoadMapDataAsync(mapId, fileName);
            if (map == null)
                return false;

            return _activeMaps.TryAdd(mapId, map);
        }

        public async Task<bool> UnloadMapAsync(ushort mapId)
        {
            if (!_activeMaps.TryRemove(mapId, out Map map))
                return false;

            // Remove all entities from the map
            foreach (MapObject entity in map.GetEntitiesInRange(Position.Zero, float.MaxValue))
            {
                if (entity is Player player)
                {
                    await _playerManager.RemovePlayerAsync(player.ObjectId);
                }
            }

            map.Dispose();
            return true;
        }

        public async Task<Player> SpawnPlayerAsync(IGameClient client, ushort mapId)
        {
            if (!_activeMaps.TryGetValue(mapId, out Map? map))
            {
                _logger.LogError("Map {MapId} is not loaded", mapId);
                return null;
            }

            Position? spawnPoint = await _mapRepository.GetValidSpawnPointAsync(map);
            if (!spawnPoint.HasValue)
            {
                _logger.LogError("Could not find valid spawn point on map {MapId}", mapId);
                return null;
            }
            Player player = client.Player;
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

            if (!_activeMaps.TryGetValue(player.MapId, out Map map))
                return false;

            return map.TryMoveEntity(player, newPosition);
        }

        public async Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(uint playerId, float range)
        {
            Player player = await _playerManager.GetPlayerAsync(playerId);
            if (player == null)
                return Enumerable.Empty<MapObject>();

            if (!_activeMaps.TryGetValue(player.MapId, out Map map))
                return Enumerable.Empty<MapObject>();

            return map.GetEntitiesInRange(player.Position, range);
        }

        public void Update(float deltaTime)
        {
            foreach (Map map in _activeMaps.Values)
            {
                map.Update(deltaTime);
            }
        }
    }
}
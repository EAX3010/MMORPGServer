namespace MMORPGServer.Game.World
{
    public class GameWorld
    {
        private readonly ConcurrentDictionary<uint, MapObject> _entities;
        private readonly ConcurrentDictionary<ushort, Map> _maps;
        private uint _nextEntityId = 1;

        public GameWorld()
        {
            _entities = new ConcurrentDictionary<uint, MapObject>();
            _maps = new ConcurrentDictionary<ushort, Map>();
        }

        public Map CreateMap(ushort mapId, string name, int width, int height)
        {
            var map = new Map(mapId, name, width, height);
            _maps.TryAdd(mapId, map);
            return map;
        }

        public Map? GetMap(ushort mapId)
        {
            _maps.TryGetValue(mapId, out var map);
            return map;
        }

        public T CreateEntity<T>(string name, ushort mapId, Vector2 position) where T : MapObject, new()
        {
            var entity = new T
            {
                Id = _nextEntityId++,
            };

            if (_maps.TryGetValue(mapId, out var map))
            {
                map.AddEntity(entity);
                entity.Position = position;
                _entities.TryAdd(entity.Id, entity);
            }

            return entity;
        }

        public bool RemoveEntity(uint id)
        {
            if (_entities.TryRemove(id, out var entity))
            {
                // Remove from map if it exists
                foreach (var map in _maps.Values)
                {
                    if (map.RemoveEntity(id))
                    {
                        break;
                    }
                }
                return true;
            }
            return false;
        }

        public T? GetEntity<T>(uint id) where T : MapObject
        {
            if (_entities.TryGetValue(id, out var entity) && entity is T typedEntity)
            {
                return typedEntity;
            }
            return null;
        }

        public IEnumerable<T> GetEntitiesOfType<T>() where T : MapObject
        {
            return _entities.Values.OfType<T>();
        }

        public void Update(float deltaTime)
        {
            foreach (var map in _maps.Values)
            {
                map.Update(deltaTime);
            }
        }

        public IEnumerable<MapObject> GetEntitiesInRange(Vector2 position, float range, ushort mapId)
        {
            if (_maps.TryGetValue(mapId, out var map))
            {
                return map.GetEntitiesInRange(position, range);
            }
            return Enumerable.Empty<MapObject>();
        }
    }
}
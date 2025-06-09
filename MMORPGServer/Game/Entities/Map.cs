using MMORPGServer.Game.World.Spatial;

namespace MMORPGServer.Game.Entities
{
    public partial class Map : IDisposable
    {
        private readonly SpatialHashGrid<MapObject> _spatialGrid;
        private readonly ConcurrentDictionary<uint, MapObject> _entities;
        private readonly bool[,] _walkable;

        public ushort Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public Map(ushort id, string name, int width, int height)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;

            // Initialize the new spatial system
            _spatialGrid = new SpatialHashGrid<MapObject>(width, height, cellSize: 32);
            _entities = new ConcurrentDictionary<uint, MapObject>();
            _walkable = new bool[width, height];

            // Initialize all tiles as walkable
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _walkable[x, y] = true;
                }
            }
        }

        // REPLACE your old AddEntity method
        public void AddEntity(MapObject entity)
        {
            if (_entities.TryAdd(entity.ObjectId, entity))
            {
                _spatialGrid.Add(entity);
                Console.WriteLine($"Added {entity.GetType().Name} {entity.ObjectId} at {entity.Position}");
            }
        }

        // REPLACE your old RemoveEntity method
        public bool RemoveEntity(uint id)
        {
            if (_entities.TryRemove(id, out var entity))
            {
                _spatialGrid.Remove(entity);
                Console.WriteLine($"Removed {entity.GetType().Name} {entity.ObjectId}");
                return true;
            }
            return false;
        }

        // NEW: Handle entity movement efficiently
        public bool MoveEntity(uint entityId, Vector2 newPosition)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                var oldPosition = entity.Position;
                entity.Position = newPosition; // Update entity position

                return _spatialGrid.Move(entity, oldPosition, newPosition);
            }
            return false;
        }

        // REPLACE your old GetEntitiesInRange method
        public IEnumerable<MapObject> GetEntitiesInRange(Vector2 position, float range)
        {
            return _spatialGrid.QueryRadius(position, range);
        }

        // NEW: Get specific types in range (replaces your old type filtering)
        public IEnumerable<MapObject> GetPlayersInRange(Vector2 position, float range)
        {
            return _spatialGrid.QueryRadius(position, range, MapObjectType.Player);
        }

        public IEnumerable<MapObject> GetMonstersInRange(Vector2 position, float range)
        {
            return _spatialGrid.QueryRadius(position, range, MapObjectType.Monster);
        }

        public IEnumerable<MapObject> GetItemsInRange(Vector2 position, float range)
        {
            return _spatialGrid.QueryRadius(position, range, MapObjectType.Item);
        }

        // NEW: Screen-based queries (replaces your old View.Roles method)
        public IEnumerable<MapObject> GetEntitiesInScreen(Vector2 center, int screenRadius = 50)
        {
            return _spatialGrid.QueryScreen(center, screenRadius);
        }

        // NEW: Find nearest entity
        public MapObject? FindNearestPlayer(Vector2 position, float maxDistance = 100f)
        {
            return _spatialGrid.FindNearest(position, MapObjectType.Player, maxDistance);
        }

        public MapObject? FindNearestMonster(Vector2 position, float maxDistance = 100f)
        {
            return _spatialGrid.FindNearest(position, MapObjectType.Monster, maxDistance);
        }

        // NEW: Count entities efficiently
        public int CountPlayersInRange(Vector2 position, float range)
        {
            return _spatialGrid.CountInRadius(position, range, MapObjectType.Player);
        }

        // NEW: Get all entities of a type (replaces your old GetAllMapRoles)
        public IEnumerable<MapObject> GetAllPlayers()
        {
            return _spatialGrid.GetAllObjects(MapObjectType.Player);
        }

        public IEnumerable<MapObject> GetAllMonsters()
        {
            return _spatialGrid.GetAllObjects(MapObjectType.Monster);
        }

        // Performance monitoring
        public void LogSpatialStats()
        {
            var stats = _spatialGrid.GetStats();
            Console.WriteLine($"Map {Id} Spatial Stats:");
            Console.WriteLine($"  Active Objects: {stats.ActiveObjects}");
            Console.WriteLine($"  Total Queries: {stats.TotalQueries}");
            Console.WriteLine($"  Active Cells: {stats.ActiveCells}");
            Console.WriteLine($"  Memory Usage: {stats.MemoryUsage:N0} bytes");
        }

        public void Dispose()
        {
            _spatialGrid?.Dispose();
        }
    }
}
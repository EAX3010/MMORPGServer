namespace MMORPGServer.Game.Maps
{
    public partial class Map : IDisposable
    {
        private readonly Cell[,] _cells;
        private readonly Dictionary<int, Position> _portalPositions;
        private readonly ConcurrentDictionary<uint, MapObject> _entities;
        private readonly SpatialGrid _spatialGrid;
        private readonly ILogger<Map> _logger;

        public ushort Id { get; }
        public string FilePath { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyDictionary<int, Position> PortalPositions => _portalPositions;

        public Map(ushort id, string filePath, int width, int height)
        {
            Id = id;
            FilePath = filePath;
            Width = width;
            Height = height;
            _entities = new ConcurrentDictionary<uint, MapObject>();
            _cells = new Cell[width, height];
            _portalPositions = new Dictionary<int, Position>();
            _spatialGrid = new SpatialGrid(width, height, 32); // 32x32 cell size
        }

        public Cell this[int x, int y]
        {
            get
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return new Cell(CellType.Terrain, 0, 0);
                return _cells[x, y];
            }
            set
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    _cells[x, y] = value;
            }
        }

        public bool AddEntity(MapObject entity)
        {
            if (!_entities.TryAdd(entity.ObjectId, entity))
                return false;

            _spatialGrid.Add(entity);
            return true;
        }

        public bool RemoveEntity(uint entityId)
        {
            if (!_entities.TryRemove(entityId, out var entity))
                return false;

            _spatialGrid.Remove(entity);
            return true;
        }

        public MapObject? GetEntity(uint entityId)
        {
            _entities.TryGetValue(entityId, out var entity);
            return entity;
        }

        public IEnumerable<MapObject> GetEntitiesInRange(Position position, float range)
        {
            return _spatialGrid.GetEntitiesInRange(position, range);
        }

        public bool IsValidPosition(Position position)
        {
            int x = position.X;
            int y = position.Y;

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;

            var cell = _cells[x, y];
            return cell[CellType.Open] && !cell[CellType.Portal];
        }

        public bool TryMoveEntity(MapObject entity, Position newPosition)
        {
            if (!IsValidPosition(newPosition))
                return false;

            var oldPosition = entity.Position;
            entity.Position = newPosition;
            _spatialGrid.Update(entity);
            return true;
        }

        public void Update(float deltaTime)
        {
            foreach (var entity in _entities.Values)
            {
                if (entity is IUpdatable updatable)
                {
                    updatable.Update(deltaTime);
                }
            }
        }

        public void Dispose()
        {
            _entities.Clear();
            _spatialGrid.Clear();
        }

        public void AddPortal(int destinationId, Position pos)
        {
            _portalPositions[destinationId] = pos;
        }
    }
}
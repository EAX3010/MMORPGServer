namespace MMORPGServer.Domain.Entities
{
    /// <summary>
    /// Represents a game map with entities, terrain, and spatial management
    /// </summary>
    public partial class Map
    {
        private readonly Cell[,] _cells;
        private readonly Dictionary<int, Position> _portalPositions;
        private readonly ConcurrentDictionary<uint, MapObject> _entities;
        private readonly SpatialGrid _spatialGrid;

        // Domain events for infrastructure to handle
        public event Action<MapEntityAddedEvent>? EntityAdded;
        public event Action<MapEntityRemovedEvent>? EntityRemoved;
        public event Action<MapEntityMovedEvent>? EntityMoved;

        public ushort Id { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyDictionary<int, Position> PortalPositions => _portalPositions;
        public IReadOnlyCollection<MapObject> Entities => _entities.Values.ToList();
        public int EntityCount => _entities.Count;

        public Map(ushort id, int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Map dimensions must be positive");

            Id = id;
            Width = width;
            Height = height;
            _entities = new ConcurrentDictionary<uint, MapObject>();
            _cells = new Cell[width, height];
            _portalPositions = new Dictionary<int, Position>();
            _spatialGrid = new SpatialGrid(width, height, 32); // 32x32 cell size
        }

        /// <summary>
        /// Gets or sets a cell at the specified coordinates
        /// </summary>
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

        /// <summary>
        /// Adds an entity to the map
        /// </summary>
        public bool AddEntity(MapObject entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!IsValidPosition(entity.Position))
                return false;

            if (!_entities.TryAdd(entity.ObjectId, entity))
                return false;

            entity.MapId = Id;
            _spatialGrid.Add(entity);

            // Raise domain event
            EntityAdded?.Invoke(new MapEntityAddedEvent(Id, entity.ObjectId, entity.Position));

            return true;
        }

        /// <summary>
        /// Removes an entity from the map
        /// </summary>
        public bool RemoveEntity(uint entityId)
        {
            if (!_entities.TryRemove(entityId, out var entity))
                return false;

            _spatialGrid.Remove(entity);

            // Raise domain event
            EntityRemoved?.Invoke(new MapEntityRemovedEvent(Id, entityId));

            return true;
        }

        /// <summary>
        /// Gets an entity by its ID
        /// </summary>
        public MapObject? GetEntity(uint entityId)
        {
            _entities.TryGetValue(entityId, out var entity);
            return entity;
        }

        /// <summary>
        /// Gets all entities within range of a position
        /// </summary>
        public IEnumerable<MapObject> GetEntitiesInRange(Position position, float range)
        {
            if (range <= 0)
                return Enumerable.Empty<MapObject>();

            return _spatialGrid.GetEntitiesInRange(position, range);
        }

        /// <summary>
        /// Checks if a position is valid for entity placement
        /// </summary>
        public bool IsValidPosition(Position position)
        {
            int x = position.X;
            int y = position.Y;

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;

            var cell = _cells[x, y];
            return cell[CellType.Open] && !cell[CellType.Portal];
        }

        /// <summary>
        /// Attempts to move an entity to a new position
        /// </summary>
        public bool TryMoveEntity(MapObject entity, Position newPosition)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!IsValidPosition(newPosition))
                return false;

            // Check if position is occupied by another entity
            var entitiesAtPosition = GetEntitiesInRange(newPosition, 0.5f);
            if (entitiesAtPosition.Any(e => e.ObjectId != entity.ObjectId && e.Position == newPosition))
                return false;

            var oldPosition = entity.Position;
            entity.Position = newPosition;
            _spatialGrid.Update(entity);

            // Raise domain event
            EntityMoved?.Invoke(new MapEntityMovedEvent(Id, entity.ObjectId, oldPosition, newPosition));

            return true;
        }

        /// <summary>
        /// Updates all entities on the map
        /// </summary>
        public void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                return;

            foreach (var entity in _entities.Values)
            {

            }
        }

        /// <summary>
        /// Adds a portal to the map
        /// </summary>
        public void AddPortal(int destinationMapId, Position position)
        {
            if (!IsValidPosition(position))
                throw new ArgumentException("Portal position is not valid", nameof(position));

            _portalPositions[destinationMapId] = position;

            // Mark the cell as a portal
            this[position.X, position.Y] = new Cell(CellType.Portal, 0, 0);
        }

        /// <summary>
        /// Gets the destination map ID for a portal at the given position
        /// </summary>
        public int? GetPortalDestination(Position position)
        {
            foreach (var kvp in _portalPositions)
            {
                if (kvp.Value == position)
                    return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// Clears all entities from the map (for cleanup)
        /// </summary>
        public void Clear()
        {
            _entities.Clear();
            _spatialGrid.Clear();
        }

        /// <summary>
        /// Gets entities by type
        /// </summary>
        public IEnumerable<T> GetEntitiesOfType<T>() where T : MapObject
        {
            return _entities.Values.OfType<T>();
        }

        /// <summary>
        /// Checks if the map contains an entity
        /// </summary>
        public bool ContainsEntity(uint entityId)
        {
            return _entities.ContainsKey(entityId);
        }
    }
}
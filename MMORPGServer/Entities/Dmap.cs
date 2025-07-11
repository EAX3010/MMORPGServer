using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Common.ValueObjects;
using System.Collections.Concurrent;

namespace MMORPGServer.Entities
{
    /// <summary>
    /// Represents a game map with entities, terrain, and spatial management
    /// </summary>
    public class DMap : IDisposable
    {
        private bool _disposed = false;
        private readonly Cell[,] _cells;
        private readonly Dictionary<int, Position> _portalPositions;
        private readonly ConcurrentDictionary<int, MapObject> _entities; // Added missing field
        private readonly SpatialGrid _spatialGrid;

        public short Id { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyDictionary<int, Position> PortalPositions => _portalPositions;
        public IReadOnlyCollection<MapObject> Entities => _entities.Values.ToList(); // Added property
        public int EntityCount => _entities.Count; // Added property

        public DMap(short id, int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Map dimensions must be positive");

            Id = id;
            Width = width;
            Height = height;
            _entities = new ConcurrentDictionary<int, MapObject>(); // Initialize missing field
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
                    return new Cell(CellType.Blocked, 0, 0);
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
            ArgumentNullException.ThrowIfNull(entity); // Added null check

            if (!IsValidPosition(entity.Position))
                return false;

            // Try to add to entities collection first
            if (!_entities.TryAdd(entity.Id, entity))
                return false;

            _spatialGrid.Add(entity);
            this[entity.Position.X, entity.Position.Y].AddFlag(CellType.Entity);

            return true;
        }

        /// <summary>
        /// Removes an entity from the map
        /// </summary>
        public bool RemoveEntity(MapObject entity)
        {
            ArgumentNullException.ThrowIfNull(entity); // Added null check
            return RemoveEntity(entity.Id);
        }

        /// <summary>
        /// Removes an entity from the map by ID
        /// </summary>
        public bool RemoveEntity(int entityId)
        {
            if (!_entities.TryRemove(entityId, out MapObject entity))
                return false;

            _spatialGrid.Remove(entity);
            this[entity.Position.X, entity.Position.Y].RemoveFlag(CellType.Entity);

            return true;
        }

        /// <summary>
        /// Gets an entity by its ID
        /// </summary>
        public MapObject? GetEntity(int entityId)
        {
            _entities.TryGetValue(entityId, out MapObject entity);
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

            Cell cell = _cells[x, y];
            bool result = !cell[CellType.Blocked];
            return result;
        }

        /// <summary>
        /// Attempts to move an entity to a new position
        /// </summary>
        public bool TryMoveEntity(MapObject entity, Position newPosition)
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (!IsValidPosition(newPosition))
                return false;

            // Check if position is occupied by another entity
            IEnumerable<MapObject> entitiesAtPosition = GetEntitiesInRange(newPosition, 0.5f);
            if (entitiesAtPosition.Any(e => e.Id != entity.Id && e.Position == newPosition))
                return false;

            Position oldPosition = entity.Position;
            entity.Position = newPosition;
            _spatialGrid.Update(entity);

            // Update cell flags
            this[oldPosition.X, oldPosition.Y].RemoveFlag(CellType.Entity);
            this[newPosition.X, newPosition.Y].AddFlag(CellType.Entity);

            return true;
        }

        /// <summary>
        /// Updates all entities on the map
        /// </summary>
        public void Update(float deltaTime)
        {
            if (deltaTime <= 0)
                return;

            // The Map class will handle entity updates
            // This method can be used for map-specific updates like environmental effects
        }

        /// <summary>
        /// Adds a portal to the map
        /// </summary>
        public void AddPortal(int destinationMapId, Position position)
        {
            if (!IsValidPosition(position))
                return;

            _portalPositions[destinationMapId] = position;

            // Mark the cell as a portal (use Portal type instead of Entity)
            this[position.X, position.Y] = new Cell(CellType.StaticObj, 0, 0);
        }

        /// <summary>
        /// Gets the destination map ID for a portal at the given position
        /// </summary>
        public int? GetPortalDestination(Position position)
        {
            foreach (KeyValuePair<int, Position> kvp in _portalPositions)
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

            // Clear entity flags from all cells
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _cells[x, y].RemoveFlag(CellType.Entity);
                }
            }
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
        public bool ContainsEntity(int entityId)
        {
            return _entities.ContainsKey(entityId);
        }
        public CellType GetTerrainType(Position position)
        {
            return this[position.X, position.Y].Flags;
        }
        public void SetTerrainType(Position position, CellType cellType)
        {
            if (IsValidPosition(position))
            {
                this[position.X, position.Y] = new Cell(cellType, 0, 0);
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any disposable entities on the map.
                foreach (MapObject entity in _entities.Values)
                {
                    if (entity is IDisposable disposableEntity)
                    {
                        disposableEntity.Dispose();
                    }
                }

                // Clear all collections to release references.
                _entities.Clear();
                _portalPositions.Clear();
                _spatialGrid?.Clear();
            }

            _disposed = true;
        }

        // Finalizer (called by the garbage collector)
        ~DMap()
        {
            Dispose(false);
        }
    }
}

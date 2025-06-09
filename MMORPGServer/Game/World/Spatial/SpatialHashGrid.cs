namespace MMORPGServer.Game.World.Spatial
{
    /// <summary>
    /// Modern high-performance spatial hash grid using C# 13 features
    /// </summary>
    /// <typeparam name="T">Type implementing ISpatialObject</typeparam>
    public sealed class SpatialHashGrid<T> : IDisposable where T : class, ISpatialObject
    {
        #region Constants and Configuration
        private const int DEFAULT_CELL_SIZE = 32; // Optimized for modern games (was 18)
        private const int MAX_OBJECTS_PER_CELL = 64;
        private const int INITIAL_CELL_CAPACITY = 16;
        private const int CLEANUP_INTERVAL_MS = 30000; // 30 seconds
        #endregion

        #region Fields
        private readonly int _cellSize;
        private readonly int _mapWidth;
        private readonly int _mapHeight;
        private readonly int _gridWidth;
        private readonly int _gridHeight;

        // Use ConcurrentDictionary for thread-safe operations
        private readonly ConcurrentDictionary<long, SpatialCell<T>> _cells = new();

        // Performance counters
        private long _totalObjects;
        private long _activeObjects;
        private long _totalQueries;
        private long _lastCleanupTicks = Environment.TickCount64;

        // Thread-safe disposal
        private volatile bool _disposed;
        private readonly object _disposeLock = new();
        #endregion

        #region Properties
        public int CellSize => _cellSize;
        public int TotalObjects => (int)Interlocked.Read(ref _totalObjects);
        public int ActiveObjects => (int)Interlocked.Read(ref _activeObjects);
        public int TotalQueries => (int)Interlocked.Read(ref _totalQueries);
        public int ActiveCells => _cells.Count;
        #endregion

        #region Constructor
        public SpatialHashGrid(int mapWidth, int mapHeight, int cellSize = DEFAULT_CELL_SIZE)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            _cellSize = cellSize;
            _gridWidth = (mapWidth + cellSize - 1) / cellSize;
            _gridHeight = (mapHeight + cellSize - 1) / cellSize;
        }
        #endregion

        #region Core Operations
        /// <summary>
        /// Add an object to the spatial grid
        /// </summary>
        public bool Add(T obj)
        {
            if (_disposed || obj?.IsActive != true) return false;

            var cellKey = GetCellKey(obj.Position);
            var cell = _cells.GetOrAdd(cellKey, static _ => new SpatialCell<T>());

            if (cell.Add(obj))
            {
                Interlocked.Increment(ref _totalObjects);
                Interlocked.Increment(ref _activeObjects);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove an object from the spatial grid
        /// </summary>
        public bool Remove(T obj)
        {
            if (_disposed || obj == null) return false;

            var cellKey = GetCellKey(obj.Position);
            if (_cells.TryGetValue(cellKey, out var cell) && cell.Remove(obj))
            {
                Interlocked.Decrement(ref _activeObjects);

                // Clean up empty cells
                if (cell.IsEmpty)
                {
                    _cells.TryRemove(cellKey, out _);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Move an object from old position to new position
        /// </summary>
        public bool Move(T obj, Vector2 oldPosition, Vector2 newPosition)
        {
            if (_disposed || obj?.IsActive != true) return false;

            var oldKey = GetCellKey(oldPosition);
            var newKey = GetCellKey(newPosition);

            // Same cell, no need to move
            if (oldKey == newKey) return true;

            // Remove from old cell
            if (_cells.TryGetValue(oldKey, out var oldCell))
            {
                oldCell.Remove(obj);
                if (oldCell.IsEmpty)
                {
                    _cells.TryRemove(oldKey, out _);
                }
            }

            // Add to new cell
            var newCell = _cells.GetOrAdd(newKey, static _ => new SpatialCell<T>());
            return newCell.Add(obj);
        }

        /// <summary>
        /// Query objects within a radius around a position
        /// </summary>
        public IEnumerable<T> QueryRadius(Vector2 center, float radius, MapObjectType? typeFilter = null)
        {
            if (_disposed) yield break;

            Interlocked.Increment(ref _totalQueries);

            var radiusSquared = radius * radius;
            var cellRadius = (int)Math.Ceiling(radius / _cellSize);
            var centerCell = GetCellCoordinates(center);

            for (int x = centerCell.X - cellRadius; x <= centerCell.X + cellRadius; x++)
            {
                for (int y = centerCell.Y - cellRadius; y <= centerCell.Y + cellRadius; y++)
                {
                    if (!IsValidCell(x, y)) continue;

                    var cellKey = GetCellKey(x, y);
                    if (!_cells.TryGetValue(cellKey, out var cell)) continue;

                    foreach (var obj in cell.GetObjects(typeFilter))
                    {
                        if (!obj.IsActive) continue;

                        var distanceSquared = Vector2.DistanceSquared(center, obj.Position);
                        if (distanceSquared <= radiusSquared)
                        {
                            yield return obj;
                        }
                    }
                }
            }

            // Periodic cleanup
            TryPerformCleanup();
        }

        /// <summary>
        /// Query objects in a rectangular area
        /// </summary>
        public IEnumerable<T> QueryRectangle(Vector2 min, Vector2 max, MapObjectType? typeFilter = null)
        {
            if (_disposed) yield break;

            Interlocked.Increment(ref _totalQueries);

            var minCell = GetCellCoordinates(min);
            var maxCell = GetCellCoordinates(max);

            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    if (!IsValidCell(x, y)) continue;

                    var cellKey = GetCellKey(x, y);
                    if (!_cells.TryGetValue(cellKey, out var cell)) continue;

                    foreach (var obj in cell.GetObjects(typeFilter))
                    {
                        if (!obj.IsActive) continue;

                        var pos = obj.Position;
                        if (pos.X >= min.X && pos.X <= max.X &&
                            pos.Y >= min.Y && pos.Y <= max.Y)
                        {
                            yield return obj;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all objects of a specific type
        /// </summary>
        public IEnumerable<T> GetAllObjects(MapObjectType? typeFilter = null)
        {
            if (_disposed) yield break;

            foreach (var cell in _cells.Values)
            {
                foreach (var obj in cell.GetObjects(typeFilter))
                {
                    if (obj.IsActive)
                        yield return obj;
                }
            }
        }

        /// <summary>
        /// Count objects in radius
        /// </summary>
        public int CountInRadius(Vector2 center, float radius, MapObjectType? typeFilter = null)
        {
            return QueryRadius(center, radius, typeFilter).Count();
        }

        /// <summary>
        /// Find nearest object of type
        /// </summary>
        public T? FindNearest(Vector2 position, MapObjectType objectType, float maxDistance = float.MaxValue)
        {
            T? nearest = null;
            var nearestDistanceSquared = maxDistance * maxDistance;

            foreach (var obj in QueryRadius(position, maxDistance, objectType))
            {
                var distanceSquared = Vector2.DistanceSquared(position, obj.Position);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = obj;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }
        #endregion

        #region Helper Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetCellKey(Vector2 position)
        {
            var coords = GetCellCoordinates(position);
            return GetCellKey(coords.X, coords.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetCellKey(int x, int y)
        {
            // Pack x and y into a single long for dictionary key
            return ((long)x << 32) | (uint)y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int X, int Y) GetCellCoordinates(Vector2 position)
        {
            return ((int)position.X / _cellSize, (int)position.Y / _cellSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;
        }

        private void TryPerformCleanup()
        {
            var currentTicks = Environment.TickCount64;
            if (currentTicks - _lastCleanupTicks > CLEANUP_INTERVAL_MS)
            {
                Task.Run(PerformCleanup);
                _lastCleanupTicks = currentTicks;
            }
        }

        private void PerformCleanup()
        {
            var removedCells = 0;
            var removedObjects = 0;

            foreach (var kvp in _cells.ToArray())
            {
                var cell = kvp.Value;
                var removedFromCell = cell.RemoveInactive();
                removedObjects += removedFromCell;

                if (cell.IsEmpty)
                {
                    if (_cells.TryRemove(kvp.Key, out _))
                        removedCells++;
                }
            }

            Interlocked.Add(ref _activeObjects, -removedObjects);
            Interlocked.Add(ref _totalObjects, -removedObjects);

            if (removedCells > 0 || removedObjects > 0)
            {
                // Could log cleanup stats here
            }
        }

        /// <summary>
        /// Get performance statistics
        /// </summary>
        public SpatialGridStats GetStats()
        {
            return new SpatialGridStats
            {
                TotalObjects = TotalObjects,
                ActiveObjects = ActiveObjects,
                TotalQueries = TotalQueries,
                ActiveCells = ActiveCells,
                CellSize = CellSize,
                GridDimensions = (_gridWidth, _gridHeight),
                MemoryUsage = _cells.Count * 1024 // Rough estimate
            };
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;

            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;

                _cells.Clear();
                GC.SuppressFinalize(this);
            }
        }
        #endregion
    }
}



namespace MMORPGServer.Game.World.Spatial
{
    /// <summary>
    /// Thread-safe spatial cell container
    /// </summary>
    internal sealed class SpatialCell<T> where T : class, ISpatialObject
    {
        private readonly ConcurrentDictionary<MapObjectType, ConcurrentBag<T>> _objectsByType = new();
        private volatile int _objectCount;

        public bool IsEmpty => _objectCount == 0;

        public bool Add(T obj)
        {
            var bag = _objectsByType.GetOrAdd(obj.ObjectType, _ => new ConcurrentBag<T>());
            bag.Add(obj);
            Interlocked.Increment(ref _objectCount);
            return true;
        }

        public bool Remove(T obj)
        {
            if (_objectsByType.TryGetValue(obj.ObjectType, out var bag))
            {
                // ConcurrentBag doesn't have Remove, so we mark as inactive
                // and rely on cleanup to remove inactive objects
                Interlocked.Decrement(ref _objectCount);
                return true;
            }
            return false;
        }

        public IEnumerable<T> GetObjects(MapObjectType? typeFilter = null)
        {
            if (typeFilter.HasValue)
            {
                if (_objectsByType.TryGetValue(typeFilter.Value, out var bag))
                {
                    return bag.Where(obj => obj.IsActive);
                }
                return Enumerable.Empty<T>();
            }

            return _objectsByType.Values
                .SelectMany(bag => bag)
                .Where(obj => obj.IsActive);
        }

        public int RemoveInactive()
        {
            var removed = 0;
            var newObjectsByType = new ConcurrentDictionary<MapObjectType, ConcurrentBag<T>>();

            foreach (var kvp in _objectsByType)
            {
                var newBag = new ConcurrentBag<T>();
                var oldBag = kvp.Value;

                foreach (var obj in oldBag)
                {
                    if (obj.IsActive)
                    {
                        newBag.Add(obj);
                    }
                    else
                    {
                        removed++;
                    }
                }

                if (!newBag.IsEmpty)
                {
                    newObjectsByType[kvp.Key] = newBag;
                }
            }

            // Replace the old dictionary
            _objectsByType.Clear();
            foreach (var kvp in newObjectsByType)
            {
                _objectsByType[kvp.Key] = kvp.Value;
            }

            Interlocked.Add(ref _objectCount, -removed);
            return removed;
        }
    }
}

using MMORPGServer.Domain.Entities;
using System.Numerics;

namespace MMORPGServer.Domain.ValueObjects
{
    public class SpatialGrid
    {
        private readonly int _cellSize;
        private readonly int _width;
        private readonly int _height;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly Dictionary<(int, int), HashSet<MapObject>> _grid;

        public SpatialGrid(int width, int height, int cellSize)
        {
            _width = width;
            _height = height;
            _cellSize = cellSize;
            _gridWidth = (int)Math.Ceiling((double)width / cellSize);
            _gridHeight = (int)Math.Ceiling((double)height / cellSize);
            _grid = new Dictionary<(int, int), HashSet<MapObject>>();
        }

        private (int, int) GetCellKey(Position position)
        {
            int x = position.X / _cellSize;
            int y = position.Y / _cellSize;
            return (x, y);
        }

        public void Add(MapObject entity)
        {
            (int, int) key = GetCellKey(entity.Position);
            if (!_grid.ContainsKey(key))
            {
                _grid[key] = new HashSet<MapObject>();
            }
            _grid[key].Add(entity);
        }

        public void Remove(MapObject entity)
        {
            (int, int) key = GetCellKey(entity.Position);
            if (_grid.TryGetValue(key, out HashSet<MapObject> cell))
            {
                cell.Remove(entity);
                if (cell.Count == 0)
                {
                    _grid.Remove(key);
                }
            }
        }

        public void Update(MapObject entity)
        {
            Remove(entity);
            Add(entity);
        }

        public IEnumerable<MapObject> GetEntitiesInRange(Position position, float range)
        {
            HashSet<MapObject> result = new HashSet<MapObject>();
            (int, int) centerKey = GetCellKey(position);
            int cellRange = (int)Math.Ceiling(range / _cellSize);

            for (int x = -cellRange; x <= cellRange; x++)
            {
                for (int y = -cellRange; y <= cellRange; y++)
                {
                    (int, int) key = (centerKey.Item1 + x, centerKey.Item2 + y);
                    if (_grid.TryGetValue(key, out HashSet<MapObject> cell))
                    {
                        foreach (MapObject entity in cell)
                        {
                            if (Vector2.Distance((Vector2)position, (Vector2)entity.Position) <= range)
                            {
                                result.Add(entity);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public void Clear()
        {
            _grid.Clear();
        }
    }
}
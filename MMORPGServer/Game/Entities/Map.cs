using MMORPGServer.Game.Entities.Roles;
using System.Numerics;

namespace MMORPGServer.Game.Entities
{
    public class Map
    {
        public ushort Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        private readonly ConcurrentDictionary<uint, MapObject> _entities;
        private readonly bool[,] _walkable;

        public Map(ushort id, string name, int width, int height)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
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

        public void AddEntity(MapObject entity)
        {
            _entities.TryAdd(entity.Id, entity);
        }

        public bool RemoveEntity(uint id)
        {
            return _entities.TryRemove(id, out _);
        }

        public void Update(float deltaTime)
        {
            foreach (var entity in _entities.Values)
            {
                if (entity.IsActive)
                {
                    entity.Update(deltaTime);
                }
            }
        }

        public IEnumerable<MapObject> GetEntitiesInRange(Vector2 position, float range)
        {
            return _entities.Values.Where(e =>
                Vector2.Distance(e.Position, position) <= range);
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
            return _walkable[x, y];
        }

        public void SetWalkable(int x, int y, bool walkable)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                _walkable[x, y] = walkable;
            }
        }

        public bool IsValidPosition(Vector2 position)
        {
            int x = (int)position.X;
            int y = (int)position.Y;
            return IsWalkable(x, y);
        }
    }
}
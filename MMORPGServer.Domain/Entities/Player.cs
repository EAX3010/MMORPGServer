using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Common.Interfaces;

namespace MMORPGServer.Domain.Entities
{
    public class Player : MapObject
    {
        public Player(int id, int connectionId)
        {
            Id = id;
            ConnectionId = connectionId;

        }
        public static Player Create(int connectionId, int id, string name, int level, long Experience,
        short mapId, short x, short y, long gold, int conquerPoints, int boundConquerPoints)
        {
            return new Player(id, connectionId)
            {
                Name = name,
                Level = level,
                Experience = Experience,
                MapId = mapId,
                Position = new ValueObjects.Position(x, y),
                Gold = gold,
                ConquerPoints = conquerPoints,
                BoundConquerPoints = boundConquerPoints,
                MaxHealth = 100,
                MaxMana = 100,
                CurrentHealth = 100,
                CurrentMana = 100,
                Agility = 10,
                Spirit = 10,
                Vitality = 10,
                Strength = 10,
                LastLogin = DateTime.UtcNow,
                IsDirty = true,
            };

        }
        public int ConnectionId { get; set; }
        public string Name { get; set; } = default!;
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;



        // Resources
        public long Gold { get; set; } = 0;
        public int ConquerPoints { get; set; } = 0;
        public int BoundConquerPoints { get; set; } = 0;

        // Stats
        public int MaxHealth { get; set; } = 0;
        public int CurrentHealth { get; set; } = 0;
        public int MaxMana { get; set; } = 0;
        public int CurrentMana { get; set; } = 0;
        public short Strength { get; set; } = 0;
        public short Agility { get; set; } = 0;
        public short Vitality { get; set; } = 0;
        public short Spirit { get; set; } = 0;

        public bool IsDirty { get; set; }
        public DateTime LastLogin { get; set; }

        protected override MapObjectType GetObjectType()
        {
            return MapObjectType.Player;
        }

        public void MarkAsSaved()
        {
            IsDirty = true;
        }
    }

}
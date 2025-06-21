using MMORPGServer.Domain.Common;

namespace MMORPGServer.Domain.Persistence
{
    public class PlayerEntity : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;

        // Position
        public short MapId { get; set; } = 1001;
        public short X { get; set; }
        public short Y { get; set; }


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

        // Player-specific timestamps
        public DateTime? LastLogin { get; set; }
        public DateTime? LastLogout { get; set; }
        public static PlayerEntity Create(int id, string name, int level, long Experience,
           short mapId, short x, short y, long gold)
        {
            return new PlayerEntity
            {
                Id = id,
                Name = name,
                Level = level,
                Experience = Experience,
                MapId = mapId,
                X = x,
                Y = y,
                Gold = gold
            };

        }
    }
}
using MMORPGServer.Domain.Common;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Domain.Entities
{
    public class Player : MapObject
    {
        public Player(int connectionId, int id)
        {
            Name = "New Player";
            ConnectionId = connectionId;
            Id = id;
            Position = new Position(300, 300);
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
        public DateTime LastSaveTime { get; set; }
        public DateTime LastLogin { get; set; }

        protected override MapObjectType GetObjectType()
        {
            return MapObjectType.Player;
        }
    }

}
using MMORPGServer.Domain.Common;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Domain.Entities
{
    public class Player : MapObject
    {
        public Player(uint connectionId, uint objectId)
        {
            Name = "New Player";
            ConnectionId = connectionId;
            ObjectId = objectId;
            Position = new Position(300, 300);
        }
        public uint ConnectionId { get; set; }
        public string Name { get; set; }
        // Basic stats
        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int MaxHealth { get; set; } = 100;
        public int CurrentHealth { get; set; }
        public int MaxMana { get; set; } = 100;
        public int CurrentMana { get; set; }

        // Conquer Online specific stats
        public int Strength { get; set; } = 10;
        public int Agility { get; set; } = 10;
        public int Vitality { get; set; } = 10;
        public int Spirit { get; set; } = 10;

        protected override MapObjectType GetObjectType()
        {
            return MapObjectType.Player;
        }
    }

}
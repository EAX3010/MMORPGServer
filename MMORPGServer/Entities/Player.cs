using MMORPGServer.Enums;
using MMORPGServer.Repositories;

namespace MMORPGServer.Entities
{
    public class Player : MapObject
    {
        private IGameClient _gameClient { get; set; }
        public Player(IGameClient gameClient)
        {
            ObjectId = 1000000;
            _gameClient = gameClient;
            MapId = 1002; // Default map ID
            Position = new Position(300, 300);
        }
        public int Name { get; set; }
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
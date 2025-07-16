using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Database;

namespace MMORPGServer.Entities
{
    public class Player : MapObject, IPlayerNetworkContext
    {
        public Player(int id)
        {
            Id = id;

        }
        public static Player Create(int id, string name, short body,
        ClassType @class, Map map, uint createdFingerPrint, string createdAtMacAddress)
        {
            return new Player(id)
            {
                Name = name,
                Level = 1,
                Body = body,
                Class = @class,
                Map = map,
                Position = map.SpawnPoint(),
                CreatedFingerPrint = createdFingerPrint,
                CreatedAtMacAddress = createdAtMacAddress,
                Face = 1,
                LastLogin = DateTime.UtcNow,
            };

        }
        #region Variables
        public string Name { get; set; } = String.Empty;
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;
        public short Body { get; set; } = 0;
        public int Hair { get; set; } = 0;
        public int Face { get; set; } = 0;
        public int TransformationID { get; set; } = 0;

        public ClassType Class { get; set; } = 0;
        public int ClassLevel { get; set; } = 0;

        public long Gold { get; set; } = 0;
        public int ConquerPoints { get; set; } = 0;
        public int BoundConquerPoints { get; set; } = 0;

        // Stats
        public int MaxHealth { get; set; } = 1;
        public int CurrentHealth { get; set; } = 1;
        public int MaxMana { get; set; } = 1;
        public int CurrentMana { get; set; } = 1;
        public short Strength { get; set; } = 0;
        public short Agility { get; set; } = 0;
        public short Vitality { get; set; } = 0;
        public short Spirit { get; set; } = 0;
        public string CreatedAtMacAddress { get; set; } = "";
        public uint CreatedFingerPrint { get; set; } = 0;
        public DateTime LastLogin { get; set; }
        public int Mesh => TransformationID * 10000000 + Face * 10000 + Body;
        #endregion
        public bool UpdateAllotPoints()
        {
            Database.Models.PointAllotData? points = RepositoryManager.PointAllotReader?[this.Class, this.Level];
            if (points != null)
            {
                Strength = points.Strength;
                Agility = points.Agility;
                Vitality = points.Vitality;
                Spirit = points.Spirit;
                return true;
            }
            return false;
        }
        protected override MapObjectType GetObjectType()
        {
            return MapObjectType.Player;
        }
    }
}
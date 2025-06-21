using MMORPGServer.Domain.Common;

namespace MMORPGServer.Domain.Persistence
{
    public class PlayerEntity : BaseEntity
    {
        public uint Id { get; set; }
        public string Name { get; set; } = default!;
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;

        // Position
        public ushort MapId { get; set; } = 1001;
        public ushort X { get; set; }
        public ushort Y { get; set; }


        // Resources
        public long Gold { get; set; } = 0;

        // Player-specific timestamps
        public DateTime? LastLogin { get; set; }
        public DateTime? LastLogout { get; set; }
        public static PlayerEntity Create(uint id, string name, int level, long Experience,
           ushort mapId, ushort x, ushort y, long gold)
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
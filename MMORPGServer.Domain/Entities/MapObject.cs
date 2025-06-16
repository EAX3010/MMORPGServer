using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Domain.Entities
{
    public abstract partial class MapObject
    {
        public uint IndexID { get; set; }
        public uint ObjectId { get; set; }
        public uint MapId { get; set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Position Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

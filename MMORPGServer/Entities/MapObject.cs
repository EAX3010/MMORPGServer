using MMORPGServer.Enums;
using MMORPGServer.ValueObjects;

namespace MMORPGServer.Entities
{
    public abstract partial class MapObject
    {
        public uint IndexID { get; set; }
        public uint ObjectId { get; set; }
        public uint MapId { get; set; }
        public uint MapDynamicId { get; set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Position Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

using MMORPGServer.Game.World.Spatial;

namespace MMORPGServer.Game.Entities.Roles
{
    public abstract partial class MapObject : ISpatialObject
    {
        public uint IndexID { get; set; }
        public uint ObjectId { get; set; }
        public uint MapId { get; set; }
        public uint MapDynamicId { get; set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Vector2 Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

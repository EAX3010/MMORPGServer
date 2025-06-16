using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Domain.Entities
{
    public abstract partial class MapObject
    {
        public uint IndexID { get; set; }
        public uint ObjectId { get; set; }
        private Map _map;

        public Map Map
        {
            get => _map;
            set
            {
                _map = value;
                MapId = value?.Id ?? 0;
            }
        }

        public ushort MapId { get; private set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Position Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

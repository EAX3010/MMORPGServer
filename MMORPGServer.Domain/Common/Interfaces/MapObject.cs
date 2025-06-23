using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Domain.Common.Interfaces
{
    public abstract partial class MapObject
    {
        public int IndexID { get; set; }
        public int Id { get; set; }
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

        public short MapId { get; set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Position Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

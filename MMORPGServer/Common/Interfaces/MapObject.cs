using MMORPGServer.Common.Enums;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;

namespace MMORPGServer.Common.Interfaces
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
                MapId = value?.Configuration.Id ?? 0;
            }
        }

        public int MapId { get; set; }
        public bool IsActive { get; set; }
        public MapObjectType ObjectType { get; set; }
        public Position Position { get; set; }

        protected abstract MapObjectType GetObjectType();
    }
}

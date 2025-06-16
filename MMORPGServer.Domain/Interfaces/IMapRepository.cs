using MMORPGServer.Domain.Entities;

namespace MMORPGServer.Domain.Interfaces
{
    public interface IMapRepository
    {
        Task<Map> GetMapAsync(ushort mapId);
        Task<IEnumerable<Map>> GetAllMapsAsync();
        Task<bool> SaveMapAsync(Map map);
        Task InitializeMapsAsync();
    }
}
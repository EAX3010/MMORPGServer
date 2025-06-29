using MMORPGServer.Entities;

namespace MMORPGServer.Common.Interfaces
{
    public interface IMapRepository
    {
        Task<Map> GetMapAsync(short mapId);
        Task<IEnumerable<Map>> GetAllMapsAsync();
        Task<bool> SaveMapAsync(Map map);
        Task InitializeMapsAsync();
    }
}
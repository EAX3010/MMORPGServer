
namespace MMORPGServer.Domain.Repositories
{
    public interface IMapRepository
    {
        Task<Map> GetMapAsync(ushort mapId);
        Task<IEnumerable<Map>> GetAllMapsAsync();
        Task<bool> SaveMapAsync(Map map);
        Task<bool> DeleteMapAsync(ushort mapId);
        Task<Map> CreateMapAsync(ushort mapId, string name, int width, int height);
        Task<Map> LoadMapDataAsync(ushort mapId, string fileName);
        Task<Position?> GetValidSpawnPointAsync(Map map);
    }
}
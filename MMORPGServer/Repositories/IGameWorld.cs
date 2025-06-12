using MMORPGServer.Entities;
using MMORPGServer.ValueObjects;

namespace MMORPGServer.Repositories
{
    public interface IGameWorld
    {
        Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(uint playerId, float range);
        Task<bool> LoadMapAsync(ushort mapId, string fileName);
        Task<bool> MovePlayerAsync(uint playerId, Position newPosition);
        Task<Player> SpawnPlayerAsync(IGameClient client, ushort mapId);
        Task<bool> UnloadMapAsync(ushort mapId);
        void Update(float deltaTime);
    }
}
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;

namespace MMORPGServer.Common.Interfaces
{
    public interface IGameWorld
    {
        Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(int playerId, float range);
        Task<bool> MovePlayerAsync(int playerId, Position newPosition);
        Task<Player?> SpawnPlayerAsync(Player player, short mapId);
        Task UpdateAsync(float deltaTime);
    }
}
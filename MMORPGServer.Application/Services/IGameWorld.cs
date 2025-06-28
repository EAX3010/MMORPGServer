using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.ValueObjects;

namespace MMORPGServer.Application.Services
{
    public interface IGameWorld
    {
        Task<IEnumerable<MapObject>> GetEntitiesInRangeAsync(int playerId, float range);
        Task<bool> MovePlayerAsync(int playerId, Position newPosition);
        Task<Player?> SpawnPlayerAsync(Player player, short mapId);
        Task UpdateAsync(float deltaTime);
    }
}
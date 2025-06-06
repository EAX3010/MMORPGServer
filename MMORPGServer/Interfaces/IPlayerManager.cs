using MMORPGServer.Game.Entities.Roles;

namespace MMORPGServer.Interfaces
{
    public interface IPlayerManager
    {
        ValueTask<Player?> GetPlayerAsync(uint playerId);
        ValueTask AddPlayerAsync(Player player);
        ValueTask RemovePlayerAsync(uint playerId);
    }
}
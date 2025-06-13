using MMORPGServer.Game.Entities;

namespace MMORPGServer.Domain.Interfaces
{
    public interface IPlayerManager
    {
        ConcurrentDictionary<uint, Player> GetPlayers();
        ValueTask<Player?> GetPlayerAsync(uint playerId);
        ValueTask AddPlayerAsync(Player player);
        ValueTask RemovePlayerAsync(uint playerId);
    }
}
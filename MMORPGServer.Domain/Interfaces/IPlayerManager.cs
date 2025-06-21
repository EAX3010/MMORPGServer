using MMORPGServer.Domain.Entities;
using System.Collections.Concurrent;

namespace MMORPGServer.Domain.Interfaces
{
    public interface IPlayerManager
    {
        ConcurrentDictionary<int, Player> GetPlayers();
        ValueTask<Player?> GetPlayerAsync(int playerId);
        ValueTask AddPlayerAsync(Player player);
        ValueTask RemovePlayerAsync(int playerId);
    }
}
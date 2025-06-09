namespace MMORPGServer.Interfaces
{
    public interface IPlayerManager
    {
        ConcurrentDictionary<uint, Player> GetPlayers();
        ValueTask<Player?> GetPlayerAsync(uint playerId);
        ValueTask AddPlayerAsync(Player player);
        ValueTask RemovePlayerAsync(uint playerId);
    }
}
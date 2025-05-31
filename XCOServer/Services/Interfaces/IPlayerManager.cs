namespace MMORPGServer.Services.Interfaces
{
    public interface IPlayerManager
    {
        ValueTask<IPlayer?> GetPlayerAsync(uint playerId);
        ValueTask AddPlayerAsync(IPlayer player);
        ValueTask RemovePlayerAsync(uint playerId);
        ValueTask<IReadOnlyList<IPlayer>> GetPlayersInMapAsync(uint mapId);
        ValueTask<int> GetOnlinePlayerCountAsync();
    }
}
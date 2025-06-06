using MMORPGServer.Game.Entities.Roles;

namespace MMORPGServer.Services
{
    public sealed class PlayerManager : IPlayerManager
    {
        private readonly ConcurrentDictionary<uint, Player> _players = new();

        public ValueTask<Player?> GetPlayerAsync(uint playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return ValueTask.FromResult(player);
        }

        public ValueTask AddPlayerAsync(Player player)
        {
            _players.TryAdd(player.Id, player);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(uint playerId)
        {
            _players.TryRemove(playerId, out _);
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> GetOnlinePlayerCountAsync()
        {
            return ValueTask.FromResult(_players.Count);
        }
    }
}
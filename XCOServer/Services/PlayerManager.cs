namespace MMORPGServer.Services
{
    public sealed class PlayerManager : IPlayerManager
    {
        private readonly ConcurrentDictionary<uint, IPlayer> _players = new();

        public ValueTask<IPlayer?> GetPlayerAsync(uint playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return ValueTask.FromResult(player);
        }

        public ValueTask AddPlayerAsync(IPlayer player)
        {
            _players.TryAdd(player.CharacterId, player);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(uint playerId)
        {
            _players.TryRemove(playerId, out _);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<IPlayer>> GetPlayersInMapAsync(uint mapId)
        {
            var playersInMap = _players.Values
                .Where(p => p.MapId == mapId)
                .ToList();

            return ValueTask.FromResult<IReadOnlyList<IPlayer>>(playersInMap);
        }

        public ValueTask<int> GetOnlinePlayerCountAsync()
        {
            return ValueTask.FromResult(_players.Count);
        }
    }
}
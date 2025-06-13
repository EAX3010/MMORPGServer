using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Game.Entities;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MMORPGServer.Services
{
    public sealed class PlayerManager : IPlayerManager
    {
        public ConcurrentDictionary<uint, Player> _players = [];

        public ValueTask<Player> GetPlayerAsync(uint playerId)
        {
            _players.TryGetValue(playerId, out Player player);
            return ValueTask.FromResult(player);
        }

        public ValueTask AddPlayerAsync(Player player)
        {
            _players.TryAdd(player.ObjectId, player);
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

        public ConcurrentDictionary<uint, Player> GetPlayers()
        {
            return _players;
        }
    }
}
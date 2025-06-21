using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.Interfaces;
using System.Collections.Concurrent;

namespace MMORPGServer.Application.Services
{
    public sealed class PlayerManager : IPlayerManager
    {
        public ConcurrentDictionary<int, Player> _players = [];

        public ValueTask<Player> GetPlayerAsync(int playerId)
        {
            _players.TryGetValue(playerId, out Player player);
            return ValueTask.FromResult(player);
        }

        public ValueTask AddPlayerAsync(Player player)
        {
            _players.TryAdd(player.Id, player);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemovePlayerAsync(int playerId)
        {
            _players.TryRemove(playerId, out _);
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> GetOnlinePlayerCountAsync()
        {
            return ValueTask.FromResult(_players.Count);
        }

        public ConcurrentDictionary<int, Player> GetPlayers()
        {
            return _players;
        }
    }
}
using MMORPGServer.Database;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Services
{
    public class PlayerManager
    {
        public PlayerManager()
        {
            Log.Debug("PlayerManager initialized");
        }
        private readonly ConcurrentDictionary<int, Player> _players = new();


        public async Task<bool> UpdatePlayerAsync(Player player)
        {
            try
            {
                var success = await RepositoryManager.PlayerRepository?.UpdateAsync(player)!;
                if (success)
                {
                    Log.Information("Updated player {PlayerName} (ID: {PlayerId}) in database", player.Name, player.Id);
                }
                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update player {PlayerName} (ID: {PlayerId})", player.Name, player.Id);
                return false;
            }
        }
        public async Task<bool> SavePlayerAsync(Player player)
        {
            try
            {
                var success = await RepositoryManager.PlayerRepository?.SaveAsync(player)!;
                if (success)
                {
                    Log.Information("Saved new player {PlayerName} (ID: {PlayerId}) to database", player.Name, player.Id);
                }
                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save player {PlayerName} (ID: {PlayerId})", player.Name, player.Id);
                return false;
            }
        }
        public async Task<Player?> LoadPlayerAsync(int playerId)
        {
            try
            {
                // Repository returns Domain Player, handles database mapping internally
                var player = await RepositoryManager.PlayerRepository.GetByIdAsync(playerId);
                if (player != null)
                {
                    Log.Information("Loaded player {PlayerName} (ID: {PlayerId}) from database", player.Name, playerId);
                }
                else
                {
                    Log.Debug("Player with ID {PlayerId} not found in database", playerId);
                }

                return player;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load player {PlayerId}", playerId);
                return null;
            }
        }
        public async ValueTask<Player?> GetPlayerAsync(int playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public async ValueTask<bool> AddPlayerAsync(Player player)
        {
            if (_players.TryAdd(player.Id, player))
            {
                Log.Debug("Added player {PlayerName} (ID: {PlayerId}) to in-memory cache", player.Name, player.Id);
                return true;
            }
            return false;
        }

        public async ValueTask<bool> RemovePlayerAsync(int playerId)
        {
            if (_players.TryRemove(playerId, out var player))
            {
                Log.Debug("Removed player {PlayerName} (ID: {PlayerId}) from in-memory cache", player.Name, player.Id);
                return true;
            }
            return false;
        }

        public async ValueTask<int> GetOnlinePlayerCountAsync()
        {
            return _players.Count;
        }

        public ConcurrentDictionary<int, Player> GetPlayers()
        {
            return _players;
        }

        public async ValueTask<IEnumerable<Player>> GetPlayersByMapAsync(short mapId)
        {
            var playersOnMap = _players.Values.Where(p => p.MapId == mapId);
            return playersOnMap;
        }

        public async ValueTask<bool> IsPlayerOnlineAsync(int playerId)
        {
            return _players.ContainsKey(playerId);
        }
    }
}


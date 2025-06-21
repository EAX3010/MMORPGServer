using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces.Repositories;
using MMORPGServer.Domain.Persistence;

namespace MMORPGServer.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Repository implementation for Player entities.
    /// Provides game-specific query methods and optimizations.
    /// </summary>
    public sealed class PlayerRepository : RepositoryBase<PlayerEntity, int>, IPlayerRepository
    {
        public PlayerRepository(GameDbContext context, ILogger<PlayerRepository> logger)
            : base(context, logger)
        {
        }

        public async Task<PlayerEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return await DbSet
                .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), cancellationToken);
        }

        public async Task<IReadOnlyList<PlayerEntity>> GetPlayersByLevelRangeAsync(
            int minLevel,
            int maxLevel,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(p => p.Level >= minLevel && p.Level <= maxLevel)
                .OrderByDescending(p => p.Level)
                .ThenByDescending(p => p.Experience)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PlayerEntity>> GetRecentlyActivePlayersAsync(
            TimeSpan timeSpan,
            CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - timeSpan;

            return await DbSet
                .Where(p => p.LastLogin >= cutoffTime)
                .OrderByDescending(p => p.LastLogin)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PlayerEntity>> GetPlayersByMapAsync(
            short mapId,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(p => p.MapId == mapId)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return !await DbSet
                .AnyAsync(p => p.Name.ToLower() == name.ToLower(), cancellationToken);
        }

        public async Task<IReadOnlyList<PlayerEntity>> GetTopPlayersByExperienceAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .OrderByDescending(p => p.Experience)
                .ThenByDescending(p => p.Level)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateLastLoginAsync(int playerId, CancellationToken cancellationToken = default)
        {
            await DbSet
                .Where(p => p.Id == playerId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastLogin, DateTime.UtcNow), cancellationToken);
        }

        public async Task UpdateLastLogoutAsync(int playerId, CancellationToken cancellationToken = default)
        {
            await DbSet
                .Where(p => p.Id == playerId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastLogout, DateTime.UtcNow), cancellationToken);
        }

        // === Optimized Query Methods ===

        /// <summary>
        /// Gets player basic info for leaderboard display (optimized query).
        /// </summary>
        public async Task<IReadOnlyList<object>> GetPlayerLeaderboardAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .OrderByDescending(p => p.Experience)
                .Take(count)
                .Select(p => new { p.Id, p.Name, p.Level, p.Experience })
                .ToListAsync<object>(cancellationToken);
        }

        /// <summary>
        /// Gets only player names and IDs for dropdown lists (memory efficient).
        /// </summary>
        public async Task<Dictionary<int, string>> GetPlayerNamesAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        }
    }
}

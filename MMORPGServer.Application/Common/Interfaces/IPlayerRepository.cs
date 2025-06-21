using MMORPGServer.Domain.Persistence;

namespace MMORPGServer.Application.Common.Interfaces.Repositories
{
    /// <summary>
    /// Specialized repository interface for Player entities.
    /// Provides game-specific query methods.
    /// </summary>
    public interface IPlayerRepository : IRepository<PlayerEntity, int>
    {
        /// <summary>
        /// Gets a player by their name (case-insensitive).
        /// </summary>
        Task<PlayerEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets players by their level range.
        /// </summary>
        Task<IReadOnlyList<PlayerEntity>> GetPlayersByLevelRangeAsync(
            int minLevel,
            int maxLevel,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recently active players (logged in within specified timespan).
        /// </summary>
        Task<IReadOnlyList<PlayerEntity>> GetRecentlyActivePlayersAsync(
            TimeSpan timeSpan,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets players on a specific map.
        /// </summary>
        Task<IReadOnlyList<PlayerEntity>> GetPlayersByMapAsync(
            short mapId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a player name is available (not taken).
        /// </summary>
        Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets top players by experience.
        /// </summary>
        Task<IReadOnlyList<PlayerEntity>> GetTopPlayersByExperienceAsync(
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates player's last login timestamp.
        /// </summary>
        Task UpdateLastLoginAsync(int playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates player's last logout timestamp.
        /// </summary>
        Task UpdateLastLogoutAsync(int playerId, CancellationToken cancellationToken = default);
    }
}
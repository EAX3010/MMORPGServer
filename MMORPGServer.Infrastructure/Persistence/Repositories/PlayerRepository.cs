using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Infrastructure.Persistence.Mappings;

namespace MMORPGServer.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Repository implementation for Player entities.
    /// Provides game-specific query methods and optimizations.
    /// </summary>
    public sealed class PlayerRepository : IPlayerRepository
    {
        private readonly GameDbContext _context;
        private readonly ILogger<PlayerRepository> _logger;

        public PlayerRepository(GameDbContext context, ILogger<PlayerRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves a player by their unique identifier.
        /// </summary>
        /// <param name="id">The player's unique identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The player if found, otherwise null</returns>
        public async Task<Player?> GetByIdAsync(uint id, CancellationToken cancellationToken = default)
        {
            if (id == 0)
            {
                _logger.LogWarning("Attempted to retrieve player with invalid ID: {PlayerId}", id);
                return null;
            }

            try
            {
                _logger.LogDebug("Retrieving player with ID: {PlayerId}", id);

                var playerEntity = await _context.Players
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (playerEntity == null)
                {
                    _logger.LogDebug("Player not found with ID: {PlayerId}", id);
                    return null;
                }

                var player = playerEntity.ToGameObject();
                _logger.LogDebug("Successfully retrieved player: {PlayerId} - {PlayerName}", id, player.Name);

                return player;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Player retrieval cancelled for ID: {PlayerId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving player with ID: {PlayerId}", id);
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a player in the database.
        /// </summary>
        /// <param name="player">The player to upsert</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of affected rows</returns>
        public async Task<bool> UpsertPlayerAsync(Player player, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!player.IsDirty)
            {
                _logger.LogDebug("player: {PlayerId} is not dirty", player.Id);
            }
            try
            {
                _logger.LogDebug("Upserting player: {PlayerId} - {PlayerName}", player.Id, player.Name);

                var dbPlayer = player.ToDatabaseObject();

                // Check if player exists
                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == player.Id, cancellationToken);

                if (existingPlayer != null)
                {
                    _logger.LogDebug("Updating existing player: {PlayerId}", player.Id);
                    _context.Entry(existingPlayer).CurrentValues.SetValues(dbPlayer);
                }
                else
                {
                    _logger.LogDebug("Creating new player: {PlayerId}", player.Id);
                    await _context.Players.AddAsync(dbPlayer, cancellationToken);
                }

                var result = await _context.SaveChangesAsync(cancellationToken);
                if (result > 0)
                {
                    player.IsDirty = false; // Reset dirty flag after successful save
                    return true;

                }
                _logger.LogInformation("Successfully upserted player: {PlayerId} - {PlayerName}, Affected rows: {AffectedRows}",
                    player.Id, player.Name, result);

                return false;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while upserting player: {PlayerId}", player.Id);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error while upserting player: {PlayerId}", player.Id);
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Player upsert cancelled: {PlayerId}", player.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error upserting player: {PlayerId}", player.Id);
                throw;
            }
        }

        /// <summary>
        /// Checks if a player name is available for use.
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <param name="excludePlayerId">Optional player ID to exclude from the check (for updates)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the name is available, false otherwise</returns>
        public async Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogDebug("Name availability check failed: name is null or whitespace");
                return false;
            }

            // Validate name length and characters
            if (name.Length < 3 || name.Length > 20)
            {
                _logger.LogDebug("Name availability check failed: invalid length {NameLength}", name.Length);
                return false;
            }

            try
            {
                _logger.LogDebug("Checking name availability: {PlayerName}", name);

                var query = _context.Players
                    .AsNoTracking()
                    .Where(p => String.Equals(p.Name.ToLower(), name.ToLower(), StringComparison.CurrentCulture) == false);


                var isAvailable = !await query.AnyAsync(cancellationToken);

                _logger.LogDebug("Name availability check result for {PlayerName}: {IsAvailable}", name, isAvailable);

                return isAvailable;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Name availability check cancelled for: {PlayerName}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking name availability for: {PlayerName}", name);
                throw;
            }
        }
    }
}
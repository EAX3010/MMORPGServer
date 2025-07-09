using Microsoft.EntityFrameworkCore;
using MMORPGServer.Database.Mappings;
using MMORPGServer.Entities;
using Serilog;

namespace MMORPGServer.Database.Repositories
{
    /// <summary>
    /// Repository implementation for Player entities.
    /// Provides game-specific query methods and optimizations.
    /// </summary>
    public class SqlPlayerRepository
    {
        private readonly GameDbContext _context;

        public SqlPlayerRepository(GameDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Retrieves a player by their unique identifier.
        /// </summary>
        /// <param name="id">The player's unique identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The player if found, otherwise null</returns>
        public async Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id == 0)
            {
                Log.Warning("Attempted to retrieve player with invalid ID: {PlayerId}", id);
                return null;
            }

            try
            {
                Log.Debug("Retrieving player with ID: {PlayerId}", id);

                var playerEntity = await _context.Players
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (playerEntity == null)
                {
                    Log.Debug("Player not found with ID: {PlayerId}", id);
                    return null;
                }

                var player = playerEntity.ToGameObject();
                Log.Debug("Successfully retrieved player: {PlayerId} - {PlayerName}", id, player.Name);

                return player;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player retrieval cancelled for ID: {PlayerId}", id);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving player with ID: {PlayerId}", id);
                throw;
            }
        }

        /// <summary>
        /// Checks if a player exists by their unique identifier.
        /// </summary>
        /// <param name="id">The player's unique identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the player exists, false otherwise</returns>
        public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id == 0)
            {
                Log.Debug("Player existence check failed: invalid ID {PlayerId}", id);
                return false;
            }

            try
            {
                Log.Debug("Checking if player exists with ID: {PlayerId}", id);

                var exists = await _context.Players
                    .AsNoTracking()
                    .AnyAsync(p => p.Id == id, cancellationToken);

                Log.Debug("Player existence check result for ID {PlayerId}: {Exists}", id, exists);

                return exists;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player existence check cancelled for ID: {PlayerId}", id);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking player existence for ID: {PlayerId}", id);
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
                Log.Debug("player: {PlayerId} is not dirty", player.Id);
            }
            try
            {
                Log.Debug("Upserting player: {PlayerId} - {PlayerName}", player.Id, player.Name);

                var dbPlayer = player.ToDatabaseObject();

                // Check if player exists
                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == player.Id, cancellationToken);

                if (existingPlayer != null)
                {
                    Log.Debug("Updating existing player: {PlayerId}", player.Id);
                    _context.Entry(existingPlayer).CurrentValues.SetValues(dbPlayer);
                }
                else
                {
                    Log.Debug("Creating new player: {PlayerId}", player.Id);
                    await _context.Players.AddAsync(dbPlayer, cancellationToken);
                }

                var result = await _context.SaveChangesAsync(cancellationToken);
                if (result > 0)
                {
                    player.IsDirty = false; // Reset dirty flag after successful save
                    return true;

                }
                Log.Information("Successfully upserted player: {PlayerId} - {PlayerName}, Affected rows: {AffectedRows}",
                    player.Id, player.Name, result);

                return false;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Log.Warning(ex, "Concurrency conflict while upserting player: {PlayerId}", player.Id);
                throw;
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "Database update error while upserting player: {PlayerId}", player.Id);
                throw;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player upsert cancelled: {PlayerId}", player.Id);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error upserting player: {PlayerId}", player.Id);
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
                Log.Debug("Name availability check failed: name is null or whitespace");
                return false;
            }

            // Validate name length and characters
            if (name.Length < 3 || name.Length > 20)
            {
                Log.Debug("Name availability check failed: invalid length {NameLength}", name.Length);
                return false;
            }

            try
            {
                Log.Debug("Checking name availability: {PlayerName}", name);

                var query = _context.Players
                    .AsNoTracking()
                    .Where(p => p.Name.ToLower() == name.ToLower());


                var isAvailable = !await query.AnyAsync(cancellationToken);

                Log.Debug("Name availability check result for {PlayerName}: {IsAvailable}", name, isAvailable);

                return isAvailable;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Name availability check cancelled for: {PlayerName}", name);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking name availability for: {PlayerName}", name);
                throw;
            }
        }
    }
}
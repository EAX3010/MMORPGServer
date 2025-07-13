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
        /// Creates a new player in the database.
        /// </summary>
        /// <param name="player">The player to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the player was created successfully, false otherwise</returns>
        public async Task<bool> SaveAsync(Player player, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(player);

            try
            {
                Log.Debug("Creating new player: {PlayerName}", player.Name);

                var dbPlayer = player.ToDatabaseObject();

                await _context.Players.AddAsync(dbPlayer, cancellationToken);
                var result = await _context.SaveChangesAsync(cancellationToken);

                if (result > 0)
                {
                    player.IsDirty = false;

                    Log.Information("Successfully created player: {PlayerId} - {PlayerName}",
                        player.Id, player.Name);
                    return true;
                }

                Log.Warning("Failed to create player: {PlayerName}", player.Name);
                return false;
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "Database update error while creating player: {PlayerName}", player.Name);
                throw;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player creation cancelled: {PlayerName}", player.Name);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating player: {PlayerName}", player.Name);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing player in the database.
        /// </summary>
        /// <param name="player">The player to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the player was updated successfully, false otherwise</returns>
        public async Task<bool> UpdateAsync(Player player, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Id == 0)
            {
                Log.Warning("Attempted to update player with invalid ID: {PlayerId}", player.Id);
                return false;
            }

            if (!player.IsDirty)
            {
                Log.Debug("Player: {PlayerId} is not dirty, skipping update", player.Id);
                return true;
            }

            try
            {
                Log.Debug("Updating player: {PlayerId} - {PlayerName}", player.Id, player.Name);

                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == player.Id, cancellationToken);

                if (existingPlayer == null)
                {
                    Log.Warning("Player not found for update: {PlayerId}", player.Id);
                    return false;
                }

                var dbPlayer = player.ToDatabaseObject();
                _context.Entry(existingPlayer).CurrentValues.SetValues(dbPlayer);

                var result = await _context.SaveChangesAsync(cancellationToken);

                if (result > 0)
                {
                    player.IsDirty = false;
                    Log.Information("Successfully updated player: {PlayerId} - {PlayerName}, Affected rows: {AffectedRows}",
                        player.Id, player.Name, result);
                    return true;
                }

                Log.Warning("No rows affected when updating player: {PlayerId}", player.Id);
                return false;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Log.Warning(ex, "Concurrency conflict while updating player: {PlayerId}", player.Id);
                throw;
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "Database update error while updating player: {PlayerId}", player.Id);
                throw;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player update cancelled: {PlayerId}", player.Id);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error updating player: {PlayerId}", player.Id);
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
        public async Task<bool> IsNameAvailableAsync(string name, int? excludePlayerId = null,
            CancellationToken cancellationToken = default)
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
                Log.Debug("Checking name availability: {PlayerName}, ExcludePlayerId: {ExcludePlayerId}",
                    name, excludePlayerId);

                var query = _context.Players
                    .AsNoTracking()
                    .Where(p => p.Name.ToLower() == name.ToLower());

                if (excludePlayerId.HasValue)
                {
                    query = query.Where(p => p.Id != excludePlayerId.Value);
                }

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

        /// <summary>
        /// Deletes a player from the database.
        /// </summary>
        /// <param name="playerId">The player's unique identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the player was deleted successfully, false otherwise</returns>
        public async Task<bool> DeleteAsync(int playerId, CancellationToken cancellationToken = default)
        {
            if (playerId == 0)
            {
                Log.Warning("Attempted to delete player with invalid ID: {PlayerId}", playerId);
                return false;
            }

            try
            {
                Log.Debug("Deleting player: {PlayerId}", playerId);

                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);

                if (existingPlayer == null)
                {
                    Log.Warning("Player not found for deletion: {PlayerId}", playerId);
                    return false;
                }

                _context.Players.Remove(existingPlayer);
                var result = await _context.SaveChangesAsync(cancellationToken);

                if (result > 0)
                {
                    Log.Information("Successfully deleted player: {PlayerId}", playerId);
                    return true;
                }

                Log.Warning("No rows affected when deleting player: {PlayerId}", playerId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "Database update error while deleting player: {PlayerId}", playerId);
                throw;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Player deletion cancelled: {PlayerId}", playerId);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error deleting player: {PlayerId}", playerId);
                throw;
            }
        }
    }

}
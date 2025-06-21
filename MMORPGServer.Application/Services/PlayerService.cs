using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Common.Interfaces;
using MMORPGServer.Domain.Persistence;

public class PlayerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(IUnitOfWork unitOfWork, ILogger<PlayerService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new player with validation.
    /// </summary>
    public async Task<PlayerEntity> CreatePlayerAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Check if name is available
            if (!await _unitOfWork.Players.IsNameAvailableAsync(name, cancellationToken))
            {
                throw new InvalidOperationException($"Player name '{name}' is already taken");
            }

            // Create new player
            var player = PlayerEntity.Create(0, name, 1, 0, 1001, 300, 300, 1000);

            // Add to repository
            await _unitOfWork.Players.AddAsync(player, cancellationToken);

            _logger.LogInformation("Created new player: {PlayerName}", name);
            return player;

        }, cancellationToken);
    }

    /// <summary>
    /// Updates player experience and level.
    /// </summary>
    public async Task UpdatePlayerExperienceAsync(int playerId, long experience, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var player = await _unitOfWork.Players.GetByIdAsync(playerId, cancellationToken);
            if (player == null)
            {
                throw new InvalidOperationException($"Player with ID {playerId} not found");
            }

            player.Experience += experience;

            // Level up logic (simplified)
            var newLevel = CalculateLevel(player.Experience);
            if (newLevel > player.Level)
            {
                player.Level = newLevel;
                _logger.LogInformation("Player {PlayerName} leveled up to {Level}", player.Name, newLevel);
            }

            _unitOfWork.Players.Update(player);
            _logger.LogDebug("Updated experience for player {PlayerId}: +{Experience}", playerId, experience);

        }, cancellationToken);
    }

    private static int CalculateLevel(long experience)
    {
        // Simplified level calculation
        return (int)Math.Sqrt(experience / 100) + 1;
    }
}
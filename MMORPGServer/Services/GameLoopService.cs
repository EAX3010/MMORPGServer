
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMORPGServer.Common.ValueObjects;
using System.Threading.Channels;

namespace MMORPGServer.Services
{
    public class GameLoopService : BackgroundService
    {
        private readonly ILogger<GameLoopService> _logger;
        private readonly IGameWorld _gameWorld;
        private readonly Channel<GameAction> _actionChannel;
        private const float TARGET_FPS = 60.0f;
        private const float TARGET_FRAME_TIME = 1.0f / TARGET_FPS;

        public GameLoopService(ILogger<GameLoopService> logger, IGameWorld gameWorld)
        {
            _logger = logger;
            _gameWorld = gameWorld;
            _actionChannel = Channel.CreateUnbounded<GameAction>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Game loop service is starting");

            DateTime lastUpdateTime = DateTime.UtcNow;
            float accumulator = 0.0f;

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime currentTime = DateTime.UtcNow;
                float deltaTime = (float)(currentTime - lastUpdateTime).TotalSeconds;
                lastUpdateTime = currentTime;

                // Process any pending actions
                while (_actionChannel.Reader.TryRead(out GameAction action))
                {
                    ProcessAction(action);
                }

                // Fixed time step update
                accumulator += deltaTime;
                while (accumulator >= TARGET_FRAME_TIME)
                {
                    Update(TARGET_FRAME_TIME);
                    accumulator -= TARGET_FRAME_TIME;
                }

                // Cap the frame rate
                float elapsed = (float)(DateTime.UtcNow - currentTime).TotalSeconds;
                if (elapsed < TARGET_FRAME_TIME)
                {
                    await Task.Delay((int)((TARGET_FRAME_TIME - elapsed) * 1000), stoppingToken);
                }
            }

            _logger.LogInformation("Game loop service is stopping");
        }

        private void Update(float deltaTime)
        {
            try
            {
                _gameWorld.UpdateAsync(deltaTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game world update");
            }
        }

        public async ValueTask QueueAction(GameAction action)
        {
            await _actionChannel.Writer.WriteAsync(action);
        }

        private void ProcessAction(GameAction action)
        {
            try
            {
                switch (action)
                {
                    case PlayerMoveAction moveAction:
                        ProcessPlayerMove(moveAction);
                        break;
                    case PlayerAttackAction attackAction:
                        ProcessPlayerAttack(attackAction);
                        break;
                    case PlayerCastAction castAction:
                        ProcessPlayerCast(castAction);
                        break;
                    default:
                        _logger.LogWarning("Unknown action type: {ActionType}", action.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing game action");
            }
        }

        private void ProcessPlayerMove(PlayerMoveAction action)
        {
            // Implement player movement logic
        }

        private void ProcessPlayerAttack(PlayerAttackAction action)
        {
            // Implement player attack logic
        }

        private void ProcessPlayerCast(PlayerCastAction action)
        {
            // Implement player spell casting logic
        }
    }

    public abstract class GameAction
    {
        public int PlayerId { get; set; }
    }

    public class PlayerMoveAction : GameAction
    {
        public Position TargetPosition { get; set; }
    }

    public class PlayerAttackAction : GameAction
    {
        public int TargetId { get; set; }
    }

    public class PlayerCastAction : GameAction
    {
        public short SkillId { get; set; }
        public int? TargetId { get; set; }
        public Position? TargetPosition { get; set; }
    }
}
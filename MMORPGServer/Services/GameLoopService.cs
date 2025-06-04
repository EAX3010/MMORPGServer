namespace MMORPGServer.Services
{
    public sealed class GameLoopService : BackgroundService
    {
        private readonly ILogger<GameLoopService> _logger;
        private readonly IPlayerManager _playerManager;

        public GameLoopService(ILogger<GameLoopService> logger, IPlayerManager playerManager)
        {
            _logger = logger;
            _playerManager = playerManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Game loop service started - Tick rate: 10 FPS");

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessGameTickAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in game tick");
                }
            }

            _logger.LogInformation("Game loop service stopped");
        }

        private async ValueTask ProcessGameTickAsync()
        {
            var playerCount = await _playerManager.GetOnlinePlayerCountAsync();

            if (DateTime.UtcNow.Second % 30 == 0)
            {
                _logger.LogDebug("Game tick - Online players: {PlayerCount}", playerCount);
            }

            await ValueTask.CompletedTask;
        }
    }
}
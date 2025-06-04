namespace MMORPGServer.Services
{
    public sealed class GameServerHostedService : BackgroundService
    {
        private readonly IGameServer _gameServer;
        private readonly ILogger<GameServerHostedService> _logger;

        public GameServerHostedService(IGameServer gameServer, ILogger<GameServerHostedService> logger)
        {
            _gameServer = gameServer;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Conquer Online MMORPG Server...");

            try
            {
                // Initialize DH key parameters

                _logger.LogDebug("DH key parameters initialized");

                // Start the game server
                await _gameServer.StartAsync(cancellationToken);
                _logger.LogInformation("Game server started successfully");

                // Start the background service
                await base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to start game server");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Conquer Online MMORPG Server...");

            try
            {
                // Stop the background service first
                await base.StopAsync(cancellationToken);

                // Stop the game server
                await _gameServer.StopAsync(cancellationToken);
                _logger.LogInformation("Game server stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during server shutdown");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Game server hosted service is running");

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Game server hosted service cancelled");
            }
        }
    }
}
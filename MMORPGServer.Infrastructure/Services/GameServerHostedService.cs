using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Common.Interfaces;

namespace MMORPGServer.Infrastructure.Services
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
            try
            {
                _logger.LogInformation("Starting game server...");
                await _gameServer.StartAsync(cancellationToken);
                await base.StartAsync(cancellationToken);

                _logger.LogInformation("Game server fully operational!");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Critical failure during server startup");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Initiating server shutdown sequence...");

            try
            {
                _logger.LogInformation("Notifying connected clients...");
                await Task.Delay(1000, cancellationToken); // Give clients time to disconnect gracefully

                _logger.LogInformation("Stopping game server...");
                await base.StopAsync(cancellationToken);
                await _gameServer.StopAsync(cancellationToken);

                _logger.LogInformation("Saving server state...");
                await Task.Delay(500, cancellationToken);

                _logger.LogInformation("Server shutdown completed successfully");
                _logger.LogInformation("Thanks for playing!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graceful shutdown");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Game server monitoring service active");

            DateTime serverStartTime = DateTime.UtcNow;
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    TimeSpan uptime = DateTime.UtcNow - serverStartTime;
                    _logger.LogInformation("Server Status - Uptime: {Days}d {Hours}h {Minutes}m",
                        uptime.Days, uptime.Hours, uptime.Minutes);

                    // Log memory usage
                    long memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;
                    _logger.LogInformation("Memory Usage: {MemoryMB} MB", memoryUsage);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Game server monitoring service cancelled");
            }
        }
    }
}
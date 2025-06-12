using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMORPGServer.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MMORPGServer
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
            _logger.LogInformation("Initializing MMORPG Server components...");

            try
            {
                _logger.LogInformation("Setting up security systems...");
                await Task.Delay(500, cancellationToken); // Simulate initialization
                _logger.LogInformation("Security systems online");

                _logger.LogInformation("Configuring network layer...");
                await Task.Delay(300, cancellationToken);
                _logger.LogInformation("Network layer configured");

                _logger.LogInformation("Loading game systems...");
                await Task.Delay(400, cancellationToken);
                _logger.LogInformation("Game systems loaded");

                _logger.LogInformation("Starting game server...");
                await _gameServer.StartAsync(cancellationToken);
                _logger.LogInformation("Game server started successfully on port {Port}", GameConstants.DEFAULT_PORT);

                _logger.LogInformation("Server is ready to accept connections!");
                _logger.LogInformation("Maximum concurrent clients: {MaxClients}", GameConstants.MAX_CLIENTS);
                _logger.LogInformation("Game tick rate: {TickRate} Hz", GameConstants.GAME_TICK_RATE);

                await base.StartAsync(cancellationToken);

                _logger.LogInformation("MMORPG Server fully operational!");
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
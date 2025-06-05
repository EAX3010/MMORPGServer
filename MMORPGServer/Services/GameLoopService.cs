using MMORPGServer.Interfaces;

namespace MMORPGServer.Services
{
    public sealed class GameLoopService : BackgroundService
    {
        private readonly ILogger<GameLoopService> _logger;
        private readonly IPlayerManager _playerManager;
        private long _tickCount = 0;
        private DateTime _lastStatsLog = DateTime.UtcNow;
        private readonly TimeSpan _statsInterval = TimeSpan.FromMinutes(5);

        public GameLoopService(ILogger<GameLoopService> logger, IPlayerManager playerManager)
        {
            _logger = logger;
            _playerManager = playerManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Game loop service started - Tick rate: {TickRate} FPS", GameConstants.GAME_TICK_RATE);
            _logger.LogInformation("High-performance game simulation active");

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000 / GameConstants.GAME_TICK_RATE));
            var gameStartTime = DateTime.UtcNow;

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessGameTickAsync();
                        _tickCount++;

                        // Log periodic statistics
                        if (DateTime.UtcNow - _lastStatsLog >= _statsInterval)
                        {
                            await LogGameStatisticsAsync(gameStartTime);
                            _lastStatsLog = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing game tick #{TickCount}", _tickCount);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Game loop service stopped gracefully");
            }
            finally
            {
                var runtime = DateTime.UtcNow - gameStartTime;
                _logger.LogInformation("Game loop statistics - Total ticks: {TotalTicks}, Runtime: {Runtime}",
                    _tickCount, runtime.ToString(@"hh\:mm\:ss"));
            }
        }

        private async ValueTask ProcessGameTickAsync()
        {
            var playerCount = await _playerManager.GetOnlinePlayerCountAsync();

            // Simulate game world updates
            if (_tickCount % (GameConstants.GAME_TICK_RATE * 30) == 0) // Every 30 seconds
            {
                if (playerCount > 0)
                {
                    _logger.LogInformation("World update #{WorldTick} - Active players: {PlayerCount}",
                        _tickCount / (GameConstants.GAME_TICK_RATE * 30), playerCount);
                }
            }

            // Log connection events
            if (_tickCount % GameConstants.GAME_TICK_RATE == 0) // Every second
            {
                if (playerCount > 0 && _tickCount % (GameConstants.GAME_TICK_RATE * 60) == 0) // Every minute with players
                {
                    _logger.LogDebug("Game tick #{TickCount} - Players online: {PlayerCount}", _tickCount, playerCount);
                }
            }

            await ValueTask.CompletedTask;
        }

        private async ValueTask LogGameStatisticsAsync(DateTime gameStartTime)
        {
            var playerCount = await _playerManager.GetOnlinePlayerCountAsync();
            var uptime = DateTime.UtcNow - gameStartTime;
            var averageTicksPerSecond = _tickCount / uptime.TotalSeconds;
            var memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;

            _logger.LogInformation("=== GAME SERVER STATISTICS ===");
            _logger.LogInformation("Server Uptime: {Uptime}", uptime.ToString(@"dd\.hh\:mm\:ss"));
            _logger.LogInformation("Total Game Ticks: {TotalTicks:N0}", _tickCount);
            _logger.LogInformation("Average TPS: {AverageTPS:F1}", averageTicksPerSecond);
            _logger.LogInformation("Online Players: {PlayerCount}", playerCount);
            _logger.LogInformation("Memory Usage: {MemoryMB} MB", memoryUsage);
            _logger.LogInformation("GC Collections: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}",
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            _logger.LogInformation("=====================================");

            // Trigger garbage collection if memory usage is high
            if (memoryUsage > 500) // 500 MB threshold
            {
                _logger.LogWarning("High memory usage detected, requesting garbage collection");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var newMemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;
                _logger.LogInformation("Garbage collection completed - Memory: {OldMB} MB -> {NewMB} MB",
                    memoryUsage, newMemoryUsage);
            }
        }
    }
}
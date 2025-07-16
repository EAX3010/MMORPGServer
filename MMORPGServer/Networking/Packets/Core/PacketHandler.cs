using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Middleware;
using Serilog;

namespace MMORPGServer.Networking.Packets.Core
{
    /// <summary>
    /// Enhanced PacketHandler with middleware pipeline running in registration order
    /// </summary>
    public sealed class PacketHandler : IDisposable
    {
        private readonly List<IPacketMiddleware> _middlewares = new();

        // Core middleware instances
        private readonly RateLimitingMiddleware _rateLimitingMiddleware;
        private readonly AuthenticationMiddleware _authMiddleware;
        private readonly SlowPacketMiddleware _slowPacketMiddleware;
        private readonly MetricsMiddleware _metricsMiddleware;
        private readonly LoggingMiddleware _loggingMiddleware;

        public PacketHandler(bool enableDebugLogging = false, bool enableSlowPacketDetection = false)
        {
            // Initialize middleware instances
            _rateLimitingMiddleware = new RateLimitingMiddleware();
            _authMiddleware = new AuthenticationMiddleware();
            _slowPacketMiddleware = new SlowPacketMiddleware();
            _metricsMiddleware = new MetricsMiddleware();
            _loggingMiddleware = new LoggingMiddleware(
                logAllPackets: enableDebugLogging,
                logPacketContent: enableDebugLogging,
                logClientInfo: true
            );

            // Register middleware in execution order
            RegisterMiddleware(_rateLimitingMiddleware);
            RegisterMiddleware(_authMiddleware);
            if (enableDebugLogging)
                RegisterMiddleware(_loggingMiddleware);
            if (enableSlowPacketDetection)
                RegisterMiddleware(_slowPacketMiddleware);

            RegisterMiddleware(_metricsMiddleware);

            Log.Information("PacketHandler initialized with {MiddlewareCount} middleware components (Debug: {Debug}, SlowPacketDetection: {SlowDetection})",
                _middlewares.Count, enableDebugLogging, enableSlowPacketDetection);
        }

        /// <summary>
        /// Register custom middleware (executes in registration order)
        /// </summary>
        public void RegisterMiddleware(IPacketMiddleware middleware)
        {
            _middlewares.Add(middleware);
            Log.Debug("Registered middleware {MiddlewareType}", middleware.GetType().Name);
        }

        /// <summary>
        /// Main packet handling entry point
        /// </summary>
        public async ValueTask HandlePacketAsync(GameClient client, Packet packet)
        {
            // Check if handler is registered
            if (!PacketHandlerRegistry.IsHandlerRegistered(packet.Type))
            {
                Log.Warning("No handler registered for packet type {PacketType} from client {ClientId} (Player: {PlayerName})",
                    packet.Type, client.ClientId, client.Player?.Name ?? "N/A");
                return;
            }

            try
            {
                // Execute through middleware pipeline
                await ExecuteWithMiddleware(client, packet);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error handling packet {PacketType} from client {ClientId} (Player: {PlayerName})",
                    packet.Type, client.ClientId, client.Player?.Name ?? "N/A");

                // Consider disconnecting client on critical errors
                await client.DisconnectAsync($"Packet handling error: {ex.GetType().Name}");
            }
        }

        /// <summary>
        /// Execute packet through middleware pipeline in registration order
        /// </summary>
        private async ValueTask ExecuteWithMiddleware(GameClient client, Packet packet)
        {
            if (_middlewares.Count == 0)
            {
                // No middleware registered, execute handler directly
                await PacketHandlerRegistry.HandlePacketAsync(client, packet);
                return;
            }

            // Execute middleware in registration order (not reversed)
            var currentIndex = 0;

            async ValueTask ProcessNext()
            {
                if (currentIndex >= _middlewares.Count)
                {
                    // All middleware processed, execute the actual handler
                    await PacketHandlerRegistry.HandlePacketAsync(client, packet);
                    return;
                }

                var middleware = _middlewares[currentIndex];
                currentIndex++;

                var shouldContinue = await middleware.InvokeAsync(client, packet, ProcessNext);
                if (!shouldContinue)
                {
                    Log.Debug("Middleware {MiddlewareType} blocked packet {PacketType} from client {ClientId}",
                        middleware.GetType().Name, packet.Type, client.ClientId);
                }
            }

            await ProcessNext();
        }

        /// <summary>
        /// Get statistics about registered packet handlers
        /// </summary>
        public IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
        {
            return PacketHandlerRegistry.GetRegisteredHandlers();
        }

        /// <summary>
        /// Check if a handler is registered for a specific packet type
        /// </summary>
        public bool IsHandlerRegistered(GamePackets packetType) =>
            PacketHandlerRegistry.IsHandlerRegistered(packetType);

        /// <summary>
        /// Enable debug logging for specific packet types at runtime
        /// </summary>
        public void EnablePacketLogging(GamePackets packetType)
        {
            _loggingMiddleware?.EnableLoggingForPacket(packetType);
        }

        /// <summary>
        /// Disable debug logging for specific packet types at runtime
        /// </summary>
        public void DisablePacketLogging(GamePackets packetType)
        {
            _loggingMiddleware?.DisableLoggingForPacket(packetType);
        }

        /// <summary>
        /// Get current packet statistics from logging middleware
        /// </summary>
        public IReadOnlyDictionary<GamePackets, long>? GetPacketStatistics()
        {
            return _loggingMiddleware?.GetPacketCounts();
        }

        /// <summary>
        /// Reset packet statistics
        /// </summary>
        public void ResetStatistics()
        {
            _loggingMiddleware?.ResetPacketCounts();
            Log.Information("Packet statistics reset");
        }

        /// <summary>
        /// Log current middleware pipeline status
        /// </summary>
        public void LogMiddlewareStatus()
        {
            Log.Information("=== PacketHandler Middleware Pipeline Status ===");
            Log.Information("Total middleware components: {Count}", _middlewares.Count);

            for (int i = 0; i < _middlewares.Count; i++)
            {
                var middleware = _middlewares[i];
                Log.Information("  {Index}. {MiddlewareType}", i + 1, middleware.GetType().Name);
            }

            _loggingMiddleware?.LogPacketStatistics();
        }

        /// <summary>
        /// Get middleware pipeline information
        /// </summary>
        public IReadOnlyList<string> GetMiddlewarePipeline()
        {
            return _middlewares.Select(m => m.GetType().Name).ToList();
        }

        /// <summary>
        /// Check if the packet handler is healthy (all middleware operational)
        /// </summary>
        public bool IsHealthy()
        {
            try
            {
                // Basic health check - ensure all middleware instances are not null
                return _rateLimitingMiddleware != null &&
                       _authMiddleware != null &&
                       _slowPacketMiddleware != null &&
                       _metricsMiddleware != null &&
                       _loggingMiddleware != null &&
                       _middlewares.Count > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Health check failed for PacketHandler");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                Log.Information("Disposing PacketHandler and middleware components...");

                // Dispose middleware that implement IDisposable
                _rateLimitingMiddleware?.Dispose();
                _slowPacketMiddleware?.Dispose();
                _metricsMiddleware?.Dispose();
                Log.Information("PacketHandler disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing PacketHandler");
            }
        }
    }

    /// <summary>
    /// Factory for creating PacketHandler with different configurations
    /// </summary>
    public static class PacketHandlerFactory
    {
        /// <summary>
        /// Create a production PacketHandler with standard middleware
        /// </summary>
        public static PacketHandler CreateProduction()
        {
            return new PacketHandler(enableDebugLogging: false, enableSlowPacketDetection: true);
        }

        /// <summary>
        /// Create a development PacketHandler with debug logging enabled
        /// </summary>
        public static PacketHandler CreateDevelopment()
        {
            return new PacketHandler(enableDebugLogging: true, enableSlowPacketDetection: true);
        }

        /// <summary>
        /// Create a lightweight PacketHandler for testing
        /// </summary>
        public static PacketHandler CreateTesting()
        {
            var handler = new PacketHandler(enableDebugLogging: false, enableSlowPacketDetection: false);
            // Could add test-specific middleware here
            return handler;
        }

        /// <summary>
        /// Create a high-performance PacketHandler with minimal middleware
        /// </summary>
        public static PacketHandler CreateHighPerformance()
        {
            // Create with minimal middleware for maximum performance
            var handler = new PacketHandler(enableDebugLogging: false, enableSlowPacketDetection: false);

            Log.Information("Created high-performance PacketHandler with minimal middleware");
            return handler;
        }
    }
}
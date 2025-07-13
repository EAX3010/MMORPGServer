using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Middleware;
using Serilog;

namespace MMORPGServer.Networking.Packets
{
    /// <summary>
    /// Enhanced PacketHandler with comprehensive middleware pipeline
    /// </summary>
    public sealed class PacketHandler : IDisposable
    {
        private readonly List<IPacketMiddleware> _middlewares = new();

        // Core middleware instances
        private readonly RateLimitingMiddleware _rateLimitingMiddleware;
        private readonly AuthenticationMiddleware _authMiddleware;
        private readonly MetricsMiddleware _metricsMiddleware;
        private readonly LoggingMiddleware _loggingMiddleware;

        public PacketHandler(bool enableDebugLogging = false, bool enableSlowPacketDetection = true)
        {
            // Initialize middleware instances
            _rateLimitingMiddleware = new RateLimitingMiddleware();
            _authMiddleware = new AuthenticationMiddleware();
            _metricsMiddleware = new MetricsMiddleware();
            _loggingMiddleware = new LoggingMiddleware(
                logAllPackets: enableDebugLogging,
                logPacketContent: enableDebugLogging,
                logClientInfo: true
            );



            // Register middleware in execution order
            RegisterMiddleware(_rateLimitingMiddleware);  // 1. Rate limiting
            RegisterMiddleware(_authMiddleware);          // 2. Authentication  

            if (enableDebugLogging)
                RegisterMiddleware(_loggingMiddleware);   // 4. Logging (debug)


            RegisterMiddleware(_metricsMiddleware);       // 6. Metrics (wraps handler)

            Log.Information("PacketHandler initialized with {MiddlewareCount} middleware components (Debug: {Debug}, SlowDetection: {SlowDetection})",
                _middlewares.Count, enableDebugLogging, enableSlowPacketDetection);
        }

        /// <summary>
        /// Register custom middleware (executes in registration order)
        /// </summary>
        public void RegisterMiddleware(IPacketMiddleware middleware)
        {
            _middlewares.Add(middleware);
            Log.Information("Registered middleware {MiddlewareType}", middleware.GetType().Name);
        }

        /// <summary>
        /// Main packet handling entry point
        /// </summary>
        public async ValueTask HandlePacketAsync(GameClient client, Packet packet)
        {
            var handler = PacketHandlerRegistry.GetHandler(packet.Type);
            if (handler == null)
            {
                Log.Warning("No handler registered for packet type {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
                return;
            }

            try
            {
                await ExecuteWithMiddleware(client, packet, handler);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error handling packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);

                // Consider disconnecting client on critical errors
                await client.DisconnectAsync($"Packet handling error: {ex.GetType().Name}");
            }
        }

        /// <summary>
        /// Execute packet through middleware pipeline
        /// </summary>
        private async ValueTask ExecuteWithMiddleware(GameClient client, Packet packet,
            Func<GameClient, Packet, ValueTask> handler)
        {
            if (_middlewares.Count == 0)
            {
                // No middleware registered, execute handler directly
                await handler(client, packet);
                return;
            }

            // Build middleware pipeline from end to start
            Func<ValueTask> pipeline = () => handler(client, packet);

            // Build pipeline in reverse order (last middleware wraps first)
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = _middlewares[i];
                var next = pipeline;

                pipeline = async () =>
                {
                    var shouldContinue = await middleware.InvokeAsync(client, packet, next);
                    if (!shouldContinue)
                    {
                        Log.Debug("Middleware {MiddlewareType} blocked packet {PacketType} from client {ClientId}",
                            middleware.GetType().Name, packet.Type, client.ClientId);
                    }
                };
            }

            await pipeline();
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
                _rateLimitingMiddleware.Dispose();
                _metricsMiddleware.Dispose();
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
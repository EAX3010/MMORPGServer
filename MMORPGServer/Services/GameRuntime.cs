using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Security;
using MMORPGServer.Networking.Server;
using Serilog;

namespace MMORPGServer.Services
{
    /// <summary>
    /// Manages the lifecycle and provides access to core game systems and services.
    /// This static class ensures a single point of control for initialization and disposal.
    /// </summary>
    public static class GameRuntime
    {
        private static bool _isInitialized = false;

        // Publicly accessible core services. Set once during initialization.
        public static TransferCipher TransferCipher { get; private set; } = default!;
        public static NetworkManager NetworkManager { get; private set; } = default!;
        public static GameServer GameServer { get; private set; } = default!;

        /// <summary>
        /// Defines the mode for initializing the PacketHandler.
        /// </summary>
        public enum PacketHandlerMode
        {
            Development,
            Production,
            HighPerformance,
            Testing
        }

        /// <summary>
        /// Initializes all core game systems and services.
        /// This method should be called once at application startup.
        /// </summary>
        /// <param name="handlerMode">Specifies the mode for the PacketHandler, enabling different logging/security levels.</param>
        public static async Task InitializeAsync(PacketHandlerMode handlerMode = PacketHandlerMode.Development)
        {
            if (_isInitialized)
            {
                Log.Warning("Game systems already initialized. Skipping initialization.");
                return;
            }

            Log.Information("Initializing game systems...");

            try
            {
                // Create core services in dependency order
                Log.Debug("Initializing TransferCipher...");
                GameRuntime.TransferCipher = new TransferCipher(GameServerConfig.Configuration);



                Log.Debug("Initializing GameWorld...");
                GameWorld.Instance = new GameWorld();

                Log.Debug("Initializing NetworkManager...");
                GameRuntime.NetworkManager = new NetworkManager();

                Log.Debug("Creating PacketHandler with mode: {HandlerMode}...", handlerMode);
                PacketHandler packetHandler;
                switch (handlerMode)
                {
                    case PacketHandlerMode.Development:
                        packetHandler = PacketHandlerFactory.CreateDevelopment();
                        break;
                    case PacketHandlerMode.Production:
                        packetHandler = PacketHandlerFactory.CreateProduction();
                        break;
                    case PacketHandlerMode.HighPerformance:
                        packetHandler = PacketHandlerFactory.CreateHighPerformance();
                        break;
                    case PacketHandlerMode.Testing:
                        packetHandler = PacketHandlerFactory.CreateTesting();
                        break;
                    default:
                        Log.Warning("Unknown PacketHandlerMode '{HandlerMode}'. Defaulting to Development.", handlerMode);
                        packetHandler = PacketHandlerFactory.CreateDevelopment();
                        break;
                }

                Log.Debug("Initializing GameServer...");
                GameRuntime.GameServer = new GameServer(GameRuntime.NetworkManager, packetHandler);

                _isInitialized = true;
                Log.Information("Game systems initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize game systems");
                // Attempt to clean up even on partial failure
                await DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// Disposes all initialized game systems and services, releasing resources.
        /// This method should be called once at application shutdown.
        /// </summary>
        public static async Task DisposeAsync()
        {
            if (!_isInitialized)
            {
                Log.Warning("Attempted to dispose game systems that were not initialized or already disposed.");
                return;
            }

            Log.Information("Disposing game systems...");

            try
            {
                // Stop server first
                if (GameServer != null)
                {
                    await GameServer.StopAsync(); // Ensure server is stopped gracefully
                    GameServer.Dispose(); // Dispose internal resources
                    GameServer = null!;
                    Log.Debug("GameServer disposed");
                }

                // Dispose networking components
                if (NetworkManager != null)
                {
                    NetworkManager.Dispose();
                    NetworkManager = null!;
                    Log.Debug("NetworkManager disposed");
                }


                // Clear TransferCipher if it holds significant resources
                if (TransferCipher != null)
                {
                    // TransferCipher might not need explicit Dispose depending on its internal resources
                    TransferCipher = null!;
                    Log.Debug("TransferCipher reference cleared");
                }

                _isInitialized = false; // Mark as disposed
                Log.Information("Game systems disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing game systems");
            }
        }

        /// <summary>
        /// Starts the game server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop the server.</param>
        /// <exception cref="InvalidOperationException">Thrown if game systems are not initialized.</exception>
        public static async Task StartServerAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("Game systems must be initialized before starting the server. Call InitializeAsync() first.");
            Log.Information("Starting game server...");
            await GameServer.StartAsync(cancellationToken);
            Log.Information("Game server started successfully on port {Port}", GameServerConfig.ServerPort);
        }

        /// <summary>
        /// Stops the game server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to observe while stopping.</param>
        /// <exception cref="InvalidOperationException">Thrown if game systems are not initialized.</exception>
        public static async Task StopServerAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("Game systems must be initialized before stopping the server. Call InitializeAsync() first.");
            Log.Information("Stopping game server...");
            await GameServer.StopAsync(cancellationToken);
            Log.Information("Game server stopped");
        }

        /// <summary>
        /// Logs the current status of various core game systems.
        /// </summary>
        public static void LogSystemStatus()
        {
            Log.Information("=== Game Systems Status ===");
            Log.Information("Systems Initialized: {IsInitialized}", _isInitialized);
            if (!_isInitialized)
            {
                Log.Information("Server Status: Not Running (Systems not initialized)");
                Log.Information("========================");
                return;
            }

            Log.Information("Server Running: {IsRunning}", GameServer.IsRunning);
            if (GameServer.IsRunning)
            {
                Log.Information("Listening on Port: {Port}", GameServerConfig.ServerPort);
            }
            Log.Information("Connected Players: {PlayerCount}/{MaxPlayers}", NetworkManager.ConnectionCount, GameServerConfig.MaxPlayers);
            Log.Information("Maps Loaded: {MapCount}", GameWorld.Instance.MapManager?.GetTotalMaps() ?? 0); // Use GameWorld directly

            var stats = NetworkManager.GetNetworkStatistics();
            Log.Information("Network Stats - Packets Sent: {Packets:N0}, Data Sent: {MB:F2} MB",
                stats.TotalPacketsSent, stats.TotalBytesSent / 1024.0 / 1024.0);

            Log.Information("Server Stats - Uptime: {Uptime}, Total Connections: {Connections:N0}, Messages Processed: {Messages:N0}",
                 GameServer.Uptime.ToString(@"dd\.hh\:mm\:ss"), GameServer.TotalConnectionsAccepted, GameServer.TotalMessagesProcessed);
            Log.Information("========================");
        }

        /// <summary>
        /// Gets a value indicating whether the game systems have been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets a value indicating whether the game server is currently running and listening for connections.
        /// </summary>
        public static bool IsServerRunning => _isInitialized && GameServer.IsRunning;
    }
}
using MMORPGServer.Networking.Packets;
using MMORPGServer.Networking.Security;
using MMORPGServer.Networking.Server;
using Serilog;

namespace MMORPGServer.Services
{
    public static class GameSystemsManager
    {

        public static GameWorld? GameWorld
        {
            get => _gameWorld ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set => _gameWorld = value;
        }
        private static GameWorld? _gameWorld;

        public static TransferCipher? TransferCipher
        {
            get => _transferCipher ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set => _transferCipher = value;
        }
        private static TransferCipher? _transferCipher;

        public static NetworkManager? NetworkManager
        {
            get => _networkManager ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set => _networkManager = value;
        }
        private static NetworkManager? _networkManager;
        public static GameServer? GameServer
        {
            get => _gameServer ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set => _gameServer = value;
        }
        private static GameServer? _gameServer;

        public static async Task InitializeAsync()
        {
            Log.Information("Initializing game systems...");

            try
            {
                // Create core services in dependency order
                Log.Debug("Initializing TransferCipher...");
                TransferCipher = new TransferCipher(GameServerConfig.Configuration);

                Log.Debug("Initializing GameWorld...");
                GameWorld = new GameWorld(new MapManager(), new PlayerManager());

                Log.Debug("Initializing NetworkManager...");
                NetworkManager = new NetworkManager();

                Log.Debug("Creating PacketHandler...");
                var packetHandler = PacketHandlerFactory.CreateDevelopment();

                Log.Debug("Initializing GameServer...");
                GameServer = new GameServer(NetworkManager, packetHandler);


                Log.Information("Game systems initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize game systems");
                await DisposeAsync(); // Cleanup on failure
                throw;
            }
        }

        public static async Task DisposeAsync()
        {
            Log.Information("Disposing game systems...");

            try
            {
                // Stop server first
                if (GameServer != null)
                {
                    await GameServer.StopAsync();
                    GameServer.Dispose();
                    GameServer = null;
                    Log.Debug("GameServer disposed");
                }

                // Dispose networking
                if (NetworkManager != null)
                {
                    NetworkManager.Dispose();
                    NetworkManager = null;
                    Log.Debug("NetworkManager disposed");
                }
                Log.Information("Game systems disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing game systems");
            }
        }

        // Utility methods for server control
        public static async Task StartServerAsync(CancellationToken cancellationToken = default)
        {
            Log.Information("Starting game server...");
            await GameServer.StartAsync(cancellationToken);
            Log.Information("Game server started successfully on port {Port}", GameServerConfig.ServerPort);
        }

        public static async Task StopServerAsync(CancellationToken cancellationToken = default)
        {
            Log.Information("Stopping game server...");
            await GameServer.StopAsync(cancellationToken);
            Log.Information("Game server stopped");
        }

        // Status and monitoring methods
        public static void LogSystemStatus()
        {
            Log.Information("=== Game Systems Status ===");
            Log.Information("Server Running: {IsRunning}", GameServer?.IsRunning ?? false);
            if (GameServer?.IsRunning == true)
            {
                Log.Information("Listening on Port: {Port}", GameServerConfig.ServerPort);
            }
            Log.Information("Connected Players: {PlayerCount}/{MaxPlayers}", NetworkManager?.ConnectionCount ?? 0, GameServerConfig.MaxPlayers);
            Log.Information("Maps Loaded: {MapCount}", GameSystemsManager.GameWorld.MapManager?.GetTotalMaps() ?? 0);

            if (NetworkManager != null)
            {
                var stats = NetworkManager.GetNetworkStatistics();
                Log.Information("Network Stats - Packets Sent: {Packets:N0}, Data Sent: {MB:F2} MB",
                    stats.TotalPacketsSent, stats.TotalBytesSent / 1024.0 / 1024.0);
            }

            if (GameServer != null)
            {
                Log.Information("Server Stats - Uptime: {Uptime}, Total Connections: {Connections:N0}, Messages Processed: {Messages:N0}",
                     GameServer.Uptime.ToString(@"dd\.hh\:mm\:ss"), GameServer.TotalConnectionsAccepted, GameServer.TotalMessagesProcessed);
            }
            Log.Information("========================");
        }

        public static bool IsServerRunning => GameServer?.IsRunning ?? false;
    }
}
using MMORPGServer.Entities;
using MMORPGServer.Networking.Packets;
using MMORPGServer.Networking.Security;
using MMORPGServer.Networking.Server;
using Serilog;

namespace MMORPGServer.Services
{
    public static class GameSystemsManager
    {
        public static Map TwinCity => MapManager?.GetMap(1002) ?? throw new InvalidOperationException("MapManager not initialized");
        public static PlayerManager? PlayerManager
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }
        public static GameWorld? GameWorld
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }
        public static TransferCipher? TransferCipher
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }
        public static NetworkManager? NetworkManager
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }
        public static GameServer? GameServer
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }
        public static MapManager? MapManager
        {
            get => field ?? throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");
            private set;
        }

        public static async Task InitializeAsync()
        {
            Log.Information("Initializing game systems...");

            try
            {
                // Create core services in dependency order
                TransferCipher = new TransferCipher(GameServerConfig.Configuration);
                PlayerManager = new PlayerManager();
                MapManager = new MapManager();
                GameWorld = new GameWorld();
                NetworkManager = new NetworkManager();
                var PacketHandler = PacketHandlerFactory.CreateDevelopment();
                GameServer = new GameServer(NetworkManager, PacketHandler);


                Log.Information("Game systems initialized successfully");
                Log.Information("  - Packet Handlers: {HandlerCount} registered", PacketHandlerRegistry.GetHandlerCount());
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
            Log.Information("Connected Players: {PlayerCount}", NetworkManager?.ConnectionCount ?? 0);
            Log.Information("DMaps Loaded: {DMapCount}", MapManager.GetTotalMaps());

            if (NetworkManager != null)
            {
                var stats = NetworkManager.GetNetworkStatistics();
                Log.Information("Network Stats - Packets: {Packets:N0}, Data: {MB:F1} MB, Uptime: {Uptime}",
                    stats.TotalPacketsSent, stats.TotalBytesSent / 1024.0 / 1024.0, stats.Uptime);
            }

            if (GameServer != null)
            {
                Log.Information("Server Stats - Connections: {Connections:N0}, Messages: {Messages:N0}, Uptime: {Uptime}",
                    GameServer.TotalConnectionsAccepted, GameServer.TotalMessagesProcessed, GameServer.Uptime);
            }
            Log.Information("========================");
        }

        public static bool IsServerRunning => GameServer?.IsRunning ?? false;
    }
}

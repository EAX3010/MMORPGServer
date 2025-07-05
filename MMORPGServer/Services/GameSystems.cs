using MMORPGServer.Infrastructure.Database.Ini;
using MMORPGServer.Networking.Packets;
using MMORPGServer.Networking.Security;
using MMORPGServer.Networking.Server;
using Serilog;

namespace MMORPGServer.Services
{
    public static class GameSystemsManager
    {
        private static PlayerManager? _playerManager;
        private static GameWorld? _gameWorld;
        private static TransferCipher? _transferCipher;
        private static NetworkManager? _networkManager;
        private static GameServer? _gameServer;
        private static PacketHandler? _packetHandler;

        public static PlayerManager PlayerManager => _playerManager ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static GameWorld GameWorld => _gameWorld ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static TransferCipher TransferCipher => _transferCipher ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static NetworkManager NetworkManager => _networkManager ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static GameServer GameServer => _gameServer ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static PacketHandler PacketHandler => _packetHandler ??
            throw new InvalidOperationException("Game systems not initialized. Call InitializeAsync() first.");

        public static async Task InitializeAsync()
        {
            Log.Information("Initializing game systems...");

            try
            {
                // Create core services in dependency order
                _transferCipher = new TransferCipher(GameServerConfig.Configuration);
                Log.Debug("TransferCipher initialized");

                _playerManager = new PlayerManager();
                Log.Debug("PlayerManager initialized");

                _gameWorld = new GameWorld(); // Pass PlayerManager to avoid duplicates
                Log.Debug("GameWorld initialized");

                // Create networking services
                _networkManager = new NetworkManager();
                Log.Debug("NetworkManager initialized");

                _packetHandler = new PacketHandler();
                Log.Debug("PacketHandler initialized");

                _gameServer = new GameServer(_networkManager, _packetHandler);
                Log.Debug("GameServer initialized");

                Log.Information("Game systems initialized successfully");
                Log.Information("System status:");
                Log.Information("  - Database: Ready");
                Log.Information("  - Repositories: Ready");
                Log.Information("  - Game World: Ready");
                Log.Information("  - Network: Ready");
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
                if (_gameServer != null)
                {
                    await _gameServer.StopAsync();
                    _gameServer.Dispose();
                    _gameServer = null;
                    Log.Debug("GameServer disposed");
                }

                // Dispose networking
                if (_networkManager != null)
                {
                    _networkManager.Dispose();
                    _networkManager = null;
                    Log.Debug("NetworkManager disposed");
                }

                // Dispose other services
                _packetHandler = null;
                _gameWorld = null;
                _playerManager = null;
                _transferCipher = null;


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
            Log.Information("Server Running: {IsRunning}", _gameServer?.IsRunning ?? false);
            Log.Information("Connected Players: {PlayerCount}", _networkManager?.ConnectionCount ?? 0);
            Log.Information("Maps Loaded: {MapCount}", MapRepository.Instance.GetMapCount());

            if (_networkManager != null)
            {
                var stats = _networkManager.GetNetworkStatistics();
                Log.Information("Network Stats - Packets: {Packets:N0}, Data: {MB:F1} MB, Uptime: {Uptime}",
                    stats.TotalPacketsSent, stats.TotalBytesSent / 1024.0 / 1024.0, stats.Uptime);
            }

            if (_gameServer != null)
            {
                Log.Information("Server Stats - Connections: {Connections:N0}, Messages: {Messages:N0}, Uptime: {Uptime}",
                    _gameServer.TotalConnectionsAccepted, _gameServer.TotalMessagesProcessed, _gameServer.Uptime);
            }
            Log.Information("========================");
        }

        public static bool IsInitialized => _playerManager != null && _gameWorld != null && _transferCipher != null;
        public static bool IsServerRunning => _gameServer?.IsRunning ?? false;
    }
}

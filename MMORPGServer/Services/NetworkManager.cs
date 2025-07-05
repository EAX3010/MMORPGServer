using MMORPGServer.Networking.Clients;
using Serilog;
using System.Collections.Concurrent;
using System.Globalization;

namespace MMORPGServer.Services
{
    public sealed class NetworkManager
    {
        private readonly ConcurrentDictionary<int, GameClient> _clients = new();
        private long _totalPacketsSent = 0;
        private long _totalBytesSent = 0;
        private readonly DateTime _startTime;

        public NetworkManager()
        {
            _startTime = DateTime.UtcNow;
            Log.Debug("NetworkManager initialized");
        }

        public IReadOnlyDictionary<int, GameClient> ConnectedClients => _clients;
        public int ConnectionCount => _clients.Count;

        public void AddClient(GameClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            if (_clients.TryAdd(client.ClientId, client))
            {
                Log.Debug("Client {ClientId} added to network manager from {IPAddress}",
                    client.ClientId, client.IPAddress ?? "Unknown");

                // Log connection milestones
                int currentCount = _clients.Count;
                if (currentCount % 10 == 0 && currentCount > 0)
                {
                    Log.Information("Network milestone: {ClientCount} concurrent connections", currentCount);
                }

                // Log major milestones
                if (currentCount == 50 || currentCount == 100 || currentCount % 250 == 0)
                {
                    var stats = GetNetworkStatistics();
                    Log.Information("Network Status - Clients: {ClientCount}, Packets Sent: {PacketsSent}, Data: {DataMB:F1} MB",
                        currentCount, stats.TotalPacketsSent, stats.TotalBytesSent / 1024.0 / 1024.0);
                }
            }
            else
            {
                Log.Warning("Failed to add client {ClientId} - ID already exists", client.ClientId);
            }
        }

        public void RemoveClient(int clientId)
        {
            if (_clients.TryRemove(clientId, out GameClient? client))
            {
                TimeSpan connectionDuration = DateTime.UtcNow - client.ConnectedAt;

                Log.Information("Client {ClientId} removed from network manager (Duration: {Duration}, Remaining: {Count})",
                    clientId, connectionDuration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), _clients.Count);

                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing client {ClientId}", clientId);
                }
            }
            else
            {
                Log.Debug("Attempted to remove non-existent client {ClientId}", clientId);
            }
        }

        public GameClient? GetClient(int clientId)
        {
            _clients.TryGetValue(clientId, out GameClient? client);
            return client;
        }

        public async ValueTask BroadcastAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0)
        {
            if (packetData.IsEmpty)
            {
                Log.Warning("Attempted to broadcast empty packet data");
                return;
            }

            var tasks = new List<ValueTask>();
            int clientCount = 0;
            int failedClients = 0;

            // Collect all send tasks
            foreach (GameClient client in _clients.Values)
            {
                if (client.ClientId != excludeClientId && client.IsConnected)
                {
                    try
                    {
                        tasks.Add(client.SendPacketAsync(packetData));
                        clientCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to queue packet for client {ClientId}", client.ClientId);
                        failedClients++;
                    }
                }
            }

            if (tasks.Count > 0)
            {
                Log.Debug("Broadcasting packet to {ClientCount} clients (Size: {PacketSize} bytes, Failed: {FailedCount})",
                    clientCount, packetData.Length, failedClients);

                // Execute all send tasks
                int successfulSends = 0;
                foreach (ValueTask task in tasks)
                {
                    try
                    {
                        await task;
                        successfulSends++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send broadcast packet to a client");
                    }
                }

                // Update statistics based on successful sends
                if (successfulSends > 0)
                {
                    Interlocked.Add(ref _totalPacketsSent, successfulSends);
                    Interlocked.Add(ref _totalBytesSent, (long)packetData.Length * successfulSends);

                    // Log broadcast milestones
                    if (_totalPacketsSent % 1000 == 0)
                    {
                        Log.Debug("Network statistics: {TotalPackets} packets sent, {TotalMB:F1} MB total",
                            _totalPacketsSent, _totalBytesSent / 1024.0 / 1024.0);
                    }
                }

                if (successfulSends != clientCount)
                {
                    Log.Warning("Broadcast partially failed: {Successful}/{Total} clients reached",
                        successfulSends, clientCount);
                }
            }
            else
            {
                Log.Debug("No clients available for broadcast (Total clients: {TotalClients}, Excluded: {ExcludedId})",
                    _clients.Count, excludeClientId);
            }
        }

        public async ValueTask SendToClientAsync(int clientId, ReadOnlyMemory<byte> packetData)
        {
            if (packetData.IsEmpty)
            {
                Log.Warning("Attempted to send empty packet data to client {ClientId}", clientId);
                return;
            }

            var client = GetClient(clientId);
            if (client == null)
            {
                Log.Warning("Cannot send packet to non-existent client {ClientId}", clientId);
                return;
            }

            if (!client.IsConnected)
            {
                Log.Warning("Cannot send packet to disconnected client {ClientId}", clientId);
                return;
            }

            try
            {
                await client.SendPacketAsync(packetData);

                Interlocked.Increment(ref _totalPacketsSent);
                Interlocked.Add(ref _totalBytesSent, packetData.Length);

                Log.Debug("Sent packet to client {ClientId} (Size: {PacketSize} bytes)",
                    clientId, packetData.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send packet to client {ClientId}", clientId);
            }
        }

        public async ValueTask DisconnectAllAsync()
        {
            Log.Information("Disconnecting all clients ({ClientCount} total)...", _clients.Count);

            var disconnectTasks = new List<Task>();

            foreach (var client in _clients.Values.ToArray()) // ToArray to avoid modification during iteration
            {
                disconnectTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await client.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error disconnecting client {ClientId}", client.ClientId);
                    }
                }));
            }

            if (disconnectTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(disconnectTasks).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    Log.Warning("Some client disconnections did not complete within timeout");
                }
            }

            // Clear the client dictionary
            _clients.Clear();
            Log.Information("All clients disconnected");
        }

        public IEnumerable<GameClient> GetConnectedClients()
        {
            return _clients.Values.Where(c => c.IsConnected);
        }

        public IEnumerable<GameClient> GetClientsInMap(int mapId)
        {
            return _clients.Values.Where(c => c.IsConnected && c.Player.MapId == mapId);
        }

        public async ValueTask BroadcastToMapAsync(ReadOnlyMemory<byte> packetData, int mapId, int excludeClientId = 0)
        {
            if (packetData.IsEmpty)
            {
                Log.Warning("Attempted to broadcast empty packet data to map {MapId}", mapId);
                return;
            }

            var tasks = new List<ValueTask>();
            int clientCount = 0;

            foreach (GameClient client in _clients.Values)
            {
                if (client.ClientId != excludeClientId &&
                    client.IsConnected &&
                    client.Player.MapId == mapId)
                {
                    tasks.Add(client.SendPacketAsync(packetData));
                    clientCount++;
                }
            }

            if (tasks.Count > 0)
            {
                Log.Debug("Broadcasting packet to {ClientCount} clients in map {MapId} (Size: {PacketSize} bytes)",
                    clientCount, mapId, packetData.Length);

                int successfulSends = 0;
                foreach (ValueTask task in tasks)
                {
                    try
                    {
                        await task;
                        successfulSends++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to send map broadcast packet to a client");
                    }
                }

                if (successfulSends > 0)
                {
                    Interlocked.Add(ref _totalPacketsSent, successfulSends);
                    Interlocked.Add(ref _totalBytesSent, (long)packetData.Length * successfulSends);
                }
            }
            else
            {
                Log.Debug("No clients available for map broadcast in map {MapId}", mapId);
            }
        }

        // Method to get network statistics for monitoring
        public (long TotalPacketsSent, long TotalBytesSent, int ActiveConnections, TimeSpan Uptime) GetNetworkStatistics()
        {
            return (_totalPacketsSent, _totalBytesSent, _clients.Count, DateTime.UtcNow - _startTime);
        }

        // Method to get detailed client information
        public IEnumerable<(int ClientId, string IPAddress, TimeSpan ConnectionDuration, bool IsConnected)> GetClientDetails()
        {
            var now = DateTime.UtcNow;
            return _clients.Values.Select(c => (
                c.ClientId,
                c.IPAddress ?? "Unknown",
                now - c.ConnectedAt,
                c.IsConnected
            ));
        }

        // Performance monitoring
        public void LogPerformanceStatistics()
        {
            var stats = GetNetworkStatistics();
            var avgPacketsPerSecond = stats.Uptime.TotalSeconds > 0
                ? stats.TotalPacketsSent / stats.Uptime.TotalSeconds
                : 0;

            Log.Information("Network Performance Statistics:");
            Log.Information("  Active Connections: {ActiveConnections}", stats.ActiveConnections);
            Log.Information("  Total Packets Sent: {TotalPackets:N0}", stats.TotalPacketsSent);
            Log.Information("  Total Data Sent: {TotalMB:F2} MB", stats.TotalBytesSent / 1024.0 / 1024.0);
            Log.Information("  Average Packets/sec: {AvgPacketsPerSec:F1}", avgPacketsPerSecond);
            Log.Information("  Uptime: {Uptime}", stats.Uptime.ToString(@"dd\.hh\:mm\:ss"));
        }

        public void Dispose()
        {
            Log.Information("Disposing NetworkManager...");

            try
            {
                // Disconnect all clients synchronously for disposal
                var disconnectTasks = _clients.Values.Select(client =>
                    Task.Run(() => client.Dispose())).ToArray();

                if (disconnectTasks.Length > 0)
                {
                    Task.WaitAll(disconnectTasks, TimeSpan.FromSeconds(5));
                }

                _clients.Clear();
                Log.Information("NetworkManager disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during NetworkManager disposal");
            }
        }
    }
}
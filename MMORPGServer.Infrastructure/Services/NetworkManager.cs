using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Common.Interfaces;
using System.Collections.Concurrent;
using System.Globalization;

namespace MMORPGServer.Infrastructure.Services
{
    public sealed class NetworkManager : INetworkManager
    {
        private readonly ILogger<NetworkManager> _logger;
        private readonly ConcurrentDictionary<int, IGameClient> _clients = new();
        private long _totalPacketsSent = 0;
        private long _totalBytesSent = 0;

        public NetworkManager(ILogger<NetworkManager> logger)
        {
            _logger = logger;
        }

        public IReadOnlyDictionary<int, IGameClient> ConnectedClients => _clients;
        public int ConnectionCount => _clients.Count;

        public void AddClient(IGameClient client)
        {
            if (_clients.TryAdd(client.ClientId, client))
            {
                _logger.LogDebug("Client {ClientId} added to network manager from {IPAddress}",
                    client.ClientId, client.IPAddress ?? "Unknown");

                // Log connection milestones
                int currentCount = _clients.Count;
                if (currentCount % 10 == 0 && currentCount > 0)
                {
                    _logger.LogInformation("Network milestone: {ClientCount} concurrent connections", currentCount);
                }
            }
            else
            {
                _logger.LogWarning("Failed to add client {ClientId} - ID already exists", client.ClientId);
            }
        }

        public void RemoveClient(int clientId)
        {
            if (_clients.TryRemove(clientId, out IGameClient client))
            {
                TimeSpan connectionDuration = DateTime.UtcNow - client.ConnectedAt;

                _logger.LogInformation("Client {ClientId} removed from network manager (Duration: {Duration}, Remaining: {Count})",
                    clientId, connectionDuration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), _clients.Count);

                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing client {ClientId}", clientId);
                }
            }
            else
            {
                _logger.LogDebug("Attempted to remove non-existent client {ClientId}", clientId);
            }
        }

        public IGameClient GetClient(int clientId)
        {
            _clients.TryGetValue(clientId, out IGameClient client);
            return client;
        }

        public async ValueTask BroadcastAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0)
        {
            List<ValueTask> tasks = new List<ValueTask>();
            int clientCount = 0;

            foreach (IGameClient client in _clients.Values)
            {
                if (client.ClientId != excludeClientId && client.IsConnected)
                {
                    tasks.AddRange(client.SendPacketAsync(packetData));
                    clientCount++;
                }
            }

            if (tasks.Count > 0)
            {
                _logger.LogDebug("Broadcasting packet to {ClientCount} clients (Size: {PacketSize} bytes)",
                    clientCount, packetData.Length);

                foreach (ValueTask task in tasks)
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send broadcast packet to a client");
                    }
                }

                // Update statistics
                Interlocked.Add(ref _totalPacketsSent, clientCount);
                Interlocked.Add(ref _totalBytesSent, packetData.Length * clientCount);

                // Log broadcast milestones
                if (_totalPacketsSent % 1000 == 0)
                {
                    _logger.LogDebug("Network statistics: {TotalPackets} packets sent, {TotalMB:F1} MB total",
                        _totalPacketsSent, _totalBytesSent / 1024.0 / 1024.0);
                }
            }
            else
            {
                _logger.LogDebug("No clients available for broadcast");
            }
        }


        // Method to get network statistics for monitoring
        public (long TotalPacketsSent, long TotalBytesSent, int ActiveConnections) GetNetworkStatistics()
        {
            return (_totalPacketsSent, _totalBytesSent, _clients.Count);
        }
    }
}
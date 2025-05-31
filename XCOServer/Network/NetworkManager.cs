namespace MMORPGServer.Network
{
    public sealed class NetworkManager : INetworkManager
    {
        private readonly ILogger<NetworkManager> _logger;
        private readonly ConcurrentDictionary<uint, IGameClient> _clients = new();

        public NetworkManager(ILogger<NetworkManager> logger)
        {
            _logger = logger;
        }

        public IReadOnlyDictionary<uint, IGameClient> ConnectedClients => _clients;
        public int ConnectionCount => _clients.Count;

        public void AddClient(IGameClient client)
        {
            if (_clients.TryAdd(client.ClientId, client))
            {
                _logger.LogDebug("Added client {ClientId} to network manager", client.ClientId);
            }
        }

        public void RemoveClient(uint clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                _logger.LogInformation("Client {ClientId} removed from network (Total: {Count})",
                    clientId, _clients.Count);
                client.Dispose();
            }
        }

        public IGameClient? GetClient(uint clientId)
        {
            _clients.TryGetValue(clientId, out var client);
            return client;
        }

        public async ValueTask BroadcastAsync(ReadOnlyMemory<byte> packetData, uint excludeClientId = 0)
        {
            var tasks = new List<ValueTask>();

            foreach (var client in _clients.Values)
            {
                if (client.ClientId != excludeClientId && client.IsConnected)
                {
                    tasks.Add(client.SendPacketAsync(packetData));
                }
            }

            foreach (var task in tasks)
            {
                await task;
            }
        }

        public async ValueTask BroadcastToMapAsync(uint mapId, ReadOnlyMemory<byte> packetData, uint excludeClientId = 0)
        {
            var tasks = new List<ValueTask>();

            foreach (var client in _clients.Values)
            {
                if (client.ClientId != excludeClientId &&
                    client.IsConnected &&
                    client.Player?.MapId == mapId)
                {
                    tasks.Add(client.SendPacketAsync(packetData));
                }
            }

            foreach (var task in tasks)
            {
                await task;
            }
        }
    }
}
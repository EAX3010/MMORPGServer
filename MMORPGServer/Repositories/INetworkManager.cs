using MMORPGServer.Repositories;

public interface INetworkManager
{
    IReadOnlyDictionary<uint, IGameClient> ConnectedClients { get; }
    int ConnectionCount { get; }

    void AddClient(IGameClient client);
    void RemoveClient(uint clientId);
    IGameClient? GetClient(uint clientId);
    ValueTask BroadcastAsync(ReadOnlyMemory<byte> packetData, uint excludeClientId = 0);
}

using MMORPGServer.Domain.Interfaces;

public interface INetworkManager
{
    IReadOnlyDictionary<int, IGameClient> ConnectedClients { get; }
    int ConnectionCount { get; }

    void AddClient(IGameClient client);
    void RemoveClient(int clientId);
    IGameClient? GetClient(int clientId);
    ValueTask BroadcastAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0);
}

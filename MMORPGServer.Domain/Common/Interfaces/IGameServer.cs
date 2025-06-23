namespace MMORPGServer.Domain.Common.Interfaces
{
    public interface IGameServer
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        void RemoveClient(int clientId);
        ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0);
    }
}

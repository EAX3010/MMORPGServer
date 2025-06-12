namespace MMORPGServer.Repositories
{
    public interface IGameServer
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        void RemoveClient(uint clientId);
        ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, uint excludeClientId = 0);
    }
}

namespace MMORPGServer.Domain.Interfaces
{
    public interface IGameClient : IDisposable
    {
        uint ClientId { get; }
        bool IsConnected { get; }
        string IPAddress { get; }
        DateTime ConnectedAt { get; }
        Task StartAsync(CancellationToken cancellationToken);
        ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData);
        ValueTask DisconnectAsync(string reason = "");
    }
}
namespace MMORPGServer.Interfaces
{
    public interface IGameClient : IDisposable
    {
        uint ClientId { get; }
        Player? Player { get; set; }
        bool IsConnected { get; }
        string? IPAddress { get; }
        DateTime ConnectedAt { get; }

        Task StartAsync(CancellationToken cancellationToken);
        ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData);
        ValueTask DisconnectAsync(string reason = "");
    }
}
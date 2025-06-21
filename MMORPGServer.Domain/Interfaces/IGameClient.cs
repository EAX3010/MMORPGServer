using MMORPGServer.Domain.Entities;

namespace MMORPGServer.Domain.Interfaces
{
    public interface IGameClient : IDisposable
    {
        int ClientId { get; }
        Player Player { get; set; }
        bool IsConnected { get; }
        string IPAddress { get; }
        DateTime ConnectedAt { get; }
        Task StartAsync(CancellationToken cancellationToken);
        ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData);
        ValueTask DisconnectAsync(string reason = "");
    }
}
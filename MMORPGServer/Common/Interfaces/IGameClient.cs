using MMORPGServer.Entities;

namespace MMORPGServer.Common.Interfaces
{
    public interface IGameClient : IDisposable
    {
        int ClientId { get; }
        Player? Player { get; set; }
        bool IsConnected { get; }
        string IPAddress { get; }
        DateTime ConnectedAt { get; }
        Task StartAsync(CancellationToken cancellationToken);
        ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData);
        ValueTask DisconnectAsync(string reason = "");
    }
}
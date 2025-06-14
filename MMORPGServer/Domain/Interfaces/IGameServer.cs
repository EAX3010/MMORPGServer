﻿namespace MMORPGServer.Domain.Interfaces
{
    public interface IGameServer
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        void RemoveClient(uint clientId);
        ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, uint excludeClientId = 0);
    }
}

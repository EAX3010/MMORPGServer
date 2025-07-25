﻿using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
namespace MMORPGServer.Networking.Packets.PacketsHandlers.Login
{
    [PacketHandler(GamePackets.CMsgLoginGame)]
    public sealed class LoginGame(Packet packet) : PacketBaseHandler(packet), IPacketHandler
    {
        public override async ValueTask ProcessAsync(GameClient client)
        {
            Log.Debug("Client {ClientId} is handling CMsgLoginGame", client.ClientId);
            await client.SendPacketAsync(Build());
            Log.Debug("CMsgLoginGame response sent to client {ClientId}", client.ClientId);
        }
        public ReadOnlyMemory<byte> Build()
        {
            return new Packet(GamePackets.LoginGamaEnglish)
           .WriteUInt32(10002)
           .WriteUInt32(0)
           .Build();
        }
    }
}

using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    [PacketHandler(GamePackets.CMsgItemPing)]
    public sealed class ItemPing(Packet packet) : AuthenticatedHandler(packet), IPacketHandler
    {
        public override async ValueTask ProcessAsync(GameClient client)
        {
            Log.Debug("Client {ClientId} is handling CMsgLoginGame", client.ClientId);
            var value = NewPing - client.Player!.Ping;
            if (value is 0)
            {
                value = 1;
            }
            uint Ping = NewPing - (value / 1000);
            await client.SendPacketAsync(this.Build(Ping));

            client.Player.Ping = NewPing;
            Log.Debug("CMsgItemPing response sent to client {ClientId}", Ping);
        }
        public uint NewPing => packet.ReadUInt32(8);
        public ReadOnlyMemory<byte> Build(uint Ping)
        {
            return new Packet(GamePackets.CMsgItemPing)
            .WriteUInt32(0)
            .WriteUInt32(Ping)
            .Build();
        }
    }
}

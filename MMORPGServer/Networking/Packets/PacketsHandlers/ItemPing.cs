using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    [PacketHandler(GamePackets.CMsgItemPing)]
    public sealed class ItemPing(Packet packet) : PacketBaseHandler(packet), IPacketHandler
    {
        public override async ValueTask ProcessAsync(GameClient client)
        {
            Log.Debug("Client {ClientId} is handling CMsgLoginGame", client.ClientId);
            packet.Seek(8);
            uint NewPing = packet.ReadUInt32();
            var value = NewPing - client.Player.Ping;
            if (value is 0)
            {
                value = 1;
            }
            uint Ping = (uint)(NewPing - (value / 1000));
            await client.SendPacketAsync(this.Build(Ping));

            client.Player.Ping = NewPing;
            Log.Information("CMsgItemPing response sent to client {ClientId}", NewPing);
        }
        public ReadOnlyMemory<byte> Build(uint Ping)
        {
            var p = new Packet(GamePackets.CMsgItemPing);
            p.Seek(8);
            p.WriteUInt32(Ping);
            p.FinalizePacket(GamePackets.CMsgItemPing);
            return p.Data.ToArray();

        }
    }
}

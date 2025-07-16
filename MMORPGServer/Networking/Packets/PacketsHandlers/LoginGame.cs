using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
namespace MMORPGServer.Networking.Packets.PacketsHandlers
{




    [PacketHandler(GamePackets.CMsgLoginGame)]
    public sealed class LoginGame : PacketBaseHandler, IPacketHandler
    {
        public LoginGame(Packet packet) : base(packet) { }

        public override async ValueTask ProcessAsync(GameClient client)
        {
            Log.Debug("Client {ClientId} is handling CMsgLoginGame", client.ClientId);
            await client.SendPacketAsync(PacketFactory.CreateLoginGamaEnglish());
            Log.Information("CMsgLoginGame response sent to client {ClientId}", client.ClientId);
        }
    }
}

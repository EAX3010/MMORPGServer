using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using Serilog;
namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    public sealed class LoginGameHandler
    {
        [PacketHandler(GamePackets.CMsgLoginGame)]
        public static async ValueTask HandleAsync(GameClient client, Packet packet)
        {
            try
            {
                Log.Debug("Client {ClientId} is handling CMsgLoginGame", client.ClientId);

                await client.SendPacketAsync(PacketFactory.CreateLoginGamaEnglish());

                Log.Information("CMsgLoginGame response sent to client {ClientId}", client.ClientId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CMsgLoginGame packet for client {ClientId}", client.ClientId);
            }
        }
    }
}

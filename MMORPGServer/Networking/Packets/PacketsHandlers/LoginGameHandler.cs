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
                await Handle(client);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CMsgLoginGame packet");
            }
        }
        private static async ValueTask Handle(GameClient client)
        {

            await client.SendPacketAsync(PacketFactory.CreateLoginGamaEnglish());

            Log.Information("CMsgLoginGame response sent to client.");
        }

    }
}

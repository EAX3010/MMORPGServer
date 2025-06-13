using Microsoft.Extensions.Logging;
using MMORPGServer.Attributes;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;

namespace MMORPGServer.Networking.Handlers
{
    public sealed class LoginGameHandler(
        ILogger<LoginGameHandler> logger) : IPacketProcessor
    {
        [PacketHandler(GamePackets.CMsgLoginGame)]
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);
            try
            {
                await Handle(client);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling CMsgLoginGame packet");
            }
        }
        private async ValueTask Handle(IGameClient client)
        {

            await client.SendPacketAsync(PacketFactory.CreateLoginGamaEnglish());

            logger.LogInformation("CMsgLoginGame response sent to client.");
        }

    }
}

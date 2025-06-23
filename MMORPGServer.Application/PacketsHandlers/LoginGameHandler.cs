using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Interfaces;
using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Common.Interfaces;

namespace MMORPGServer.Application.PacketsHandlers
{
    public sealed class LoginGameHandler(
        ILogger<LoginGameHandler> logger, IPacketFactory packetFactory) : IPacketProcessor<GamePackets>
    {
        public GamePackets PacketType => GamePackets.CMsgLoginGame;

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

            await client.SendPacketAsync(packetFactory.CreateLoginGamaEnglish());

            logger.LogInformation("CMsgLoginGame response sent to client.");
        }

    }
}

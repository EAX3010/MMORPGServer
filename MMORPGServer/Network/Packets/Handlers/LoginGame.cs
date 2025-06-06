using MMORPGServer.Attributes;
using MMORPGServer.Network.Fluent;

namespace MMORPGServer.Network.Packets.Handlers
{
    public sealed class LoginGame(
        ILogger<LoginGame> logger) : IPacketProcessor
    {
        [PacketHandler(GamePackets.CMsgLoginGame)]
        public async ValueTask HandleAsync(IGameClient client, Packet packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);
            try
            {
                await Handle(client, packet);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling CMsgLoginGame packet");
            }
        }

        /// <summary>
        /// Simple approach - exactly like your original but with fluent API
        /// </summary>
        private async ValueTask Handle(IGameClient client, Packet packet)
        {
            var response = PacketBuilder.Create(GamePackets.LoginGamaEnglish)
                .WriteUInt32(10002)
                .WriteUInt32(0)
                .Debug("CMsgLoginGame simple response")
                .BuildAndFinalize();
            if (response.IsEmpty)
            {
                logger.LogWarning("Finalized packet is empty.");
                return;
            }
            await client.SendPacketAsync(response);
            logger.LogInformation("CMsgLoginGame response sent to client.");
        }
    }
}

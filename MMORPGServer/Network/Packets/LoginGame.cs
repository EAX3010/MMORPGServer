namespace MMORPGServer.Network.Packets
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

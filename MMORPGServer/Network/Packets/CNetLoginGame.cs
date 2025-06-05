using MMORPGServer.Attributes;

namespace MMORPGServer.Network.Packets
{
    public partial class PlayerPackets(ILogger<PlayerPackets> logger)
    {
        [PacketHandler(GamePackets.CNetLoginGame)]
        public async ValueTask LoginGameHandler(IGameClient client, Packet packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            packet.Reset();
            packet.WriteUInt32(10002);
            packet.WriteUInt32(0);
            packet.FinalizePacket(GamePackets.LoginGamaEnglish);

            var finalized = packet.GetFinalizedMemory();
            if (finalized.IsEmpty)
            {
                logger.LogWarning("Finalized packet is empty.");
            }

            await client.SendPacketAsync(finalized);
            logger.LogInformation("LoginGame packet sent to client.");
        }
        [PacketHandler(GamePackets.LoginGamaEnglish)]
        public ValueTask LoginGamaEnglishHandler(IGameClient client, Packet packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            TransferCipher.Key = Encoding.ASCII.GetBytes("xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj");
            TransferCipher.Salt = Encoding.ASCII.GetBytes("z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX");

            var transferCipher = new TransferCipher("127.0.0.99");

            packet.Seek(4);
            uint[] decrypted = transferCipher.Decrypt(new uint[]
            {
              packet.ReadUInt32(),
              packet.ReadUInt32()
            });

            var uid = decrypted[0];
            var state = decrypted[1];

            logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", uid, state);
            return ValueTask.CompletedTask;
        }
    }
}

using MMORPGServer.Attributes;

namespace MMORPGServer.Network.Packets
{
    public partial class PlayerPackets(ILogger<PlayerPackets> logger) : IPacketProcessor
    {
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

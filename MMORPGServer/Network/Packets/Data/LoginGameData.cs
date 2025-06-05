namespace MMORPGServer.Network.Packets.Data
{
    public record LoginGameData : IPacketSerializable, IPacketDeserializable
    {
        public void Serialize(Packet packet)
        {
        }

        public void Deserialize(Packet packet)
        {
        }

        /// <summary>
        /// Creates CNetLoginGameData from a packet
        /// </summary>
        public static LoginGameData FromPacket(Packet packet)
        {
            LoginGameData data = new LoginGameData();
            data.Deserialize(packet);
            return data;
        }
    }
}

namespace MMORPGServer.Core
{
    public static class PacketFactory
    {
        public static ReadOnlyMemory<byte> CreateProtoPacket(GamePackets packetType, byte[] protoData)
        {
            return PacketBuilder.Create(GamePackets.CMsgTalk)
               .WriteBytes(protoData)
               .BuildAndFinalize();
        }
    }
}
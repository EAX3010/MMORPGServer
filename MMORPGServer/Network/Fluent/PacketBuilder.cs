namespace MMORPGServer.Network.Fluent
{
    public static class PacketBuilder
    {
        public static IPacketWriter Create(GamePackets type) => new FluentPacketWriter(type);
        public static IPacketWriter Create(ushort type) => new FluentPacketWriter(type);

        // Quick factory methods for common packets
        public static IPacketWriter LoginResponse() => Create(GamePackets.LoginGamaEnglish);
    }
}

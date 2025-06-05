namespace MMORPGServer.Network.Fluent
{
    public static class PacketBuilder
    {
        public static IPacketBuilder Create(GamePackets type) => new FluentPacketBuilder(type);
        public static IPacketBuilder Create(ushort type) => new FluentPacketBuilder(type);

        // Quick factory methods for common packets
        public static IPacketBuilder LoginResponse() => Create(GamePackets.LoginGamaEnglish);
    }
}

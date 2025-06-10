namespace MMORPGServer.Infrastructure.Attributes
{
    /// <summary>
    /// Marks a method as a handler for a specific packet type.
    /// The method must have the signature: ValueTask HandlePacketAsync(IGameClient client, Packet packet)
    /// </summary>
    /// <remarks>
    /// Create a packet handler for a specific packet type
    /// </remarks>
    /// <param name="packetType">The packet type to handle</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PacketHandlerAttribute(GamePackets packetType) : Attribute
    {
        public GamePackets PacketType { get; } = packetType;
    }
}
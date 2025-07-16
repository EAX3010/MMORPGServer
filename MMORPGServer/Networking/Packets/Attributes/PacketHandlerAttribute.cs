using MMORPGServer.Common.Enums;

namespace MMORPGServer.Networking.Packets.Attributes
{
    /// <summary>
    /// Attribute to mark packet handlers, supporting both method-level (static) and class-level (instance) handlers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class PacketHandlerAttribute : Attribute
    {
        /// <summary>
        /// The packet type this handler processes.
        /// </summary>
        public GamePackets PacketType { get; }

        /// <summary>
        /// Initializes a new instance of the PacketHandlerAttribute.
        /// </summary>
        /// <param name="packetType">The packet type this handler will process.</param>
        public PacketHandlerAttribute(GamePackets packetType)
        {
            PacketType = packetType;
        }
    }
}
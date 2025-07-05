using MMORPGServer.Common.Enums;

namespace MMORPGServer.Networking.Packets.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PacketHandlerAttribute : Attribute
    {
        public GamePackets PacketType { get; }

        public PacketHandlerAttribute(GamePackets packetType)
        {
            PacketType = packetType;
        }
    }
}
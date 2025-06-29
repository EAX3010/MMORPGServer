using MMORPGServer.Common.Interfaces;

namespace MMORPGServer.Common.ValueObjects
{
    public readonly record struct ClientMessage(IGameClient Client, IPacket Packet);
}

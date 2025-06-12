using MMORPGServer.Repositories;

namespace MMORPGServer.ValueObjects
{
    public readonly record struct ClientMessage(uint ClientId, IPacket Packet);
}

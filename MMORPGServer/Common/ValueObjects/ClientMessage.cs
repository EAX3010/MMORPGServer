using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;

namespace MMORPGServer.Common.ValueObjects
{
    public readonly record struct ClientMessage(GameClient Client, IPacket Packet);
}

using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Core;

namespace MMORPGServer.Common.ValueObjects
{
    public readonly record struct ClientMessage(GameClient Client, Packet Packet);
}

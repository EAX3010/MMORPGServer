using MMORPGServer.Networking.Clients;

namespace MMORPGServer.Common.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(GameClient client, IPacket packet);
    }
}
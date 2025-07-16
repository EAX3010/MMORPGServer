using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Core;

namespace MMORPGServer.Common.Interfaces
{
    public interface IPacketMiddleware
    {
        ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next);
    }
}

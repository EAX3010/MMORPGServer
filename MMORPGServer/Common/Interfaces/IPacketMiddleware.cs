using MMORPGServer.Networking.Clients;

namespace MMORPGServer.Common.Interfaces
{
    public interface IPacketMiddleware
    {
        ValueTask<bool> InvokeAsync(GameClient client, IPacket packet, Func<ValueTask> next);
    }
}

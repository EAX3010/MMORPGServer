using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Structures;

namespace MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers
{
    public interface IActionHandler
    {
        ValueTask<bool> HandleAsync(GameClient client, ActionProto action);
    }
}

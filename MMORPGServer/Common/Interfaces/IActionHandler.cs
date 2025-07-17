using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Structures;

namespace MMORPGServer.Common.Interfaces
{
    public interface IActionHandler
    {
        ValueTask<bool> HandleAsync(GameClient client, ActionProto action);
    }
}

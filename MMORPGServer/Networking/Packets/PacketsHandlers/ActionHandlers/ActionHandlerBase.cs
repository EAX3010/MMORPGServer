using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Packets.Structures;

namespace MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers
{
    public class ActionHandlerBase
    {
        public ReadOnlyMemory<byte> Build(ActionProto proto)
        {
            return new Packet(GamePackets.CMsgAction)
                .SerializeProto(proto).Build(GamePackets.CMsgAction);

        }
    }
}

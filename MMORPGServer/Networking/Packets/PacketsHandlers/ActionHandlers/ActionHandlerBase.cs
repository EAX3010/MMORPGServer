using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Fluent;
using MMORPGServer.Networking.Packets.Structures;

namespace MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers
{
    public class ActionHandlerBase
    {
        public ReadOnlyMemory<byte> Build(ActionProto proto)
        {
            return new FluentPacketWriter(GamePackets.CMsgAction)
               .Seek(4).SerializeProto(proto)
               .BuildAndFinalize();
        }
    }
}

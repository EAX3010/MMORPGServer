using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.PacketsProto;
using ProtoBuf;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    public class TalkHandler
    {

        [PacketHandler(GamePackets.CMsgTalk)]

        public static async ValueTask HandleAsync(GameClient client, IPacket packet)
        {
            // 1. Deserialize the packet payload from Protobuf
            // The new Packet class gives us a ReadOnlySpan of the data
            TalkProto talkData = Serializer.Deserialize<TalkProto>(packet.Data);
            if (talkData?.Strings == null || talkData.Strings.Count < 4)
            {
                Log.Warning("Received invalid TalkPacket from client {ClientId}", client.ClientId);
                return;
            }

            string from = talkData.Strings[0];
            string to = talkData.Strings[1];
            string message = talkData.Strings[3];

            Log.Debug("Received chat message from {From} to {To}: {Message}", from, to, message);



            // 3. Process the message based on ChatType
            switch (talkData.ChatType)
            {
                default:
                    Log.Warning("Unhandled ChatType: {ChatType}", talkData.ChatType);
                    break;
            }

        }
    }
}
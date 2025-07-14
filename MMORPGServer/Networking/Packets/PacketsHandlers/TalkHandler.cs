using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.PacketsProto;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    public class TalkHandler
    {

        [PacketHandler(GamePackets.CMsgTalk)]

        public static async ValueTask HandleAsync(GameClient client, Packet packet)
        {
            // 1. Deserialize the packet payload from Protobuf
            TalkProto talkData = packet.DeserializeProto<TalkProto>();
            if (talkData?.Strings == null || talkData.Strings.Count < 4)
            {
                Log.Warning("Received invalid TalkPacket from client {ClientId}", client.ClientId);
                return;
            }

            string from = talkData.Strings[0];
            string to = talkData.Strings[1];
            string message = talkData.Strings[3];

            Log.Debug("Received chat message from {From} (PlayerId: {PlayerId}) to {To}: {Message} (ChatType: {ChatType})",
                from, client.PlayerId, to, message, talkData.ChatType);

            // 3. Process the message based on ChatType
            switch (talkData.ChatType)
            {
                case ChatType.Talk:
                    Log.Debug("Processing Talk chat for player {PlayerId}", client.PlayerId);
                    // TODO: Handle talk chat logic (e.g., broadcast to nearby players)
                    break;
                case ChatType.Whisper:
                    Log.Debug("Processing Whisper chat from {FromPlayerId} to {ToPlayer}", client.PlayerId, to);
                    // TODO: Handle whisper chat logic (e.g., find target player and send message)
                    break;
                case ChatType.Team:
                    Log.Debug("Processing Team chat for player {PlayerId}", client.PlayerId);
                    // TODO: Handle team chat logic
                    break;
                case ChatType.Guild:
                    Log.Debug("Processing Guild chat for player {PlayerId}", client.PlayerId);
                    // TODO: Handle guild chat logic
                    break;
                default:
                    Log.Warning("Unhandled ChatType: {ChatType} from player {PlayerId}", talkData.ChatType, client.PlayerId);
                    break;
            }
        }
    }
}

using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Packets.Structures;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    [PacketHandler(GamePackets.CMsgTalk)]
    public sealed class Talk : AuthenticatedHandler, IPacketHandler<TalkProto>
    {
        public Talk(Packet packet) : base(packet) { }

        public override async ValueTask ProcessAsync(GameClient client)
        {
            // Deserialize the packet payload from Protobuf
            var talkData = Read();
            if (talkData == null)
            {
                Log.Warning("Failed to read talk data for client {ClientId}", client.ClientId);
                return;
            }

            string from = talkData.Strings[0];
            string to = talkData.Strings[1];
            string message = talkData.Strings[3];

            Log.Debug("Received chat message from {From} (PlayerId: {PlayerId}) to {To}: {Message} (ChatType: {ChatType})",
                from, client.PlayerId, to, message, talkData.ChatType);

            await ProcessChatByTypeAsync(client, talkData, from, to, message);
        }

        public TalkProto? Read()
        {
            try
            {
                TalkProto talkData = Packet.DeserializeProto<TalkProto>();

                if (talkData?.Strings == null || talkData.Strings.Count < 4)
                {
                    Log.Warning("Received invalid TalkPacket - insufficient strings");
                    return null;
                }

                if (!Enum.IsDefined(typeof(ChatType), talkData.ChatType))
                {
                    Log.Warning("Invalid ChatType: {ChatType}", talkData.ChatType);
                    return null;
                }

                return talkData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading talk data");
                return null;
            }
        }

        private async ValueTask ProcessChatByTypeAsync(GameClient client, TalkProto talkData, string from, string to, string message)
        {
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

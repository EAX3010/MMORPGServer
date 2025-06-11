using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Repositories;
using MMORPGServer.Network.Attributes;
using MMORPGServer.Network.Packets;
using ProtoBuf;
using System.Threading.Tasks;

namespace MMORPGServer.Network.Handlers
{
    public class TalkHandler : IPacketProcessor
    {
        private readonly ILogger<TalkHandler> _logger;
        public TalkHandler(ILogger<TalkHandler> logger)
        {
            _logger = logger;
        }

        [PacketHandler(GamePackets.CMsgTalk)]
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            // 1. Deserialize the packet payload from Protobuf
            // The new Packet class gives us a ReadOnlySpan of the data
            TalkProto talkData = Serializer.Deserialize<TalkProto>(packet.Data);
            if (talkData?.Strings == null || talkData.Strings.Count < 4)
            {
                _logger.LogWarning("Received invalid TalkPacket from client {ClientId}", client.ClientId);
                return;
            }

            string from = talkData.Strings[0];
            string to = talkData.Strings[1];
            string message = talkData.Strings[3];

            _logger.LogDebug("Received chat message from {From} to {To}: {Message}", from, to, message);

            // 2. Handle Chat Commands (if any)
            // if (message.StartsWith("/")) {
            //     _commandService.Execute(client, message);
            //     return;
            // }

            // 3. Process the message based on ChatType
            switch (talkData.ChatType)
            {
                default:
                    _logger.LogWarning("Unhandled ChatType: {ChatType}", talkData.ChatType);
                    break;
            }

            await ValueTask.CompletedTask;
        }
    }
}
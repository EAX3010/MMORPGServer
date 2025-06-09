using MMORPGServer.Network.Packets;
using ProtoBuf;

namespace MMORPGServer.Network.Handlers
{
    public class TalkPacketHandler : IPacketProcessor
    {
        private readonly ILogger<TalkPacketHandler> _logger;
        private readonly INetworkManager _networkManager;
        public TalkPacketHandler(ILogger<TalkPacketHandler> logger, INetworkManager networkManager)
        {
            _logger = logger;
            _networkManager = networkManager;
        }

        [PacketHandler(GamePackets.CMsgTalk)]
        public async ValueTask HandleAsync(IGameClient client, Packet packet)
        {
            // 1. Deserialize the packet payload from Protobuf
            // The new Packet class gives us a ReadOnlySpan of the data
            var talkData = Serializer.Deserialize<TalkPacket>(packet.Data);
            if (talkData?.Strings == null || talkData.Strings.Count < 4)
            {
                _logger.LogWarning("Received invalid TalkPacket from client {ClientId}", client.ClientId);
                return;
            }

            var from = talkData.Strings[0];
            var to = talkData.Strings[1];
            var message = talkData.Strings[3];

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

        /// <summary>
        /// A helper method to create the response packet using the modern PacketBuilder.
        /// </summary>

    }
}
using MMORPGServer.Network.Packets;
using ProtoBuf;

namespace MMORPGServer.Network.Handlers
{
    public class TalkPacketHandler : IPacketProcessor
    {
        private readonly ILogger<TalkPacketHandler> _logger;
        private readonly INetworkManager _networkManager;

        // --- Inject any services you need for chat logic ---
        // For example:
        // private readonly IChatService _chatService;

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
        public static ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, uint mesh)
        {
            var talkPacket = new TalkPacket
            {
                ChatType = chatType,
                Mesh = mesh,
                Strings = new List<string> { from, to, "", message, "", suffix, "" }
            };

            // Serialize our object to a byte array using Protobuf
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, talkPacket);
            var payload = memoryStream.ToArray();

            // Use the new PacketBuilder to construct the final packet for sending
            return PacketBuilder.Create(GamePackets.CMsgTalk)
                .WriteBytes(payload)
                .BuildAndFinalize();
        }
    }
}
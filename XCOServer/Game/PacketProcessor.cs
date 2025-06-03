namespace MMORPGServer.Game
{
    public sealed class PacketProcessor : IPacketProcessor
    {
        private readonly ILogger<PacketProcessor> _logger;
        private readonly IPlayerManager _playerManager;
        private readonly IAuthenticationService _authService;
        private readonly ICharacterService _characterService;
        private readonly IChatService _chatService;

        public PacketProcessor(
            ILogger<PacketProcessor> logger,
            IPlayerManager playerManager,
            IAuthenticationService authService,
            ICharacterService characterService,
            IChatService chatService)
        {
            _logger = logger;
            _playerManager = playerManager;
            _authService = authService;
            _characterService = characterService;
            _chatService = chatService;
        }

        public async ValueTask ProcessPacketAsync(IGameClient client, Packet packet)
        {
            try
            {
                switch (packet.Type)
                {
                    default:
                        _logger.LogWarning("Unknown packet type: {PacketType} from client {ClientId}",
                            packet.Type, client.ClientId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
        }

        public void RegisterPacketHandler(ushort packetType, Func<IGameClient, Packet, ValueTask> handler)
        {
            _logger.LogDebug("Registered handler for packet type {PacketType}", packetType);
        }


    }
}
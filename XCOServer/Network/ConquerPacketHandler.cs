namespace MMORPGServer.Network
{
    public sealed class ConquerPacketHandler : IPacketHandler
    {
        private readonly ILogger<ConquerPacketHandler> _logger;
        private readonly IPacketProcessor _packetProcessor;
        private readonly Dictionary<ushort, Func<IGameClient, ConquerPacket, ValueTask>> _handlers = new();

        public ConquerPacketHandler(ILogger<ConquerPacketHandler> logger, IPacketProcessor packetProcessor)
        {
            _logger = logger;
            _packetProcessor = packetProcessor;
        }

        public async ValueTask HandlePacketAsync(IGameClient client, ConquerPacket packet)
        {
            try
            {
                await _packetProcessor.ProcessPacketAsync(client, packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
        }

        public ValueTask HandlePacketAsync(IGameClient client, object packet)
        {
            if (packet is ConquerPacket conquerPacket)
            {
                return HandlePacketAsync(client, conquerPacket);
            }

            _logger.LogWarning("Received non-ConquerPacket from client {ClientId}", client.ClientId);
            return ValueTask.CompletedTask;
        }
        public void RegisterHandler<T>(ushort packetType, Func<IGameClient, T, ValueTask> handler)
            where T : class
        {
            if (typeof(T) == typeof(ConquerPacket))
            {
                _handlers[packetType] = (client, packet) => handler(client, (T)(object)packet);
                _logger.LogDebug("Registered Conquer handler for packet type {PacketType}", packetType);
            }
        }
    }
}
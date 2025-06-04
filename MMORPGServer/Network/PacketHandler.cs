using MMORPGServer.Interfaces;

namespace MMORPGServer.Network
{
    public sealed class PacketHandler : IPacketHandler
    {
        private readonly ILogger<PacketHandler> _logger;
        private readonly IPacketProcessor _packetProcessor;
        private readonly Dictionary<ushort, Func<IGameClient, Packet, ValueTask>> _handlers = new();

        public PacketHandler(ILogger<PacketHandler> logger, IPacketProcessor packetProcessor)
        {
            _logger = logger;
            _packetProcessor = packetProcessor;
        }

        public async ValueTask HandlePacketAsync(IGameClient client, Packet packet)
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

        public ValueTask HandlePacketAsync(IGameClient client, object data)
        {
            if (data is Packet packet)
            {
                return HandlePacketAsync(client, packet);
            }

            _logger.LogWarning("Received unknowen Packet from client {ClientId}", client.ClientId);
            return ValueTask.CompletedTask;
        }
        public void RegisterHandler<T>(ushort packetType, Func<IGameClient, T, ValueTask> handler)
            where T : class
        {
            if (typeof(T) == typeof(Packet))
            {
                _handlers[packetType] = (client, packet) => handler(client, (T)(object)packet);
                _logger.LogDebug("Registered handler for packet type {PacketType}", packetType);
            }
        }
    }
}
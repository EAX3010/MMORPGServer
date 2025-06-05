using MMORPGServer.Interfaces;

namespace MMORPGServer.Network
{
    public sealed class PacketHandler : IPacketHandler
    {
        private readonly ILogger<PacketHandler> _logger;
        private readonly Dictionary<ushort, Func<IGameClient, Packet, ValueTask>> _handlers = new();

        public PacketHandler(ILogger<PacketHandler> logger)
        {
            _logger = logger;
        }
        public ValueTask HandlePacketAsync(IGameClient client, Packet packet)
        {

            if (_handlers.TryGetValue(packet.Type, out var handler))
            {
                return handler(client, packet);
            }
            _logger.LogWarning("No handler registered for packet type {PacketType}", packet.Type);
            return ValueTask.CompletedTask;

        }
        public void RegisterHandler<T>(ushort packetType, Func<IGameClient, T, ValueTask> handler)
            where T : class
        {
            if (typeof(T) == typeof(Packet))
            {
                _handlers[packetType] = (client, packet) =>
                handler(client, (T)(object)packet);
                _logger.LogDebug("Registered handler for packet type {PacketType}", packetType);
            }
        }
    }
}
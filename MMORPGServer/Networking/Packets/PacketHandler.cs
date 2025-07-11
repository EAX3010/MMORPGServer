using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using Serilog;

namespace MMORPGServer.Networking.Packets
{
    public sealed class PacketHandler
    {
        private readonly List<IPacketMiddleware> _middlewares = new();

        public PacketHandler()
        {
            Log.Debug("PacketHandler initialized");
        }

        /// <summary>
        /// Register middleware - no DI needed
        /// </summary>
        public void RegisterMiddleware(IPacketMiddleware middleware)
        {
            _middlewares.Add(middleware);
            Log.Information("Registered middleware {MiddlewareType}", middleware.GetType().Name);
        }

        public async ValueTask HandlePacketAsync(GameClient client, Packet packet)
        {
            var handler = PacketHandlerRegistry.GetHandler(packet.Type);
            if (handler == null)
            {
                Log.Warning("No handler registered for packet type {PacketType}", packet.Type);
                return;
            }

            try
            {
                // Build middleware pipeline (if any)
                if (_middlewares.Count > 0)
                {
                    await ExecuteWithMiddleware(client, packet, handler);
                }
                else
                {
                    // Direct execution - maximum performance
                    await handler(client, packet);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
        }

        private async ValueTask ExecuteWithMiddleware(GameClient client, Packet packet,
            Func<GameClient, Packet, ValueTask> handler)
        {
            // Build middleware pipeline
            Func<ValueTask> pipeline = () => handler(client, packet);

            // Build pipeline from end to start
            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = _middlewares[i];
                var next = pipeline;
                pipeline = async () =>
                {
                    var shouldContinue = await middleware.InvokeAsync(client, packet, next);
                    if (!shouldContinue)
                    {
                        Log.Debug("Middleware {MiddlewareType} blocked packet {PacketType}",
                            middleware.GetType().Name, packet.Type);
                    }
                };
            }

            await pipeline();
        }

        public IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
        {
            return PacketHandlerRegistry.GetRegisteredHandlers();
        }

        public bool IsHandlerRegistered(GamePackets packetType) =>
            PacketHandlerRegistry.IsHandlerRegistered(packetType);
    }
}

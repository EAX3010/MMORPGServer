using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using Serilog;
using System.Reflection;

namespace MMORPGServer.Networking.Packets
{
    public static class PacketHandlerRegistry
    {
        private static readonly Dictionary<GamePackets, Func<GameClient, Packet, ValueTask>> _handlers = new();
        private static readonly Dictionary<GamePackets, string> _handlerNames = new();
        private static bool _isInitialized = false;

        /// <summary>
        /// Auto-discovers and registers all packet handlers in the current assembly
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Log.Warning("PacketHandlerRegistry already initialized");
                return;
            }

            Log.Information("Initializing packet handler registry...");

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var handlerCount = 0;

                // Find all static methods with PacketHandlerAttribute
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                     .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

                    foreach (var method in methods)
                    {
                        var attribute = method.GetCustomAttribute<PacketHandlerAttribute>()!;

                        if (ValidateHandlerMethod(method, attribute.PacketType))
                        {
                            RegisterStaticHandler(method, attribute.PacketType);
                            handlerCount++;
                        }
                    }
                }

                _isInitialized = true;
                Log.Information("Packet handler registry initialized with {HandlerCount} handlers", handlerCount);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize packet handler registry");
                throw;
            }
        }

        /// <summary>
        /// Validates that a handler method has the correct signature
        /// </summary>
        private static bool ValidateHandlerMethod(MethodInfo method, GamePackets packetType)
        {
            // Check return type
            if (method.ReturnType != typeof(ValueTask))
            {
                Log.Error("Handler {MethodName} for packet {PacketType} must return ValueTask",
                    method.Name, packetType);
                return false;
            }

            // Check parameters
            var parameters = method.GetParameters();
            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(GameClient) ||
                parameters[1].ParameterType != typeof(Packet))
            {
                Log.Error("Handler {MethodName} for packet {PacketType} must have signature: " +
                         "static ValueTask HandleAsync(IGameClient client, IPacket packet)",
                    method.Name, packetType);
                return false;
            }

            // Check if packet type already registered
            if (_handlers.ContainsKey(packetType))
            {
                Log.Error("Packet type {PacketType} already has a registered handler", packetType);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a static method as a packet handler
        /// </summary>
        private static void RegisterStaticHandler(MethodInfo method, GamePackets packetType)
        {
            try
            {
                // Create delegate from static method
                var handlerDelegate = (Func<GameClient, Packet, ValueTask>)
                    Delegate.CreateDelegate(typeof(Func<GameClient, Packet, ValueTask>), method);

                _handlers[packetType] = handlerDelegate;
                _handlerNames[packetType] = $"{method.DeclaringType?.Name}.{method.Name}";

                Log.Debug("Registered static handler {HandlerName} for packet {PacketType}",
                    _handlerNames[packetType], packetType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register handler {MethodName} for packet {PacketType}",
                    method.Name, packetType);
            }
        }

        /// <summary>
        /// Gets a handler delegate for the specified packet type
        /// </summary>
        public static Func<GameClient, Packet, ValueTask>? GetHandler(GamePackets packetType)
        {
            _handlers.TryGetValue(packetType, out var handler);
            return handler;
        }

        /// <summary>
        /// Checks if a handler is registered for the packet type
        /// </summary>
        public static bool IsHandlerRegistered(GamePackets packetType)
        {
            return _handlers.ContainsKey(packetType);
        }

        /// <summary>
        /// Gets all registered handlers for debugging
        /// </summary>
        public static IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
        {
            return _handlerNames.AsReadOnly();
        }

        /// <summary>
        /// Gets the total number of registered handlers
        /// </summary>
        public static int GetHandlerCount() => _handlers.Count;
    }
}

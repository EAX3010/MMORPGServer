using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using Serilog;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MMORPGServer.Networking.Packets.Core
{
    /// <summary>
    /// High-performance packet handler registry with no runtime reflection
    /// </summary>
    public static class PacketHandlerRegistry
    {
        // Handler delegates for different handler types
        private static readonly Dictionary<GamePackets, Func<GameClient, Packet, ValueTask>> _staticHandlers = new();
        private static readonly Dictionary<GamePackets, Func<Packet, PacketBaseHandler>> _instanceHandlerFactories = new();
        private static readonly Dictionary<GamePackets, string> _handlerNames = new();

        private static bool _isInitialized = false;
        private static readonly object _initializationLock = new object();

        /// <summary>
        /// Auto-discovers and registers all packet handlers in the current assembly
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Log.Warning("PacketHandlerRegistry is already initialized. Skipping.");
                return;
            }

            lock (_initializationLock)
            {
                if (_isInitialized) return; // Double-check locking pattern

                Log.Information("Initializing packet handler registry...");

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var handlerCount = 0;

                    var types = assembly.GetTypes();
                    Log.Debug("Scanning {TypeCount} types in assembly {AssemblyName} for packet handlers.",
                        types.Length, assembly.GetName().Name);

                    foreach (var type in types)
                    {
                        // Check for class-level PacketHandler attribute (new instance-based approach)
                        var classAttribute = type.GetCustomAttribute<PacketHandlerAttribute>();
                        if (classAttribute != null)
                        {
                            Log.Debug("Found instance-based handler class {ClassName} for packet {PacketType}",
                                type.Name, classAttribute.PacketType);

                            if (ValidateInstanceHandlerClass(type, classAttribute.PacketType))
                            {
                                RegisterInstanceHandler(type, classAttribute.PacketType);
                                handlerCount++;
                            }
                            continue; // Skip method scanning for this type since it's an instance handler
                        }

                        // Check for method-level PacketHandler attributes (legacy static approach)
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                         .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

                        foreach (var method in methods)
                        {
                            var methodAttribute = method.GetCustomAttribute<PacketHandlerAttribute>()!;
                            Log.Debug("Found static handler method {MethodName} for packet {PacketType}",
                                method.Name, methodAttribute.PacketType);

                            if (ValidateStaticHandlerMethod(method, methodAttribute.PacketType))
                            {
                                RegisterStaticHandler(method, methodAttribute.PacketType);
                                handlerCount++;
                            }
                        }
                    }

                    _isInitialized = true;
                    Log.Information("Packet handler registry initialized successfully with {HandlerCount} handlers.", handlerCount);

                    // Log handler distribution for debugging
                    Log.Debug("Handler distribution: {StaticCount} static, {InstanceCount} instance",
                        _staticHandlers.Count, _instanceHandlerFactories.Count);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to initialize packet handler registry");
                    throw;
                }
            }
        }

        /// <summary>
        /// Validates that an instance handler class has the correct structure
        /// </summary>
        private static bool ValidateInstanceHandlerClass(Type type, GamePackets packetType)
        {
            // Check if packet type already registered
            if (_staticHandlers.ContainsKey(packetType) || _instanceHandlerFactories.ContainsKey(packetType))
            {
                Log.Error("Duplicate Handler: Packet type {PacketType} already has a registered handler ({ExistingHandler}). New handler {NewHandler} will be ignored.",
                    packetType, _handlerNames.GetValueOrDefault(packetType, "Unknown"), type.Name);
                return false;
            }

            // Check if it inherits from BasePacketHandler
            if (!typeof(PacketBaseHandler).IsAssignableFrom(type))
            {
                Log.Error("Invalid Handler: Class {ClassName} for packet {PacketType} must inherit from BasePacketHandler.",
                    type.Name, packetType);
                return false;
            }

            // Check for required constructor
            var constructor = type.GetConstructor(new[] { typeof(Packet) });
            if (constructor == null)
            {
                Log.Error("Invalid Handler: Class {ClassName} for packet {PacketType} must have a constructor that accepts a Packet parameter.",
                    type.Name, packetType);
                return false;
            }

            // Validate class is sealed for performance
            if (!type.IsSealed)
            {
                Log.Warning("Performance Warning: Handler class {ClassName} for packet {PacketType} should be sealed for better performance.",
                    type.Name, packetType);
            }

            return true;
        }

        /// <summary>
        /// Validates that a static handler method has the correct signature
        /// </summary>
        private static bool ValidateStaticHandlerMethod(MethodInfo method, GamePackets packetType)
        {
            // Check return type
            if (method.ReturnType != typeof(ValueTask))
            {
                Log.Error("Invalid Handler: Method {MethodName} for packet {PacketType} must return ValueTask.",
                    method.Name, packetType);
                return false;
            }

            // Check parameters
            var parameters = method.GetParameters();
            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(GameClient) ||
                parameters[1].ParameterType != typeof(Packet))
            {
                Log.Error("Invalid Handler: Method {MethodName} for packet {PacketType} has an incorrect signature. Expected: static ValueTask HandleAsync(GameClient client, Packet packet)",
                    method.Name, packetType);
                return false;
            }

            // Check if packet type already registered
            if (_staticHandlers.ContainsKey(packetType) || _instanceHandlerFactories.ContainsKey(packetType))
            {
                Log.Error("Duplicate Handler: Packet type {PacketType} already has a registered handler ({ExistingHandler}). New handler {NewHandler} will be ignored.",
                    packetType, _handlerNames.GetValueOrDefault(packetType, "Unknown"), $"{method.DeclaringType?.Name}.{method.Name}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers an instance-based packet handler class
        /// </summary>
        private static void RegisterInstanceHandler(Type handlerType, GamePackets packetType)
        {
            try
            {
                // Create a factory function that creates instances of the handler
                _instanceHandlerFactories[packetType] = (packet) =>
                {
                    return (PacketBaseHandler)Activator.CreateInstance(handlerType, packet)!;
                };

                _handlerNames[packetType] = handlerType.Name;

                Log.Debug("Registered instance handler {HandlerName} for packet {PacketType}",
                    handlerType.Name, packetType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register instance handler {HandlerName} for packet {PacketType}",
                    handlerType.Name, packetType);
            }
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

                _staticHandlers[packetType] = handlerDelegate;
                var handlerName = $"{method.DeclaringType?.Name}.{method.Name}";
                _handlerNames[packetType] = handlerName;

                Log.Debug("Registered static handler {HandlerName} for packet {PacketType}",
                    handlerName, packetType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create delegate for handler {MethodName} for packet {PacketType}",
                    method.Name, packetType);
            }
        }

        /// <summary>
        /// Handles packet using the appropriate registered handler (no runtime reflection)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask HandlePacketAsync(GameClient client, Packet packet)
        {
            try
            {
                // Check for static handler first (legacy support) - fastest path
                if (_staticHandlers.TryGetValue(packet.Type, out var staticHandler))
                {
                    await staticHandler(client, packet);
                    return;
                }

                // Check for instance handler - no reflection, just virtual method call
                if (_instanceHandlerFactories.TryGetValue(packet.Type, out var factory))
                {
                    var handlerInstance = factory(packet);
                    await handlerInstance.HandleAsync(client); // Virtual method call - no reflection!
                    return;
                }

                Log.Warning("No handler registered for packet type {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling packet {PacketType} for client {ClientId} (Player: {PlayerName})",
                    packet.Type, client.ClientId, client.Player?.Name ?? "N/A");
                throw;
            }
        }

        /// <summary>
        /// Gets a static handler delegate for the specified packet type (legacy support)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<GameClient, Packet, ValueTask>? GetHandler(GamePackets packetType)
        {
            _staticHandlers.TryGetValue(packetType, out var handler);
            return handler;
        }

        /// <summary>
        /// Checks if a handler is registered for the packet type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHandlerRegistered(GamePackets packetType)
        {
            return _staticHandlers.ContainsKey(packetType) || _instanceHandlerFactories.ContainsKey(packetType);
        }

        /// <summary>
        /// Gets all registered handlers for debugging
        /// </summary>
        public static IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
        {
            return _handlerNames;
        }

        /// <summary>
        /// Gets the total number of registered handlers
        /// </summary>
        public static int GetHandlerCount() => _staticHandlers.Count + _instanceHandlerFactories.Count;

        /// <summary>
        /// Gets detailed statistics about the registry
        /// </summary>
        public static (int staticHandlers, int instanceHandlers, int totalHandlers) GetRegistryStatistics()
        {
            return (_staticHandlers.Count, _instanceHandlerFactories.Count,
                   _staticHandlers.Count + _instanceHandlerFactories.Count);
        }

        /// <summary>
        /// Validates the registry is properly initialized and configured
        /// </summary>
        public static bool ValidateRegistry()
        {
            if (!_isInitialized)
            {
                Log.Error("PacketHandlerRegistry is not initialized");
                return false;
            }

            var totalHandlers = GetHandlerCount();
            if (totalHandlers == 0)
            {
                Log.Warning("No packet handlers registered");
                return false;
            }

            Log.Information("Registry validation successful: {TotalHandlers} handlers registered", totalHandlers);
            return true;
        }

        /// <summary>
        /// Logs detailed registry information for debugging
        /// </summary>
        public static void LogRegistryDetails()
        {
            var (staticCount, instanceCount, totalCount) = GetRegistryStatistics();

            Log.Information("=== Packet Handler Registry Details ===");
            Log.Information("Total handlers: {Total}", totalCount);
            Log.Information("Static handlers: {Static}", staticCount);
            Log.Information("Instance handlers: {Instance}", instanceCount);
            Log.Information("Initialization status: {Status}", _isInitialized ? "Initialized" : "Not Initialized");

            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Log.Debug("Registered handlers by type:");
                foreach (var kvp in _handlerNames.OrderBy(x => x.Key.ToString()))
                {
                    var handlerType = _staticHandlers.ContainsKey(kvp.Key) ? "Static" : "Instance";
                    Log.Debug("  {PacketType} -> {HandlerName} ({Type})", kvp.Key, kvp.Value, handlerType);
                }
            }
        }
    }
}
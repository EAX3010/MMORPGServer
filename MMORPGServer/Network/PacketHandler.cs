using MMORPGServer.Attributes;
using System.Reflection;

public sealed class PacketHandler : IPacketHandler
{
    private readonly ILogger<PacketHandler> _logger;
    private readonly Dictionary<GamePackets, HandlerInfo> _handlers = [];

    // This struct holds all precomputed information for invoking handlers
    private readonly struct HandlerInfo
    {
        public readonly Func<IGameClient, Packet, ValueTask> ExecuteHandler { get; }
        public HandlerInfo(Func<IGameClient, Packet, ValueTask> executeHandler) => ExecuteHandler = executeHandler;
    }
    public PacketHandler(ILogger<PacketHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;

        var allTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition);

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

            if (!methods.Any()) continue;

            object? instance = null;
            try
            {
                instance = serviceProvider.GetService(type) ?? Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create instance of {Type}", type.FullName);
                continue;
            }

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<PacketHandlerAttribute>()!;
                var parameters = method.GetParameters();

                // Basic validation of the method signature
                if (parameters.Length < 2 ||
                    parameters[0].ParameterType != typeof(IGameClient) ||
                    parameters[1].ParameterType != typeof(Packet) ||
                    method.ReturnType != typeof(ValueTask))
                {
                    _logger.LogError("Invalid handler signature: {Type}.{Method} for {PacketType}",
                        type.Name, method.Name, attr.PacketType);
                    continue;
                }

                try
                {
                    // Pre-resolve all additional dependencies
                    var additionalArgs = new object?[parameters.Length - 2];
                    for (int i = 2; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        try
                        {
                            additionalArgs[i - 2] = serviceProvider.GetService(paramType);
                            if (additionalArgs[i - 2] == null && !parameters[i].HasDefaultValue)
                            {
                                throw new InvalidOperationException($"Could not resolve parameter of type {paramType.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to resolve parameter {ParamNum} of type {ParamType} for handler {Type}.{Method}",
                                i, paramType.Name, type.Name, method.Name);
                            throw;
                        }
                    }

                    // Create a closure that captures resolved dependencies
                    HandlerInfo handlerInfo = new HandlerInfo(
                        (client, packet) =>
                        {
                            var args = new object[parameters.Length];
                            args[0] = client;
                            args[1] = packet;

                            // Copy pre-resolved dependencies
                            for (int i = 0; i < additionalArgs.Length; i++)
                            {
                                args[i + 2] = additionalArgs[i]!;
                            }

                            try
                            {
                                return (ValueTask)method.Invoke(instance, args)!;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error executing handler {Type}.{Method} for packet {PacketType}",
                                    type.Name, method.Name, attr.PacketType);
                                return ValueTask.CompletedTask;
                            }
                        });

                    _handlers[attr.PacketType] = handlerInfo;
                    _logger.LogInformation("Registered handler: {Type}.{Method} for {PacketType} with {ParamCount} parameters",
                        type.Name, method.Name, attr.PacketType, parameters.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to bind handler: {Type}.{Method} for {PacketType}",
                        type.Name, method.Name, attr.PacketType);
                }
            }
        }
    }

    public ValueTask HandlePacketAsync(IGameClient client, Packet packet)
    {
        if (_handlers.TryGetValue(packet.Type, out var handlerInfo))
        {
            return handlerInfo.ExecuteHandler(client, packet);
        }

        _logger.LogWarning("No handler for packet type {PacketType}", packet.Type);
        return ValueTask.CompletedTask;
    }
}
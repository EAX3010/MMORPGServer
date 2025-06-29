using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using System.Reflection;

public sealed class PacketHandler : IPacketHandler
{
    private readonly ILogger<PacketHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<GamePackets, HandlerInfo> _handlers = [];
    private readonly List<IPacketMiddleware> _middlewares;
    private readonly struct HandlerInfo
    {
        public readonly Func<IGameClient, IPacket, ValueTask> ExecuteHandler { get; }
        public readonly Type HandlerType { get; }
        public readonly MethodInfo Method { get; }

        private readonly List<IPacketMiddleware> _middlewares;


        public HandlerInfo(
            Func<IGameClient, IPacket, ValueTask> executeHandler,
            Type handlerType,
            MethodInfo method)
        {
            ExecuteHandler = executeHandler;
            HandlerType = handlerType;
            Method = method;
        }
    }

    public PacketHandler(ILogger<PacketHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _middlewares = new List<IPacketMiddleware>
        {

        };
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        // Get all IPacketProcessor<T> implementations
        var handlerTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic &&
                       !a.FullName!.StartsWith("System") &&
                       !a.FullName.StartsWith("Microsoft") &&
                       !a.FullName.StartsWith("netstandard"))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Enumerable.Empty<Type>(); }
            })
            .Where(t => !t.IsAbstract &&
                       !t.IsGenericTypeDefinition &&
                       t.GetInterfaces().Any(i => i.IsGenericType &&
                                                 i.GetGenericTypeDefinition() == typeof(IPacketProcessor<>)));

        int registeredCount = 0;

        foreach (var handlerType in handlerTypes)
        {
            _logger.LogDebug("Processing handler type: {HandlerType}", handlerType.FullName);

            // Get the IPacketProcessor<T> interface to determine packet type
            var packetProcessorInterface = handlerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(IPacketProcessor<>));

            if (packetProcessorInterface == null)
            {
                _logger.LogWarning("Handler type {HandlerType} does not implement IPacketProcessor<T>", handlerType.Name);
                continue;
            }

            // Get the generic type argument (T)
            var packetTypeArg = packetProcessorInterface.GetGenericArguments()[0];
            _logger.LogDebug("Handler {HandlerType} has generic argument: {GenericArg}", handlerType.Name, packetTypeArg.FullName);

            // Look for HandleAsync methods
            var handleAsyncMethods = handlerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == "HandleAsync");

            _logger.LogDebug("Found {MethodCount} HandleAsync methods in {HandlerType}", handleAsyncMethods.Count(), handlerType.Name);

            foreach (var method in handleAsyncMethods)
            {
                _logger.LogDebug("Validating method: {HandlerType}.{Method}", handlerType.Name, method.Name);

                if (!ValidateHandlerMethod(handlerType, method))
                {
                    _logger.LogWarning("Method validation failed for {HandlerType}.{Method}", handlerType.Name, method.Name);
                    continue;
                }

                // Extract packet type from the PacketType property instead of generic arg
                var packetType = GetPacketTypeFromProperty(handlerType, packetProcessorInterface);
                _logger.LogDebug("Extracted packet type: {PacketType} from {HandlerType}", packetType, handlerType.Name);

                if (packetType.HasValue)
                {
                    var handlerInfo = CreateHandlerInfo(handlerType, method);
                    if (handlerInfo.HasValue)
                    {
                        _handlers[packetType.Value] = handlerInfo.Value;
                        registeredCount++;

                        _logger.LogInformation("Successfully registered handler: {Type}.{Method} for {PacketType}",
                            handlerType.Name, method.Name, packetType.Value);
                    }
                    else
                    {
                        _logger.LogError("Failed to create handler info for {HandlerType}.{Method}", handlerType.Name, method.Name);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not determine packet type for {HandlerType}", handlerType.Name);
                }
            }
        }

        _logger.LogInformation("Registered {Count} packet handlers from {TypeCount} handler types",
            registeredCount, handlerTypes.Count());
    }

    private bool ValidateHandlerMethod(Type type, MethodInfo method)
    {
        var parameters = method.GetParameters();

        _logger.LogDebug("Validating method {Type}.{Method} with {ParamCount} parameters", type.Name, method.Name, parameters.Length);

        // Must have at least 2 parameters: IGameClient and IPacket-derived
        if (parameters.Length < 2)
        {
            _logger.LogWarning("Handler method {Type}.{Method} must have at least 2 parameters (IGameClient, IPacket), found {ParamCount}",
                type.Name, method.Name, parameters.Length);
            return false;
        }

        // Log parameter types for debugging
        for (int i = 0; i < parameters.Length; i++)
        {
            _logger.LogDebug("Parameter {Index}: {Name} of type {Type}", i, parameters[i].Name, parameters[i].ParameterType.FullName);
        }

        // First parameter must be IGameClient
        if (parameters[0].ParameterType != typeof(IGameClient))
        {
            _logger.LogWarning("Handler method {Type}.{Method} first parameter must be IGameClient, found {ActualType}",
                type.Name, method.Name, parameters[0].ParameterType.FullName);
            return false;
        }

        // Second parameter must implement IPacket
        if (!typeof(IPacket).IsAssignableFrom(parameters[1].ParameterType))
        {
            _logger.LogWarning("Handler method {Type}.{Method} second parameter must implement IPacket, found {ActualType}",
                type.Name, method.Name, parameters[1].ParameterType.FullName);
            return false;
        }

        // Check return type
        if (method.ReturnType != typeof(ValueTask) &&
            method.ReturnType != typeof(Task) &&
            method.ReturnType != typeof(void))
        {
            _logger.LogWarning("Handler method {Type}.{Method} must return ValueTask, Task, or void, found {ReturnType}",
                type.Name, method.Name, method.ReturnType.FullName);
            return false;
        }

        _logger.LogDebug("Method {Type}.{Method} passed validation", type.Name, method.Name);
        return true;
    }

    private GamePackets? GetPacketTypeFromProperty(Type handlerType, Type packetProcessorInterface)
    {
        try
        {
            // Create an instance to access the PacketType property
            using var scope = _serviceProvider.CreateScope();
            var instance = scope.ServiceProvider.GetService(handlerType) ?? Activator.CreateInstance(handlerType);

            if (instance == null)
            {
                _logger.LogWarning("Could not create instance of {HandlerType}", handlerType.Name);
                return null;
            }

            // Get the PacketType property
            var packetTypeProperty = handlerType.GetProperty("PacketType");
            if (packetTypeProperty != null)
            {
                var packetTypeValue = packetTypeProperty.GetValue(instance);
                _logger.LogDebug("PacketType property value: {Value} of type {Type}", packetTypeValue, packetTypeValue?.GetType().Name);

                if (packetTypeValue is GamePackets gamePacket)
                {
                    return gamePacket;
                }
            }
            else
            {
                _logger.LogWarning("PacketType property not found on {HandlerType}", handlerType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get packet type from {HandlerType}", handlerType.Name);
        }

        return null;
    }

    private HandlerInfo? CreateHandlerInfo(Type type, MethodInfo method)
    {
        try
        {
            return new HandlerInfo(
                async (client, packet) =>
                {
                    using var scope = _serviceProvider.CreateScope();

                    try
                    {
                        // Get handler instance from DI container
                        var instance = scope.ServiceProvider.GetRequiredService(type);

                        // Prepare method parameters
                        var parameters = method.GetParameters();
                        var args = new object[parameters.Length];
                        args[0] = client;
                        args[1] = packet;

                        // Resolve additional parameters from DI
                        for (int i = 2; i < parameters.Length; i++)
                        {
                            var paramType = parameters[i].ParameterType;
                            var service = scope.ServiceProvider.GetService(paramType);

                            if (service == null && !parameters[i].HasDefaultValue)
                            {
                                throw new InvalidOperationException(
                                    $"Could not resolve parameter '{parameters[i].Name}' of type {paramType.Name}");
                            }

                            args[i] = service ?? parameters[i].DefaultValue!;
                        }

                        // Invoke the method
                        var result = method.Invoke(instance, args);

                        // Handle different return types
                        switch (result)
                        {
                            case ValueTask valueTask:
                                await valueTask;
                                break;
                            case Task task:
                                await task;
                                break;
                            case null: // void method
                                break;
                            default:
                                {
                                    break;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing handler {Type}.{Method} for packet {PacketType}",
                            type.Name, method.Name, packet.Type);
                    }
                },
                type,
                method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create handler info for {Type}.{Method}",
                type.Name, method.Name);
            return null;
        }
    }

    public async ValueTask HandlePacketAsync(IGameClient client, IPacket packet)
    {
        if (!_handlers.TryGetValue(packet.Type, out var handlerInfo))
        {
            _logger.LogWarning("No handler registered for packet type {PacketType}", packet.Type);
            return;
        }

        try
        {
            // Build middleware pipeline
            Func<ValueTask> pipeline = () => handlerInfo.ExecuteHandler(client, packet);

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
                        _logger.LogDebug("Middleware {MiddlewareType} blocked packet {PacketType} from client {ClientId}",
                            middleware.GetType().Name, packet.Type, client.ClientId);
                    }
                };
            }

            // Execute the pipeline
            await pipeline();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in packet pipeline for {PacketType} from client {ClientId}",
                packet.Type, client.ClientId);
        }
    }

    public IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
    {
        return _handlers.ToDictionary(
            kvp => kvp.Key,
            kvp => $"{kvp.Value.HandlerType.Name}.{kvp.Value.Method.Name}"
        );
    }

    public bool IsHandlerRegistered(GamePackets packetType) => _handlers.ContainsKey(packetType);
}
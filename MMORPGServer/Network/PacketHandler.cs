using MMORPGServer.Attributes;
using System.Reflection;

public sealed class PacketHandler : IPacketHandler
{
    private readonly ILogger<PacketHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<GamePackets, HandlerInfo> _handlers = [];

    // Enhanced struct to hold precomputed information for invoking handlers
    private readonly struct HandlerInfo
    {
        public readonly Func<IGameClient, Packet, ValueTask> ExecuteHandler { get; }
        public readonly Type HandlerType { get; }
        public readonly MethodInfo Method { get; }
        public readonly bool RequiresScope { get; }

        public HandlerInfo(
            Func<IGameClient, Packet, ValueTask> executeHandler,
            Type handlerType,
            MethodInfo method,
            bool requiresScope)
        {
            ExecuteHandler = executeHandler;
            HandlerType = handlerType;
            Method = method;
            RequiresScope = requiresScope;
        }
    }

    public PacketHandler(ILogger<PacketHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        var allTypes = AppDomain.CurrentDomain
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
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition);

        int registeredCount = 0;

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null);

            if (!methods.Any()) continue;

            // Check if this is a proper packet handler class
            bool isPacketHandlerGroup = typeof(IPacketProcessor).IsAssignableFrom(type);
            bool isRegisteredInDI = _serviceProvider.GetService(type) != null;

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<PacketHandlerAttribute>()!;

                if (ValidateHandlerMethod(type, method, attr))
                {
                    var handlerInfo = CreateHandlerInfo(type, method, attr, isPacketHandlerGroup || isRegisteredInDI);
                    if (handlerInfo.HasValue)
                    {
                        _handlers[attr.PacketType] = handlerInfo.Value;
                        registeredCount++;

                        _logger.LogDebug("Registered handler: {Type}.{Method} for {PacketType} (Scoped: {IsScoped})",
                            type.Name, method.Name, attr.PacketType, handlerInfo.Value.RequiresScope);
                    }
                }
            }
        }

        _logger.LogInformation("Registered {Count} packet handlers from {TypeCount} types",
            registeredCount, allTypes.Count());
    }

    private bool ValidateHandlerMethod(Type type, MethodInfo method, PacketHandlerAttribute attr)
    {
        var parameters = method.GetParameters();

        // Basic validation of the method signature
        if (parameters.Length < 2 ||
            parameters[0].ParameterType != typeof(IGameClient) ||
            parameters[1].ParameterType != typeof(Packet))
        {
            _logger.LogError("Invalid handler signature: {Type}.{Method} for {PacketType} - " +
                "First two parameters must be (IGameClient client, Packet packet)",
                type.Name, method.Name, attr.PacketType);
            return false;
        }

        // Check return type (support both ValueTask and Task)
        if (method.ReturnType != typeof(ValueTask) &&
            method.ReturnType != typeof(Task) &&
            method.ReturnType != typeof(void))
        {
            _logger.LogError("Invalid handler return type: {Type}.{Method} for {PacketType} - " +
                "Must return ValueTask, Task, or void",
                type.Name, method.Name, attr.PacketType);
            return false;
        }

        // Check for duplicate handlers
        if (_handlers.ContainsKey(attr.PacketType))
        {
            _logger.LogWarning("Duplicate handler for {PacketType}: {Type}.{Method} - Previous handler will be overwritten",
                attr.PacketType, type.Name, method.Name);
        }

        return true;
    }

    private HandlerInfo? CreateHandlerInfo(Type type, MethodInfo method, PacketHandlerAttribute attr, bool preferScoped)
    {
        var parameters = method.GetParameters();

        try
        {
            if (preferScoped)
            {
                // Use scoped approach for IPacketHandlerGroup types and DI-registered types
                return CreateScopedHandlerInfo(type, method, parameters);
            }
            else
            {
                // Use singleton approach for simple types (backward compatibility)
                return CreateSingletonHandlerInfo(type, method, parameters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create handler info for {Type}.{Method} (PacketType: {PacketType})",
                type.Name, method.Name, attr.PacketType);
            return null;
        }
    }

    private HandlerInfo CreateScopedHandlerInfo(Type type, MethodInfo method, ParameterInfo[] parameters)
    {
        return new HandlerInfo(
            async (client, packet) =>
            {
                // Create a scope for each handler execution
                using var scope = _serviceProvider.CreateScope();

                try
                {
                    // Get handler instance from DI container
                    var instance = scope.ServiceProvider.GetRequiredService(type);

                    // Resolve additional parameters
                    var args = new object[parameters.Length];
                    args[0] = client;
                    args[1] = packet;

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
                            _logger.LogWarning("Unexpected return type from handler {Type}.{Method}: {ReturnType}",
                                type.Name, method.Name, result.GetType().Name);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing scoped handler {Type}.{Method} for packet {PacketType}",
                        type.Name, method.Name, packet.Type);
                }
            },
            type,
            method,
            requiresScope: true);
    }

    private HandlerInfo CreateSingletonHandlerInfo(Type type, MethodInfo method, ParameterInfo[] parameters)
    {
        // Pre-create instance and resolve dependencies (your original approach)
        object? instance;
        try
        {
            instance = _serviceProvider.GetService(type) ?? Activator.CreateInstance(type);
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not create instance of {type.FullName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance of {Type}", type.FullName);
            throw;
        }

        // Pre-resolve additional dependencies
        var additionalArgs = new object?[parameters.Length - 2];
        for (int i = 2; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            try
            {
                additionalArgs[i - 2] = _serviceProvider.GetService(paramType);
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

        return new HandlerInfo(
            (client, packet) =>
            {
                var args = new object[parameters.Length];
                args[0] = client;
                args[1] = packet;

                // Copy pre-resolved dependencies
                for (int i = 0; i < additionalArgs.Length; i++)
                {
                    args[i + 2] = additionalArgs[i] ?? parameters[i + 2].DefaultValue!;
                }

                try
                {
                    var result = method.Invoke(instance, args);

                    return result switch
                    {
                        ValueTask valueTask => valueTask,
                        Task task => new ValueTask(task),
                        null => ValueTask.CompletedTask, // void method
                        _ => ValueTask.CompletedTask
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing singleton handler {Type}.{Method} for packet {PacketType}",
                        type.Name, method.Name, packet.Type);
                    return ValueTask.CompletedTask;
                }
            },
            type,
            method,
            requiresScope: false);
    }

    public ValueTask HandlePacketAsync(IGameClient client, Packet packet)
    {
        if (_handlers.TryGetValue(packet.Type, out var handlerInfo))
        {
            try
            {
                return handlerInfo.ExecuteHandler(client, packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in packet handler for {PacketType}", packet.Type);
                return ValueTask.CompletedTask;
            }
        }

        _logger.LogWarning("No handler registered for packet type {PacketType}", packet.Type);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Get diagnostic information about registered handlers
    /// </summary>
    public IReadOnlyDictionary<GamePackets, string> GetRegisteredHandlers()
    {
        return _handlers.ToDictionary(
            kvp => kvp.Key,
            kvp => $"{kvp.Value.HandlerType.Name}.{kvp.Value.Method.Name}" +
                   $" (Scoped: {kvp.Value.RequiresScope})"
        );
    }

    /// <summary>
    /// Check if a handler is registered for a specific packet type
    /// </summary>
    public bool IsHandlerRegistered(GamePackets packetType) => _handlers.ContainsKey(packetType);
}

using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace MMORPGServer.Networking.Packets.Core
{
    public sealed class ActionHandlerRegistry
    {
        private static readonly Lazy<ActionHandlerRegistry> _instance = new(() => new ActionHandlerRegistry());
        public static ActionHandlerRegistry Instance => _instance.Value;

        private readonly ConcurrentDictionary<ActionType, Func<IActionHandler>> _handlerFactories;

        private ActionHandlerRegistry()
        {
            _handlerFactories = new ConcurrentDictionary<ActionType, Func<IActionHandler>>();
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IActionHandler).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<ActionHandlerAttribute>() != null);

            foreach (var handlerType in handlerTypes)
            {
                var attribute = handlerType.GetCustomAttribute<ActionHandlerAttribute>();
                if (attribute != null)
                {
                    // Create compiled factory function - NO runtime reflection
                    var factory = CreateCompiledHandlerFactory(handlerType);
                    _handlerFactories.TryAdd(attribute.ActionType, factory);
                }
            }
        }

        private static Func<IActionHandler> CreateCompiledHandlerFactory(Type handlerType)
        {
            // Get constructor at startup (one-time reflection)
            var constructor = handlerType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new InvalidOperationException($"Handler {handlerType.Name} must have a parameterless constructor");

            // Create compiled expression - this becomes pure IL code
            var newExpression = Expression.New(constructor);
            var lambdaExpression = Expression.Lambda<Func<IActionHandler>>(
                Expression.Convert(newExpression, typeof(IActionHandler))
            );

            // Compile to native code - no reflection at runtime
            return lambdaExpression.Compile();
        }

        public IActionHandler? GetHandler(ActionType actionType)
        {
            // Pure dictionary lookup + compiled factory call - zero reflection
            return _handlerFactories.TryGetValue(actionType, out var factory) ? factory() : null;
        }

        public bool IsHandlerRegistered(ActionType actionType)
        {
            return _handlerFactories.ContainsKey(actionType);
        }

        public IEnumerable<ActionType> GetRegisteredActionTypes()
        {
            return _handlerFactories.Keys;
        }
    }
}
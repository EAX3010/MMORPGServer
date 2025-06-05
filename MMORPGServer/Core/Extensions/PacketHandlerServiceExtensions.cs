using System.Reflection;

namespace MMORPGServer.Core.Extensions
{
    public static class PacketHandlerServiceExtensions
    {
        /// <summary>
        /// Enhanced packet handler registration with auto-discovery
        /// </summary>
        public static IServiceCollection AddPacketHandlers(this IServiceCollection services)
        {
            // Register the main packet handler
            services.AddSingleton<IPacketHandler, PacketHandler>();

            // Auto-discover and register all IPacketHandlerGroup implementations
            var handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPacketProcessor).IsAssignableFrom(t));

            foreach (var handlerType in handlerTypes)
            {
                // Register as scoped so each packet handling gets fresh instance
                services.AddScoped(handlerType);
            }

            return services;
        }

        /// <summary>
        /// Register packet handlers with custom lifetime
        /// </summary>
        public static IServiceCollection AddPacketHandlers(this IServiceCollection services, ServiceLifetime lifetime)
        {
            services.AddSingleton<IPacketHandler, PacketHandler>();

            var handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPacketProcessor).IsAssignableFrom(t));

            foreach (var handlerType in handlerTypes)
            {
                services.Add(new ServiceDescriptor(handlerType, handlerType, lifetime));
            }

            return services;
        }
    }
}

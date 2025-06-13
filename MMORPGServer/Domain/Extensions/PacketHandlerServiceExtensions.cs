using Microsoft.Extensions.DependencyInjection;
using MMORPGServer.Domain.Interfaces;
using System.Reflection;

namespace MMORPGServer.Domain.Extensions
{
    public static class PacketHandlerServiceExtensions
    {
        /// <summary>
        /// Enhanced packet handler registration with auto-discovery
        /// </summary>
        public static IServiceCollection AddPacketHandlers(this IServiceCollection services, IServiceProvider Gservces)
        {
            // Register the main packet handler
            services.AddSingleton<IPacketHandler, PacketHandler>();

            // Auto-discover and register all IPacketHandlerGroup implementations
            IEnumerable<Type> handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPacketProcessor).IsAssignableFrom(t));

            foreach (Type handlerType in handlerTypes)
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
            IEnumerable<Type> handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPacketProcessor).IsAssignableFrom(t));

            foreach (Type handlerType in handlerTypes)
            {
                services.Add(new ServiceDescriptor(handlerType, handlerType, lifetime));
            }

            services.AddSingleton<IPacketHandler, PacketHandler>();



            return services;
        }
    }
}

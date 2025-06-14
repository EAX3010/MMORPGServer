using Microsoft.Extensions.DependencyInjection;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;
using System.Reflection;

namespace MMORPGServer.Application.Extensions
{
    public static class PacketHandlerServiceExtensions
    {
        /// <summary>
        /// Enhanced packet handler registration with auto-discovery
        /// </summary>
        public static IServiceCollection AddPacketHandlers(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            // Load the application assembly and find all IPacketProcessor implementations
            var assembly = Assembly.Load("MMORPGServer.Application");

            var handlerTypes = assembly
                .GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPacketProcessor<GamePackets>).IsAssignableFrom(t));

            // Register each handler type with the DI container
            foreach (var handlerType in handlerTypes)
            {
                services.Add(new ServiceDescriptor(handlerType, handlerType, lifetime));
            }

            // Register the main packet handler as singleton
            services.AddSingleton<IPacketHandler, PacketHandler>();

            return services;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Infrastructure.Persistence;
using System.Reflection;

namespace MMORPGServer.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Add Entity Framework
            services.AddEntityFramework(configuration);

            // Add repositories (we'll add these later)
            // services.AddRepositories();

            // Add other infrastructure services
            // services.AddCaching();
            // services.AddMessageBus();
            services.AddPacketHandlers(ServiceLifetime.Singleton);
            return services;
        }

        private static IServiceCollection AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection connection string is not configured.");
            }

            services.AddDbContext<GameDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });

                // Development settings
#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
            });

            return services;
        }
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
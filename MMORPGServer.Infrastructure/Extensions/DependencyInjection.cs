using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Infrastructure.Persistence;
using MMORPGServer.Infrastructure.Persistence.Common.Interfaces;
using MMORPGServer.Infrastructure.Persistence.Interceptors;
using MMORPGServer.Infrastructure.Persistence.Repositories;
using System.Reflection;

namespace MMORPGServer.Infrastructure.Extensions
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds infrastructure services to the dependency injection container.
        /// Includes database context, repositories, and external services.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register interceptors
            services.AddSingleton<AuditableEntitySaveChangesInterceptor>();

            // Register database initializer
            services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

            // Configure Entity Framework Core with SQL Server
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            services.AddDbContext<GameDbContext>((serviceProvider, options) =>
            {
                // Get registered interceptors
                var auditableEntitySaveChangesInterceptor = serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>();

                // Configure SQL Server
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    // Enable connection resiliency
                    sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);

                    // Set command timeout for long-running queries
                    sqlServerOptions.CommandTimeout(30);

                    // Use split queries for better performance with includes
                    sqlServerOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

                // Add interceptors
                options.AddInterceptors(auditableEntitySaveChangesInterceptor);

                // Optional: Enable lazy loading (requires proxies package)
                // options.UseLazyLoadingProxies();
            }, ServiceLifetime.Singleton);

            services.AddSingleton<IPlayerRepository, SqlPlayerRepository>();
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
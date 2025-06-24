using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MMORPGServer.Application.Interfaces;
using MMORPGServer.Application.Services;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Infrastructure.Extensions;
using MMORPGServer.Infrastructure.Networking.Packets;
using MMORPGServer.Infrastructure.Networking.Security;
using MMORPGServer.Infrastructure.Networking.Server;
using MMORPGServer.Infrastructure.Persistence;
using MMORPGServer.Infrastructure.Persistence.Repositories;
using MMORPGServer.Infrastructure.Services;
using Serilog;
using Serilog.Events;

namespace MMORPGServer
{
    public static class Program
    {

        public static async Task Main(string[] args)
        {

            try
            {
                ConfigLogger();
                DisplayStartupBanner();


                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
                _ = builder.Services.AddInfrastructure(builder.Configuration);

                // Use Serilog as the logging provider
                _ = builder.Services.AddSerilog();

                // Configure Network services
                _ = builder.Services.AddSingleton<IGameServer, GameServer>();
                _ = builder.Services.AddSingleton<INetworkManager, NetworkManager>();
                _ = builder.Services.AddSingleton<PlayerManager>();
                // Configure Security services
                _ = builder.Services.AddTransient<DiffieHellmanKeyExchange>();
                _ = builder.Services.AddTransient<TQCast5Cryptographer>();

                // Configure Business services
                _ = builder.Services.AddSingleton<IMapRepository, MapRepository>();
                _ = builder.Services.AddSingleton<GameWorld>();

                // Configure Background services
                _ = builder.Services.AddHostedService<GameServerHostedService>();
                _ = builder.Services.AddHostedService<GameLoopService>();
                _ = builder.Services.AddTransient<IPacketFactory, PacketFactory>();

                _ = builder.Services.AddPacketHandlers(ServiceLifetime.Singleton);
                _ = builder.Services.AddSingleton<ITransferCipher, TransferCipher>(service =>
                {
                    return new TransferCipher(builder.Configuration);
                });

                _ = builder.Services.AddSingleton<MapVisualizer>();

                IHost host = builder.Build();

                using IServiceScope scope = host.Services.CreateScope();
                await GameSystems.InitializeAsync(scope);


                Log.Information("MMORPG Server starting up...");
                await host.RunAsync();

            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.Information("MMORPG Server shutting down");
                await Log.CloseAndFlushAsync();
            }
        }

        private static void DisplayStartupBanner()
        {
            try
            {
                // Check if we're running in a console environment
                if (!Console.IsOutputRedirected && Environment.UserInteractive)
                {
                    Console.Clear();
                }

                Console.ForegroundColor = ConsoleColor.Cyan;

                Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║                              MMORPG SERVER v1.0                              ║
║                          High-Performance Game Server                        ║
╚══════════════════════════════════════════════════════════════════════════════╝");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($".NET {Environment.Version} | Built with C# 13");
                Console.WriteLine($"Platform: {Environment.OSVersion}");
                Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                Console.WriteLine($"Processors: {Environment.ProcessorCount}");
                Console.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nGame Features:");
                Console.WriteLine("   • Secure Diffie-Hellman Key Exchange");
                Console.WriteLine("   • CAST5 Encryption");
                Console.WriteLine("   • Rate Limiting & Flood Protection");
                Console.WriteLine("   • High-Performance Networking");
                Console.WriteLine("   • Real-time Game Loop");

                Console.WriteLine("\n" + new string('═', 80));
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception)
            {
                // Ignore console errors during design-time
                Console.WriteLine("MMORPG Server v1.0 - Starting...");
            }
        }

        private static void ConfigLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Information()
               .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
               .MinimumLevel.Override("System", LogEventLevel.Warning)
               .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
               .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
               .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
               .Enrich.FromLogContext()
               .Enrich.WithThreadId()
               .Enrich.WithProcessId()
               .Enrich.WithEnvironmentName()
               .WriteTo.Console(
                   outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                   theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
               .WriteTo.File(
                   path: "logs/server-.log",
                   rollingInterval: RollingInterval.Day,
                   retainedFileCountLimit: 7,
                   outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
               .CreateLogger();

            // Global exception handler for unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled exception occurred");
            };
        }
    }
}
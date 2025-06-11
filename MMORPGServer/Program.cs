using MMORPGServer.Application.Game.World;
using MMORPGServer.Application.Services;
using MMORPGServer.BackgroundServices;
using MMORPGServer.Infrastructure.Networking.Server;
using MMORPGServer.Infrastructure.Persistence.Repositories;
using MMORPGServer.Infrastructure.Security;
using MMORPGServer.Network;

namespace MMORPGServer
{
    public static class Program
    {

        public static async Task Main(string[] args)
        {
            // Configure Serilog first
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
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

            try
            {
                DisplayStartupBanner();

                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

                // Use Serilog as the logging provider
                _ = builder.Services.AddSerilog();

                // Configure Network services
                _ = builder.Services.AddSingleton<IGameServer, GameServer>();
                _ = builder.Services.AddSingleton<INetworkManager, NetworkManager>();

                // Configure Security services
                _ = builder.Services.AddTransient<DiffieHellmanKeyExchange>();
                _ = builder.Services.AddTransient<TQCast5Cryptographer>();

                // Configure Business services
                _ = builder.Services.AddSingleton<IPlayerManager, PlayerManager>();
                _ = builder.Services.AddSingleton<IMapRepository, MapRepository>();
                _ = builder.Services.AddSingleton<IGameWorld, GameWorld>();

                // Configure Background services
                _ = builder.Services.AddHostedService<GameServerHostedService>();
                _ = builder.Services.AddHostedService<GameLoopService>();

                _ = builder.Services.AddPacketHandlers(ServiceLifetime.Singleton);

                IHost host = builder.Build();
                Log.Information("MMORPG Server starting up...");

                // Initialize maps
                IGameWorld gameWorld = host.Services.GetRequiredService<IGameWorld>();
                await InitializeMapsAsync(gameWorld);

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
            Console.Clear();
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

        private static async Task InitializeMapsAsync(IGameWorld gameWorld)
        {
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string gameMapPath = Path.Combine(applicationDataPath, @"Database\ini\GameMap.dat");
            Log.Information("Initializing spatial system for maps...");

            if (!File.Exists(gameMapPath))
            {
                Log.Error("{0} Not found", gameMapPath);
                return;
            }

            using BinaryReader reader = new(File.OpenRead(gameMapPath));
            int mapCount = reader.ReadInt32();
            int i = 0;
            for (i = 0; i < mapCount; i++)
            {
                int mapId = reader.ReadInt32();
                int fileLength = reader.ReadInt32();
                string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength)).Replace(".7z", ".dmap");
                _ = reader.ReadInt32();//  puzzleSize

                _ = await gameWorld.LoadMapAsync((ushort)mapId, fileName);

            }
            Log.Information("Map initialization completed with total maps of {i}", i);
        }
    }
}
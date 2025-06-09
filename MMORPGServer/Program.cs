using MMORPGServer.Game.Maps;
using MMORPGServer.Network.Handlers;
using Serilog;
using Serilog.Events;

namespace MMORPGServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog first
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
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

                var builder = Host.CreateApplicationBuilder(args);

                // Use Serilog as the logging provider
                _ = builder.Services.AddSerilog();

                // Configure Network services
                _ = builder.Services.AddSingleton<IGameServer, GameServer>();
                _ = builder.Services.AddSingleton<INetworkManager, NetworkManager>();

                // Configure Security services
                _ = builder.Services.AddTransient<DiffieHellmanKeyExchange>();
                _ = builder.Services.AddTransient<TQCast5Cryptographer>();

                // Configure Game services
                _ = builder.Services.AddSingleton<GameWorld>();
                _ = builder.Services.AddSingleton<IPlayerManager, PlayerManager>();
                _ = builder.Services.AddSingleton<IMapRepository, MapRepository>();

                // Configure Packet Handlers
                _ = builder.Services.AddSingleton<IPacketHandler, PacketHandler>();
                _ = builder.Services.AddScoped<ActionHandler>();
                _ = builder.Services.AddScoped<TalkHandler>();

                // Configure Hosted Services
                _ = builder.Services.AddHostedService<GameServerHostedService>();

                var host = builder.Build();
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void DisplayStartupBanner()
        {
            Log.Information(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║                         MMORPG Game Server v1.0.0                           ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝");

            Log.Information(".NET {Version} | Built with C# 13", Environment.Version);
            Log.Information("Platform: {Platform}", Environment.OSVersion);
            Log.Information("Memory: {MemoryMB} MB", GC.GetTotalMemory(false) / 1024 / 1024);
            Log.Information("Processors: {ProcessorCount}", Environment.ProcessorCount);
            Log.Information("Started: {StartTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            Log.Information("\nGame Features:");
            Log.Information("   • Secure Diffie-Hellman Key Exchange");
            Log.Information("   • CAST5 Encryption");
            Log.Information("   • Rate Limiting & Flood Protection");
            Log.Information("   • High-Performance Networking");
            Log.Information("   • Real-time Game Loop");

            Log.Information("\n{Separator}", new string('═', 80));
            Log.Information("");
        }

        private static async Task InitializeMapsAsync(GameWorld gameWorld)
        {
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var gameMapPath = Path.Combine(applicationDataPath, @"Database\ini\GameMap.dat");
            Log.Information("Initializing spatial system for maps...");

            if (!File.Exists(gameMapPath))
            {
                Log.Error("{0} Not found", gameMapPath);
                return;
            }

            using (var reader = new BinaryReader(File.OpenRead(gameMapPath)))
            {
                var mapCount = reader.ReadInt32();
                for (var i = 0; i < mapCount; i++)
                {
                    int mapId = reader.ReadInt32();
                    int fileLength = reader.ReadInt32();
                    string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength)).Replace(".7z", ".dmap");
                    int puzzleSize = reader.ReadInt32();

                    await gameWorld.LoadMapAsync((ushort)mapId, fileName);
                }
            }

            Log.Information("Map initialization completed");
        }
    }
}
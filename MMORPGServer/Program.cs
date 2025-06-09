


namespace MMORPGServer
{
    public class Program
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

                var builder = Host.CreateApplicationBuilder(args);

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
                _ = builder.Services.AddSingleton<GameWorld>(provider =>
                {
                    var gameWorld = new GameWorld(playerManager: provider.GetService<IPlayerManager>());
                    InitializeSpatialMaps(gameWorld);

                    return gameWorld;
                });

                // Configure Background services
                _ = builder.Services.AddHostedService<GameServerHostedService>();
                _ = builder.Services.AddHostedService<GameLoopService>();

                _ = builder.Services.AddPacketHandlers(ServiceLifetime.Singleton);

                IHost host = builder.Build();
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
        private static void InitializeSpatialMaps(GameWorld gameWorld)
        {
            Log.Information("Initializing spatial system for maps...");


            // var map = gameWorld.CreateMap((ushort)mapData.Id, mapData.Name, mapData.Width, mapData.Height);
            //Log.Information("Loaded map {MapId} ({MapName}) with spatial grid", mapData.Id, mapData.Name);

            // Log.Information("Spatial system initialization complete");
        }
    }
}
using MMORPGServer.Infrastructure.Database;
using MMORPGServer.Infrastructure.Repositories;
using MMORPGServer.Networking.Packets;
using MMORPGServer.Services;
using Serilog;
using Serilog.Events;
using System.Globalization;

partial class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Set culture for consistent formatting
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            // Initialize logging first
            ConfigLogger();
            DisplayStartupBanner();

            // Initialize configuration
            GameServerConfig.Initialize();
            Log.Information("Configuration loaded successfully");

            // Initialize Database and repositories
            await DatabaseManager.InitializeAsync();
            RepositoryManager.Initialize();

            // Initialize packet handler registry (auto-discovery)
            PacketHandlerRegistry.Initialize();
            Log.Information("Packet handlers auto-discovered: {HandlerCount} handlers registered",
                PacketHandlerRegistry.GetHandlerCount());

            // Initialize all game systems (includes infrastructure)
            await GameSystemsManager.InitializeAsync();

            // Start the game server
            await GameSystemsManager.StartServerAsync();

            // Log initial system status
            GameSystemsManager.LogSystemStatus();

            // Setup graceful shutdown
            await WaitForShutdownSignalAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.Information("Application shutdown complete");
            Log.CloseAndFlush();
        }
    }

    private static async Task WaitForShutdownSignalAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var shutdownRequested = false;

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += async (sender, e) =>
        {
            if (shutdownRequested)
            {
                Log.Warning("Force shutdown requested - terminating immediately");
                Environment.Exit(1);
            }

            e.Cancel = true;
            shutdownRequested = true;
            Log.Information("Shutdown requested by user (Ctrl+C)");

            try
            {
                // Graceful shutdown with timeout
                var shutdownTask = GameSystemsManager.DisposeAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));

                var completedTask = await Task.WhenAny(shutdownTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Log.Warning("Shutdown timed out after 30 seconds");
                }
                else
                {
                    Log.Information("Graceful shutdown completed");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during shutdown");
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        };

        // Setup periodic status logging (every 5 minutes)
        var statusTimer = new Timer(
            callback: _ =>
            {
                try
                {
                    if (GameSystemsManager.IsServerRunning)
                    {
                        GameSystemsManager.LogSystemStatus();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error logging system status");
                }
            },
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5)
        );

        Log.Information("Server is running. Press Ctrl+C to shutdown gracefully.");
        Log.Information("Press Ctrl+C twice for immediate termination.");

        try
        {
            // Wait indefinitely until cancellation
            await Task.Delay(-1, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when shutdown is requested
        }
        finally
        {
            statusTimer.Dispose();
        }
    }

    private static void ConfigLogger()
    {
        // Ensure logs directory exists
        var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            // Add game-specific logging overrides
            .MinimumLevel.Override("GameServer", LogEventLevel.Debug)
            .MinimumLevel.Override("NetworkManager", LogEventLevel.Debug)
            .MinimumLevel.Override("PlayerManager", LogEventLevel.Debug)
            .MinimumLevel.Override("GameWorld", LogEventLevel.Debug)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "MMORPGServer")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "server-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "errors-.log"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            var exception = eventArgs.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}",
                eventArgs.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            Log.Error(eventArgs.Exception, "Unobserved task exception occurred");
            eventArgs.SetObserved();
        };

        Log.Information("Logging system initialized");
    }

    private static void DisplayStartupBanner()
    {
        Log.Information("========================================");
        Log.Information("       MMORPG Server Starting Up       ");
        Log.Information("========================================");
        Log.Information("Version: 1.0.0");
        Log.Information("Build: {BuildDate}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));
        Log.Information("Runtime: {Runtime}", Environment.Version);
        Log.Information("OS: {OS}", Environment.OSVersion);
        Log.Information("Machine: {Machine}", Environment.MachineName);
        Log.Information("========================================");
    }
}
//using MMORPGServer.Infrastructure.Database;
//using MMORPGServer.Infrastructure.Repositories;
//using MMORPGServer.Networking.Packets;
//using MMORPGServer.Services;
//using Serilog;
//using Serilog.Events;

//namespace MMORPGServer
//{
//    public static class Program
//    {

//        //public static async Task Main(string[] args)
//        //{
//        //    try
//        //    {
//        //        ConfigLogger();
//        //        DisplayStartupBanner();
//        //        GameServerConfig.Initialize();
//        //        await DatabaseManager.InitializeAsync();
//        //        RepositoryManager.Initialize();

//        //        // Initialize game systems
//        //        await GameSystemsManager.InitializeAsync();


//        //        PacketHandlerRegistry.Initialize();
//        //        var packetHandler = new PacketHandler();
//        //        // Continue with other services...
//        //        Log.Information("Server initialized with {HandlerCount} packet handlers",
//        //            PacketHandlerRegistry.GetHandlerCount());


//        //        //  IHostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
//        //        // _ = builder.Services.AddInfrastructure(builder.Configuration);
//        //        //_ = builder.Services.AddSerilog();

//        //        //// Configure Network services
//        //        //_ = builder.Services.AddSingleton<INetworkManager, NetworkManager>();
//        //        //_ = builder.Services.AddSingleton<IMapRepository, MapRepository>();
//        //        //_ = builder.Services.AddSingleton<IGameServer, GameServer>();
//        //        //_ = builder.Services.AddSingleton<PlayerManager>();
//        //        //_ = builder.Services.AddSingleton<IGameWorld, GameWorld>();
//        //        //_ = builder.Services.AddTransient<IPacketFactory, PacketFactory>();
//        //        //_ = builder.Services.AddPacketHandlers(ServiceLifetime.Singleton);

//        //        //// Configure cryptography services
//        //        //_ = builder.Services.AddTransient<DiffieHellmanKeyExchange>();
//        //        //_ = builder.Services.AddTransient<TQCast5Cryptographer>();
//        //        // _ = builder.Services.AddSingleton<ITransferCipher, TransferCipher>(service =>
//        //        // {
//        //        // return new TransferCipher(builder.Configuration);
//        //        // });

//        //        //// Add utilities and visualization
//        //        //_ = builder.Services.AddSingleton<MapVisualizer>();

//        //        //// Add hosted services (background services)
//        //        //_ = builder.Services.AddHostedService<GameServerHostedService>();
//        //        //_ = builder.Services.AddHostedService<GameLoopService>();

//        //        //// Build the host
//        //        //IHost host = builder.Build();

//        //        //// Initialize game systems
//        //        //using IServiceScope scope = host.Services.CreateScope();
//        //        //await GameSystems.InitializeAsync(scope);

//        //        //Log.Information("MMORPG Server starting up...");

//        //        // Handle Ctrl+C gracefully for console applications
//        //        Console.CancelKeyPress += (sender, e) =>
//        //        {
//        //            e.Cancel = true;
//        //            Environment.ExitCode = 0;
//        //            Environment.Exit(0);
//        //            Log.Information("Shutdown requested by user (Ctrl+C)");
//        //        };
//        //        while (true)
//        //        {
//        //            Thread.Sleep(1000);
//        //        }
//        //        //// Run the host (this blocks until shutdown)
//        //        ////await host.RunAsync();
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        Log.Fatal(ex, "Application terminated unexpectedly");
//        //        Environment.ExitCode = 1;
//        //        throw;
//        //    }
//        //    finally
//        //    {
//        //        Log.Information("MMORPG Server shutting down");
//        //        await Log.CloseAndFlushAsync();
//        //    }
//        //}

//        private static void DisplayStartupBanner()
//        {
//            try
//            {
//                // Check if we're running in a console environment
//                if (!Console.IsOutputRedirected && Environment.UserInteractive)
//                {
//                    Console.Clear();
//                }

//                Console.ForegroundColor = ConsoleColor.Cyan;

//                Console.WriteLine(@"
//╔══════════════════════════════════════════════════════════════════════════════╗
//║                              MMORPG SERVER v1.0                              ║
//║                          High-Performance Game Server                        ║
//╚══════════════════════════════════════════════════════════════════════════════╝");

//                Console.ForegroundColor = ConsoleColor.Yellow;
//                Console.WriteLine($".NET {Environment.Version} | Built with C# 13");
//                Console.WriteLine($"Platform: {Environment.OSVersion}");
//                Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
//                Console.WriteLine($"Processors: {Environment.ProcessorCount}");
//                Console.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

//                Console.ForegroundColor = ConsoleColor.Green;
//                Console.WriteLine("\nGame Features:");
//                Console.WriteLine("   • Secure Diffie-Hellman Key Exchange");
//                Console.WriteLine("   • CAST5 Encryption");
//                Console.WriteLine("   • Rate Limiting & Flood Protection");
//                Console.WriteLine("   • High-Performance Networking");
//                Console.WriteLine("   • Real-time Game Loop");

//                Console.ForegroundColor = ConsoleColor.White;
//                Console.WriteLine("\nControls:");
//                Console.WriteLine("   • Press Ctrl+C to stop the server");
//                Console.WriteLine("   • Server will run until manually stopped");

//                Console.WriteLine("\n" + new string('═', 80));
//                Console.ResetColor();
//                Console.WriteLine();
//            }
//            catch (Exception)
//            {
//                // Ignore console errors during design-time
//                Console.WriteLine("MMORPG Server v1.0 - Starting...");
//            }
//        }

//        private static void ConfigLogger()
//        {
//            Log.Logger = new LoggerConfiguration()
//               .MinimumLevel.Information()
//               .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//               .MinimumLevel.Override("System", LogEventLevel.Warning)
//               .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
//               .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
//               .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
//               .Enrich.FromLogContext()
//               .Enrich.WithThreadId()
//               .Enrich.WithProcessId()
//               .Enrich.WithEnvironmentName()
//               .WriteTo.Console(
//                   outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
//                   theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
//               .WriteTo.File(
//                   path: "logs/server-.log",
//                   rollingInterval: RollingInterval.Day,
//                   retainedFileCountLimit: 7,
//                   outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
//               .CreateLogger();

//            // Global exception handler for unhandled exceptions
//            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
//            {
//                Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled exception occurred");
//            };
//        }
//    }
//}
using MMORPGServer.Database;
using MMORPGServer.Networking.Packets;
using MMORPGServer.Services;
using Serilog;
using Serilog.Events;
using System.Globalization;

partial class Program
{
    public static async Task Main(string[] args)
    {
        // Set culture for consistent formatting
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // Initialize logging first
        ConfigLogger();

        try
        {
            DisplayStartupBanner();

            // Initialize configuration
            GameServerConfig.Initialize();
            Log.Information("Configuration loaded successfully");

            // Initialize Database and repositories
            await DbContextFactory.InitializeAsync();
            await RepositoryManager.Initialize();

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
                    Log.Warning("Graceful shutdown timed out after 30 seconds");
                }
                else
                {
                    Log.Information("Graceful shutdown completed");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during graceful shutdown");
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
                    Log.Error(ex, "Error during periodic system status logging");
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
            Log.Debug("Shutdown cancellation token triggered.");
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
            .MinimumLevel.Debug() // Set minimum to Debug to capture all levels
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", "MMORPGServer")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information, // Only show Info and higher in console
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate)
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "server-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "errors-.log"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
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
        Log.Information(
            "\n========================================\n" +
            "       MMORPG Server Starting Up       \n" +
            "========================================\n" +
            "Version: 1.0.0\n" +
            "Build: {BuildDate}\n" +
            "Runtime: {Runtime}\n" +
            "OS: {OS}\n" +
            "Machine: {Machine}\n" +
            "========================================",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            Environment.Version,
            Environment.OSVersion,
            Environment.MachineName);
    }
}
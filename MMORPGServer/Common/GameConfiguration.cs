using Microsoft.Extensions.Configuration;
using Serilog;

public static class GameServerConfig
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??
        throw new InvalidOperationException("Configuration not initialized. Call Initialize() first.");

    public static void Initialize()
    {
        CreateDefaultConfigFiles();
        _configuration = BuildConfiguration();
        Log.Information("Game server configuration initialized");
    }

    private static void CreateDefaultConfigFiles()
    {
        // Create appsettings.json if it doesn't exist
        if (!File.Exists("appsettings.json"))
        {
            Log.Information("Creating default appsettings.json");
            var defaultConfig = new
            {
                ConnectionStrings = new
                {
                    DefaultConnection = "Server=localhost\\MSSQLSERVER22;Database=mmorpg_game;User Id=sa;Password=65536653dD;TrustServerCertificate=True;MultipleActiveResultSets=true"
                },
                TransferCipher = new
                {
                    IP = "127.0.0.99",
                    Key = "xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj",
                    Salt = "z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX"
                },
                Server = new
                {
                    Name = "MMORPG Server",
                    Port = 7777,
                    MaxPlayers = 1000,
                    TickRate = 60,
                    TimeoutSeconds = 30
                },
                Network = new
                {
                    BufferSize = 8192,
                    MaxConnections = 1000,
                    KeepAliveInterval = 30
                },
                Game = new
                {
                    WorldSaveInterval = 300,
                    AutoSave = true,
                    StartingLevel = 1,
                    MaxLevel = 100,
                    ExperienceRate = 1.0,
                    DropRate = 1.0
                },
                Security = new
                {
                    EnableEncryption = true,
                    SessionTimeout = 3600,
                    MaxLoginAttempts = 5,
                    BanDuration = 300
                },
                Logging = new
                {
                    LogLevel = "Information",
                    LogToFile = true,
                    LogToConsole = true,
                    MaxLogFiles = 7
                }
            };

            File.WriteAllText("appsettings.json", System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        // Create logs directory
        if (!Directory.Exists("logs"))
        {
            Directory.CreateDirectory("logs");
            Log.Information("Created logs directory");
        }

        // Create data directory for game data
        if (!Directory.Exists("data"))
        {
            Directory.CreateDirectory("data");
            Log.Information("Created data directory");
        }

        // Create maps directory
        if (!Directory.Exists("maps"))
        {
            Directory.CreateDirectory("maps");
            Log.Information("Created maps directory");
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables("GAMESERVER_") // Use GAMESERVER_ prefix instead of ASPNETCORE_
            .AddCommandLine(Environment.GetCommandLineArgs().Skip(1).ToArray());

        var configuration = builder.Build();

        // Validate required settings
        ValidateConfiguration(configuration);

        return configuration;
    }

    private static void ValidateConfiguration(IConfiguration config)
    {
        var requiredSettings = new[]
        {
            "Server:Port",
            "Server:MaxPlayers",
            "ConnectionStrings:DefaultConnection",
            "TransferCipher:Key",
            "TransferCipher:Salt"
        };

        foreach (var setting in requiredSettings)
        {
            if (string.IsNullOrEmpty(config[setting]))
            {
                throw new InvalidOperationException($"Required configuration setting '{setting}' is missing or empty");
            }
        }

        Log.Information("Configuration validation passed");
    }

    // Helper methods for common config values
    public static string GetConnectionString() =>
        Configuration["ConnectionStrings:DefaultConnection"] ??
        throw new InvalidOperationException("Database connection string not found");

    // TransferCipher configuration
    public static string TransferCipherIP => Configuration.GetValue("TransferCipher:IP", "127.0.0.99");
    public static string TransferCipherKey => Configuration.GetValue("TransferCipher:Key", "");
    public static string TransferCipherSalt => Configuration.GetValue("TransferCipher:Salt", "");

    public static int ServerPort => Configuration.GetValue("Server:Port", 10033);
    public static int MaxPlayers => Configuration.GetValue("Server:MaxPlayers", 1000);
    public static int TickRate => Configuration.GetValue("Server:TickRate", 60);
    public static string ServerName => Configuration.GetValue("Server:Name", "MMORPG Server");

    public static int BufferSize => Configuration.GetValue("Network:BufferSize", 8192);
    public static int MaxConnections => Configuration.GetValue("Network:MaxConnections", 1000);

    public static bool EnableEncryption => Configuration.GetValue("Security:EnableEncryption", true);
    public static int SessionTimeout => Configuration.GetValue("Security:SessionTimeout", 3600);

    public static int WorldSaveInterval => Configuration.GetValue("Game:WorldSaveInterval", 300);
    public static bool AutoSave => Configuration.GetValue("Game:AutoSave", true);
    public static double ExperienceRate => Configuration.GetValue("Game:ExperienceRate", 1.0);

    // Generic helper for custom sections
    public static T GetSection<T>(string sectionName) where T : new() =>
        Configuration.GetSection(sectionName).Get<T>() ?? new T();

    public static string GetValue(string key, string defaultValue = "") =>
        Configuration.GetValue(key, defaultValue);

    public static int GetValue(string key, int defaultValue) =>
        Configuration.GetValue(key, defaultValue);

    public static bool GetValue(string key, bool defaultValue) =>
        Configuration.GetValue(key, defaultValue);

    public static double GetValue(string key, double defaultValue) =>
        Configuration.GetValue(key, defaultValue);
}


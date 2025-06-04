

using MMORPGServer.Game;
using MMORPGServer.Interfaces;
using MMORPGServer.Network.Servers;
using MMORPGServer.Services;

namespace MMORPGServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure Network services
            builder.Services.AddSingleton<IGameServer, GameServer>();
            builder.Services.AddSingleton<INetworkManager, NetworkManager>();
            builder.Services.AddSingleton<IPacketHandler, PacketHandler>();

            // Configure Security services
            builder.Services.AddTransient<DiffieHellmanKeyExchange>();
            builder.Services.AddTransient<TQCast5Cryptographer>();

            // Configure Game services
            builder.Services.AddSingleton<IPacketProcessor, PacketProcessor>();

            // Configure Business services
            builder.Services.AddSingleton<IPlayerManager, PlayerManager>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<ICharacterService, CharacterService>();
            builder.Services.AddSingleton<IChatService, ChatService>();

            // Configure Background services - THIS IS THE KEY PART!
            builder.Services.AddHostedService<GameServerHostedService>(); // ← This starts the server
            builder.Services.AddHostedService<GameLoopService>();

            // Configure logging
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var host = builder.Build();

            try
            {
                await host.RunAsync(); // ← This triggers StartAsync on all hosted services
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogCritical(ex, "Application terminated unexpectedly");
                throw;
            }


        }
    }

}
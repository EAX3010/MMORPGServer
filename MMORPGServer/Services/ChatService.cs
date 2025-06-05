using MMORPGServer.Interfaces;

namespace MMORPGServer.Services
{
    public sealed class ChatService : IChatService
    {
        private readonly ILogger<ChatService> _logger;
        private readonly INetworkManager _networkManager;

        public ChatService(ILogger<ChatService> logger, INetworkManager networkManager)
        {
            _logger = logger;
            _networkManager = networkManager;
        }

        public async ValueTask BroadcastMessageAsync(uint senderId, string message)
        {
            _logger.LogInformation("Broadcasting message from {SenderId}: {Message}", senderId, message);
            await ValueTask.CompletedTask;
        }

        public async ValueTask SendPrivateMessageAsync(uint senderId, uint receiverId, string message)
        {
            _logger.LogInformation("Private message from {SenderId} to {ReceiverId}: {Message}",
                senderId, receiverId, message);
            await ValueTask.CompletedTask;
        }

        public async ValueTask BroadcastToMapAsync(uint mapId, uint senderId, string message)
        {
            _logger.LogInformation("Map {MapId} message from {SenderId}: {Message}",
                mapId, senderId, message);
            await ValueTask.CompletedTask;
        }

        public async ValueTask BroadcastSystemMessageAsync(string message)
        {
            _logger.LogInformation("System message: {Message}", message);
            await ValueTask.CompletedTask;
        }
    }
}
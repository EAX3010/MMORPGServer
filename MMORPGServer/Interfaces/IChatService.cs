namespace MMORPGServer.Interfaces
{
    public interface IChatService
    {
        ValueTask BroadcastMessageAsync(uint senderId, string message);
        ValueTask SendPrivateMessageAsync(uint senderId, uint receiverId, string message);
        ValueTask BroadcastToMapAsync(uint mapId, uint senderId, string message);
        ValueTask BroadcastSystemMessageAsync(string message);
    }
}
namespace MMORPGServer.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, Packet packet);
    }
}
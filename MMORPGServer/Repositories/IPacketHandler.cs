namespace MMORPGServer.Repositories
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, IPacket packet);
    }
}
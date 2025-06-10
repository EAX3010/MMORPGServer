namespace MMORPGServer.Domain.Repositories
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, IPacket packet);
    }
}
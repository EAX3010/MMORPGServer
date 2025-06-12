namespace MMORPGServer.Repositories
{
    public interface IPacketProcessor
    {
        ValueTask HandleAsync(IGameClient client, IPacket packet);
    }
}

namespace MMORPGServer.Interfaces
{
    public interface IPacketProcessor
    {
        ValueTask HandleAsync(IGameClient client, Packet packet);
    }
}

namespace MMORPGServer.Domain.Interfaces
{
    public interface IPacketProcessor
    {
        ValueTask HandleAsync(IGameClient client, IPacket packet);
    }
}

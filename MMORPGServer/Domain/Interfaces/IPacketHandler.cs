namespace MMORPGServer.Domain.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, IPacket packet);
    }
}
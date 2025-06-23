namespace MMORPGServer.Domain.Common.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, IPacket packet);
    }
}
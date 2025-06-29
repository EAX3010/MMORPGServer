namespace MMORPGServer.Common.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, IPacket packet);
    }
}
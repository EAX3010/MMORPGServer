namespace MMORPGServer.Domain.Interfaces
{
    public interface IPacketProcessor<T> where T : Enum
    {
        T PacketType { get; }
        ValueTask HandleAsync(IGameClient client, IPacket packet);
    }
}

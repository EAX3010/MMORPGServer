namespace MMORPGServer.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, Packet packet);
        void RegisterHandler<T>(ushort packetType, Func<IGameClient, Packet, ValueTask> handler);
    }
}
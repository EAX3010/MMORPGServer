namespace MMORPGServer.Network.Interfaces
{
    public interface IPacketHandler
    {
        ValueTask HandlePacketAsync(IGameClient client, object packet);
        void RegisterHandler<T>(ushort packetType, Func<IGameClient, T, ValueTask> handler) where T : class;
    }
}
namespace MMORPGServer.Game.Interfaces
{
    public interface IPacketProcessor
    {
        ValueTask ProcessPacketAsync(IGameClient client, Packet packet);
        void RegisterPacketHandler(ushort packetType, Func<IGameClient, Packet, ValueTask> handler);
    }
}
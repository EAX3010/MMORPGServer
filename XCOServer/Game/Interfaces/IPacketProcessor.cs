namespace MMORPGServer.Game.Interfaces
{
    public interface IPacketProcessor
    {
        ValueTask ProcessPacketAsync(IGameClient client, ConquerPacket packet);
        void RegisterPacketHandler(ushort packetType, Func<IGameClient, ConquerPacket, ValueTask> handler);
    }
}
namespace MMORPGServer.Interfaces
{
    public interface IPacketSerializable
    {
        void Serialize(Packet packet);
    }

}

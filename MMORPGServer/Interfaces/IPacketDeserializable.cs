namespace MMORPGServer.Interfaces
{
    public interface IPacketDeserializable
    {
        void Deserialize(Packet packet);
    }
}

namespace MMORPGServer.Domain.ValueObjects
{
    public readonly record struct ClientMessage(uint ClientId, IPacket Packet);
}

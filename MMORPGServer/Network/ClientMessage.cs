namespace MMORPGServer.Network
{
    public readonly record struct ClientMessage(uint ClientId, Packet Packet);
}

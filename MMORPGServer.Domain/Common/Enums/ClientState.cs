namespace MMORPGServer.Domain.Common.Enums
{
    public enum ClientState
    {
        Connecting,
        WaitingForDummyPacket,
        DhKeyExchange,
        Connected,
        Disconnected
    }
}
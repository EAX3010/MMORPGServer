namespace MMORPGServer.Core.Enums
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
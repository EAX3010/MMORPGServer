namespace MMORPGServer.Common.Enums
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
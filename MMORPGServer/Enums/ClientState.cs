namespace MMORPGServer.Enums
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
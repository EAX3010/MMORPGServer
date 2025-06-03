namespace MMORPGServer.Security.Interfaces
{
    public interface IDHKeyExchange
    {
        String GenerateRequest();
        String GenerateResponse(String publicKey);
        byte[] GetSecret();
        void HandleResponse(String PubKey);
        Packet CreateDHKeyPacket();
    }
}
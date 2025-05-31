namespace MMORPGServer.Security.Interfaces
{
    public interface IDHKeyExchange
    {
        string GenerateRequest();
        void HandleResponse(string publicKey);
        byte[] GetSharedSecret();
        string SharedSecretHex { get; }
    }
}
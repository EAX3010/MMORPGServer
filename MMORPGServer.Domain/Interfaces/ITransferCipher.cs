namespace MMORPGServer.Domain.Interfaces
{
    public interface ITransferCipher
    {
        uint[] Decrypt(uint[] input);
        uint[] Encrypt(uint[] input);
    }
}
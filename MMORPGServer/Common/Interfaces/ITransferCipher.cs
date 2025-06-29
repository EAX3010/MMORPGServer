namespace MMORPGServer.Common.Interfaces
{
    public interface ITransferCipher
    {
        uint[] Decrypt(uint[] input);
        uint[] Encrypt(uint[] input);
    }
}
namespace MMORPGServer.Domain.Interfaces
{
    public interface ITransferCipher
    {
        int[] Decrypt(int[] input);
        int[] Encrypt(int[] input);
    }
}
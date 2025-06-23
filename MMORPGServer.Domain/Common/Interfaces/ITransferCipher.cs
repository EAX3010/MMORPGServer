namespace MMORPGServer.Domain.Common.Interfaces
{
    public interface ITransferCipher
    {
        int[] Decrypt(int[] input);
        int[] Encrypt(int[] input);
    }
}
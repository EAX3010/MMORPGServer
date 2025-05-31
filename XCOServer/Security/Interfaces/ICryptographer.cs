namespace MMORPGServer.Security.Interfaces
{
    public interface ICryptographer : IDisposable
    {
        void GenerateKey(ReadOnlySpan<byte> keyData);
        void Reset();
        void Encrypt(ReadOnlySpan<byte> input, Span<byte> output);
        void Decrypt(ReadOnlySpan<byte> input, Span<byte> output);
        bool IsInitialized { get; }
    }
}

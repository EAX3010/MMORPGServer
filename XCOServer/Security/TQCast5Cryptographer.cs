public sealed class TQCast5Cryptographer : ICryptographer
{
    private byte[] _encryptIV = new byte[16];
    private byte[] _decryptIV = new byte[16];
    private int[] _encryptCounter = new int[8];
    private int[] _decryptCounter = new int[8];
    private TQCast5FullImplementation? _implementation;

    public bool IsInitialized => _implementation != null;

    public void GenerateKey(ReadOnlySpan<byte> keyData)
    {
        if (keyData.Length < 16)
            throw new ArgumentException("Key data must be at least 16 bytes", nameof(keyData));

        var key = new byte[16];
        keyData[..16].CopyTo(key);
        _implementation = new TQCast5FullImplementation(key);
        Reset();
    }

    public void Reset()
    {
        _encryptIV = new byte[16];
        _decryptIV = new byte[16];
        _encryptCounter = new int[8];
        _decryptCounter = new int[8];
    }

    public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (_implementation == null)
            throw new InvalidOperationException("Cryptographer not initialized");
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output buffers must be the same length");

        for (int i = 0; i < input.Length; i++)
        {
            int n = _encryptCounter[0];

            if (n == 0)
            {
                _implementation.EncryptBlock(_encryptIV, 0, _encryptIV, 0);
            }

            var encrypted = (byte)((input[i] ^ _encryptIV[n]) & 0xff);
            output[i] = encrypted;
            _encryptIV[n] = encrypted;

            n = (n + 1) & 0x07;
            _encryptCounter[0] = n;
        }
    }

    public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (_implementation == null)
            throw new InvalidOperationException("Cryptographer not initialized");
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output buffers must be the same length");

        for (int i = 0; i < input.Length; i++)
        {
            int n = _decryptCounter[0];

            if (n == 0)
            {
                _implementation.EncryptBlock(_decryptIV, 0, _decryptIV, 0);
            }

            var inputByte = input[i];
            var ivByte = _decryptIV[n];
            _decryptIV[n] = inputByte;
            output[i] = (byte)((ivByte ^ inputByte) & 0xff);

            n = (n + 1) & 0x07;
            _decryptCounter[0] = n;
        }
    }

    public void Dispose()
    {
        _implementation?.Dispose();
        _encryptIV?.AsSpan().Clear();
        _decryptIV?.AsSpan().Clear();
    }
}
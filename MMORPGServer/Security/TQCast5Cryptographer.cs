public sealed class TQCast5Cryptographer : IDisposable
{
    private byte[] _encryptIV;
    private byte[] _decryptIV;
    private int _encryptCounter;
    private int _decryptCounter;
    private TQCast5FullImplementation? _implementation;

    // Pre-allocated buffers for block encryption
    private readonly byte[] _tempEncryptBlock = new byte[8];
    private readonly byte[] _tempDecryptBlock = new byte[8];

    public bool IsInitialized => _implementation != null;

    public TQCast5Cryptographer()
    {
        _encryptIV = new byte[16];
        _decryptIV = new byte[16];
    }

    public void GenerateKey(ReadOnlySpan<byte> keyData)
    {
        if (keyData.Length < 16)
            throw new ArgumentException("Key data must be at least 16 bytes", nameof(keyData));

        var key = new byte[16];
        keyData.Slice(0, 16).CopyTo(key);

        _implementation = new TQCast5FullImplementation(key);
        Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Array.Clear(_encryptIV);
        Array.Clear(_decryptIV);
        _encryptCounter = 0;
        _decryptCounter = 0;
    }

    public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (_implementation == null)
            throw new InvalidOperationException("Cryptographer not initialized");
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output buffers must be the same length");

        // Process in chunks when possible
        if (input.Length >= 64)
        {
            EncryptOptimized(input, output);
        }
        else
        {
            EncryptStandard(input, output);
        }
    }

    private void EncryptStandard(ReadOnlySpan<byte> input, Span<byte> output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (_encryptCounter == 0)
            {
                _implementation!.EncryptBlock(_encryptIV, 0, _encryptIV, 0);
            }

            var encrypted = (byte)(input[i] ^ _encryptIV[_encryptCounter]);
            output[i] = encrypted;
            _encryptIV[_encryptCounter] = encrypted;

            _encryptCounter = (_encryptCounter + 1) & 0x07;
        }
    }

    private unsafe void EncryptOptimized(ReadOnlySpan<byte> input, Span<byte> output)
    {
        fixed (byte* pInput = input)
        fixed (byte* pOutput = output)
        fixed (byte* pIV = _encryptIV)
        {
            int length = input.Length;
            int i = 0;

            // Process 8 bytes at a time when aligned
            while (i + 8 <= length)
            {
                if (_encryptCounter == 0)
                {
                    _implementation!.EncryptBlock(_encryptIV, 0, _encryptIV, 0);

                    // Process 8 bytes at once
                    ulong* ivBlock = (ulong*)pIV;
                    ulong* inputBlock = (ulong*)(pInput + i);
                    ulong* outputBlock = (ulong*)(pOutput + i);

                    ulong encrypted = *inputBlock ^ *ivBlock;
                    *outputBlock = encrypted;
                    *ivBlock = encrypted;

                    i += 8;
                    _encryptCounter = 0;
                }
                else
                {
                    // Handle unaligned case
                    var encrypted = (byte)(pInput[i] ^ pIV[_encryptCounter]);
                    pOutput[i] = encrypted;
                    pIV[_encryptCounter] = encrypted;
                    _encryptCounter = (_encryptCounter + 1) & 0x07;
                    i++;
                }
            }

            // Process remaining bytes
            while (i < length)
            {
                if (_encryptCounter == 0)
                {
                    _implementation!.EncryptBlock(_encryptIV, 0, _encryptIV, 0);
                }

                var encrypted = (byte)(pInput[i] ^ pIV[_encryptCounter]);
                pOutput[i] = encrypted;
                pIV[_encryptCounter] = encrypted;
                _encryptCounter = (_encryptCounter + 1) & 0x07;
                i++;
            }
        }
    }

    public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (_implementation == null)
            throw new InvalidOperationException("Cryptographer not initialized");
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output buffers must be the same length");

        // Process in chunks when possible
        if (input.Length >= 64)
        {
            DecryptOptimized(input, output);
        }
        else
        {
            DecryptStandard(input, output);
        }
    }

    private void DecryptStandard(ReadOnlySpan<byte> input, Span<byte> output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (_decryptCounter == 0)
            {
                _implementation!.EncryptBlock(_decryptIV, 0, _decryptIV, 0);
            }

            var inputByte = input[i];
            var ivByte = _decryptIV[_decryptCounter];
            _decryptIV[_decryptCounter] = inputByte;
            output[i] = (byte)(ivByte ^ inputByte);

            _decryptCounter = (_decryptCounter + 1) & 0x07;
        }
    }

    private unsafe void DecryptOptimized(ReadOnlySpan<byte> input, Span<byte> output)
    {
        fixed (byte* pInput = input)
        fixed (byte* pOutput = output)
        fixed (byte* pIV = _decryptIV)
        {
            int length = input.Length;
            int i = 0;

            while (i < length)
            {
                if (_decryptCounter == 0)
                {
                    _implementation!.EncryptBlock(_decryptIV, 0, _decryptIV, 0);
                }

                var inputByte = pInput[i];
                var ivByte = pIV[_decryptCounter];
                pIV[_decryptCounter] = inputByte;
                pOutput[i] = (byte)(ivByte ^ inputByte);

                _decryptCounter = (_decryptCounter + 1) & 0x07;
                i++;
            }
        }
    }

    // SIMD version for even better performance (requires .NET 7+)
    public void EncryptSimd(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (!Sse2.IsSupported || input.Length < 16)
        {
            Encrypt(input, output);
            return;
        }

        if (_implementation == null)
            throw new InvalidOperationException("Cryptographer not initialized");
        if (input.Length != output.Length)
            throw new ArgumentException("Input and output buffers must be the same length");

        int i = 0;
        int length = input.Length;

        // Process 16 bytes at a time with SIMD
        while (i + 16 <= length && _encryptCounter == 0)
        {
            _implementation.EncryptBlock(_encryptIV, 0, _encryptIV, 0);
            _implementation.EncryptBlock(_encryptIV, 8, _encryptIV, 8);

            var inputVector = Vector128.Create(input.Slice(i, 16));
            var ivVector = Vector128.Create(_encryptIV);
            var encrypted = Sse2.Xor(inputVector, ivVector);

            encrypted.CopyTo(output.Slice(i, 16));
            encrypted.CopyTo(_encryptIV);

            i += 16;
        }

        // Handle remaining bytes
        while (i < length)
        {
            if (_encryptCounter == 0)
            {
                _implementation.EncryptBlock(_encryptIV, 0, _encryptIV, 0);
            }

            var encrypted = (byte)(input[i] ^ _encryptIV[_encryptCounter]);
            output[i] = encrypted;
            _encryptIV[_encryptCounter] = encrypted;
            _encryptCounter = (_encryptCounter + 1) & 0x07;
            i++;
        }
    }

    public void Dispose()
    {
        _implementation?.Dispose();
        _implementation = null;

        if (_encryptIV != null)
        {
            Array.Clear(_encryptIV);
            _encryptIV = null!;
        }

        if (_decryptIV != null)
        {
            Array.Clear(_decryptIV);
            _decryptIV = null!;
        }
    }
}


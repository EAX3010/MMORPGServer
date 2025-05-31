public sealed class ConquerPacket : IDisposable
{
    private const string CLIENT_SIGNATURE = "TQClient";
    private const string SERVER_SIGNATURE = "TQServer";
    private const int SIGNATURE_SIZE = 8;
    private const int HEADER_SIZE = 4;

    public readonly IMemoryOwner<byte>? _memoryOwner;
    private readonly Memory<byte> _buffer;
    private int _position;

    public ReadOnlySpan<byte> Data => _buffer.Span[.._position];
    public ushort Length => _position > 2 ? BitConverter.ToUInt16(_buffer.Span[..2]) : (ushort)0;
    public ushort Type => _position > 4 ? BitConverter.ToUInt16(_buffer.Span[2..4]) : (ushort)0;
    public bool IsComplete => _position >= HEADER_SIZE + SIGNATURE_SIZE && HasValidSignature();

    // Constructor for incoming packets from span
    public ConquerPacket(ReadOnlySpan<byte> data)
    {
        _memoryOwner = null;
        _buffer = data.ToArray();
        _position = data.Length;
    }

    // Constructor for incoming packets from array with offset and length
    public ConquerPacket(byte[] data, int offset, int length)
    {
        _memoryOwner = null;
        var packetData = new byte[length];
        Array.Copy(data, offset, packetData, 0, length);
        _buffer = packetData;
        _position = length;
    }

    // Constructor for incoming packets from array
    public ConquerPacket(byte[] data)
    {
        _memoryOwner = null;
        _buffer = (byte[])data.Clone();
        _position = data.Length;
    }

    // Constructor for outgoing packets
    public ConquerPacket(ushort type, bool isServer = true, int capacity = 1024)
    {
        _memoryOwner = MemoryPool<byte>.Shared.Rent(capacity);
        _buffer = _memoryOwner.Memory;
        _position = 0;

        // Clear the rented buffer to ensure no leftover data
        _buffer.Span.Clear();

        // Write placeholder length (will be updated in FinalizePacket)
        WriteUInt16(0);

        // Write packet type
        WriteUInt16(type);
    }

    public ConquerPacketWriter CreateWriter() => new(this);
    public ConquerPacketReader CreateReader() => new(this);

    public void SeekForward(int amount) => Seek(_position + amount);

    public void WriteUInt16(ushort value)
    {
        if (_position + 2 > _buffer.Length)
            throw new InvalidOperationException("Buffer overflow");

        BitConverter.TryWriteBytes(_buffer.Span[_position..], value);
        _position += 2;
    }

    public void WriteUInt32(uint value)
    {
        if (_position + 4 > _buffer.Length)
            throw new InvalidOperationException("Buffer overflow");

        BitConverter.TryWriteBytes(_buffer.Span[_position..], value);
        _position += 4;
    }

    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        if (_position + data.Length > _buffer.Length)
            throw new InvalidOperationException("Buffer overflow");

        data.CopyTo(_buffer.Span[_position..]);
        _position += data.Length;
    }

    public ushort ReadUInt16()
    {
        if (_position + 2 > _buffer.Length)
            throw new InvalidOperationException("Buffer underflow");

        var value = BitConverter.ToUInt16(_buffer.Span[_position..]);
        _position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        if (_position + 4 > _buffer.Length)
            throw new InvalidOperationException("Buffer underflow");

        var value = BitConverter.ToUInt32(_buffer.Span[_position..]);
        _position += 4;
        return value;
    }

    public void Seek(int position)
    {
        if (position < 0 || position > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _position = position;
    }

    public void FinalizePacket(bool isServer = true)
    {
        var signature = Encoding.ASCII.GetBytes(isServer ? SERVER_SIGNATURE : CLIENT_SIGNATURE);

        if (_position + SIGNATURE_SIZE > _buffer.Length)
            throw new InvalidOperationException("Buffer overflow during finalization");

        signature.CopyTo(_buffer.Span[_position..]);
        _position += SIGNATURE_SIZE;

    }

    internal void WriteSeal(bool isServer)
    {
        var signature = Encoding.ASCII.GetBytes(isServer ? SERVER_SIGNATURE : CLIENT_SIGNATURE);

        if (_position + SIGNATURE_SIZE > _buffer.Length)
            throw new InvalidOperationException("Buffer overflow during seal");

        signature.CopyTo(_buffer.Span[_position..]);
        _position += SIGNATURE_SIZE;

        // Update the length field like in FinalizePacket
    }

    public ReadOnlyMemory<byte> ToMemory()
    {
        return _buffer[.._position];
    }

    private bool HasValidSignature()
    {
        if (_position < SIGNATURE_SIZE)
            return false;

        var signatureSpan = _buffer.Span.Slice(_position - SIGNATURE_SIZE, SIGNATURE_SIZE);
        var signature = Encoding.ASCII.GetString(signatureSpan);

        return signature == CLIENT_SIGNATURE || signature == SERVER_SIGNATURE;
    }

    public bool TryExtractDHKey(out string dhKey)
    {
        dhKey = string.Empty;
        var originalPosition = _position;

        try
        {
            Seek(11);
            var offset = ReadUInt32() + 4 + 11;

            if (offset > 0 && offset < _buffer.Length)
            {
                Seek((int)offset);

                // Skip P and G parameters
                var pLength = ReadUInt32();
                if (pLength > 0 && _position + pLength < _buffer.Length)
                {
                    SeekForward((int)pLength);

                    var gLength = ReadUInt32();
                    if (gLength > 0 && _position + gLength < _buffer.Length)
                    {
                        SeekForward((int)gLength);

                        // Now read the actual DH key
                        var keySize = ReadUInt32();
                        if (keySize > 0 && _position + keySize <= _buffer.Length)
                        {
                            var keyBytes = _buffer.Span.Slice(_position, (int)keySize);
                            dhKey = Encoding.ASCII.GetString(keyBytes);
                            return !string.IsNullOrEmpty(dhKey);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        finally
        {
            _position = originalPosition;
        }

        return false;
    }

    // Helper method to get the actual data length (excluding signature)
    public int GetDataLength()
    {
        if (!IsComplete) return 0;
        return _position - SIGNATURE_SIZE;
    }

    // Helper method to check if this is a client or server packet
    public bool IsClientPacket()
    {
        if (!HasValidSignature()) return false;

        var signatureSpan = _buffer.Span.Slice(_position - SIGNATURE_SIZE, SIGNATURE_SIZE);
        var signature = Encoding.ASCII.GetString(signatureSpan);

        return signature == CLIENT_SIGNATURE;
    }

    public bool IsServerPacket()
    {
        if (!HasValidSignature()) return false;

        var signatureSpan = _buffer.Span.Slice(_position - SIGNATURE_SIZE, SIGNATURE_SIZE);
        var signature = Encoding.ASCII.GetString(signatureSpan);

        return signature == SERVER_SIGNATURE;
    }

    public void Dispose()
    {
        _memoryOwner?.Dispose();
    }
}
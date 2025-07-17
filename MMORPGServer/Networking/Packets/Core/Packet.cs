using MMORPGServer.Common.Enums;
using ProtoBuf;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace MMORPGServer.Networking.Packets.Core
{
    /// <summary>
    /// High-performance network packet with unified reading and writing operations.
    /// Optimized version with additional performance improvements.
    /// </summary>
    public sealed class Packet : IDisposable
    {
        private const string CLIENT_SIGNATURE = "TQClient";
        private const string SERVER_SIGNATURE = "TQServer";
        private const int SIGNATURE_SIZE = 8;
        private const int HEADER_SIZE = 4;

        // Pre-computed signature bytes to avoid repeated encoding
        private static readonly byte[] ClientSignatureBytes = Encoding.ASCII.GetBytes(CLIENT_SIGNATURE);
        private static readonly byte[] ServerSignatureBytes = Encoding.ASCII.GetBytes(SERVER_SIGNATURE);

        internal IMemoryOwner<byte>? _memoryOwner;
        internal Memory<byte> _buffer;
        private int _dataLength;
        private bool _disposed;

        // Cache for frequently used strings (optional - profile to see if beneficial)
        private static readonly ConcurrentDictionary<int, string> StringCache = new(Environment.ProcessorCount, 1024);

        public ReadOnlySpan<byte> Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Span[.._dataLength];
        }

        public short Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataLength >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span) : (short)0;
        }

        public GamePackets Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (GamePackets)(_dataLength >= 4 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span[2..]) : 0);
        }

        public int Position { get; set; }

        public bool IsComplete
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                short declaredLength = Length;
                if (declaredLength == 0) return false;

                int totalPacketSize = declaredLength + SIGNATURE_SIZE;
                if (totalPacketSize > _dataLength || declaredLength < HEADER_SIZE) return false;

                // Use pre-computed signature bytes
                var signatureSpan = _buffer.Span.Slice(totalPacketSize - SIGNATURE_SIZE, SIGNATURE_SIZE);
                return signatureSpan.SequenceEqual(ClientSignatureBytes) ||
                       signatureSpan.SequenceEqual(ServerSignatureBytes);
            }
        }

        // Constructor for zero-copy scenarios when we own the buffer
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(Memory<byte> ownedBuffer, int dataLength)
        {
            _memoryOwner = null;
            _buffer = ownedBuffer;
            _dataLength = dataLength;
            Position = HEADER_SIZE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(ReadOnlySpan<byte> data)
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(data.Length);
            _buffer = _memoryOwner.Memory.Slice(0, data.Length);
            data.CopyTo(_buffer.Span);
            _dataLength = data.Length;
            Position = HEADER_SIZE;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(short type, int capacity = 1024)
        {
            int actualCapacity = Math.Max(capacity, HEADER_SIZE + SIGNATURE_SIZE);
            _memoryOwner = MemoryPool<byte>.Shared.Rent(actualCapacity);
            _buffer = _memoryOwner.Memory;
            _buffer.Span.Clear();
            Position = 0;
            WriteUInt16(0);
            WriteUInt16((ushort)type);
            _dataLength = HEADER_SIZE;
            Position = HEADER_SIZE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(GamePackets type, int capacity = 1024)
            : this((short)type, capacity)
        {
        }

        // Batch read operations for better performance
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadMultiple(out ushort value1, out uint value2)
        {
            EnsureCanRead(6); // 2 + 4 bytes
            var span = _buffer.Span[Position..];
            value1 = BinaryPrimitives.ReadUInt16LittleEndian(span);
            value2 = BinaryPrimitives.ReadUInt32LittleEndian(span[2..]);
            Position += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            EnsureCanRead(2);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span[Position..]);
            Position += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            EnsureCanRead(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            EnsureCanRead(4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            EnsureCanRead(8);
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            EnsureCanRead(1);
            return _buffer.Span[Position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> destination)
        {
            EnsureCanRead(destination.Length);
            _buffer.Span.Slice(Position, destination.Length).CopyTo(destination);
            Position += destination.Length;
        }

        public byte[] ReadBytes(int count)
        {
            EnsureCanRead(count);
            byte[] result = _buffer.Span.Slice(Position, count).ToArray();
            Position += count;
            return result;
        }

        // Optimized string reading with optional caching
        public string ReadString(int length, bool useCache = false)
        {
            EnsureCanRead(length);
            var stringSpan = _buffer.Span.Slice(Position, length);
            Position += length;

            int nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex];

            if (useCache && stringSpan.Length <= 32) // Only cache small strings
            {
                int hash = GetSpanHashCode(stringSpan);
                if (StringCache.TryGetValue(hash, out var cached))
                {
                    var cachedBytes = Encoding.UTF8.GetBytes(cached);
                    if (stringSpan.SequenceEqual(cachedBytes))
                        return cached;
                }

                var str = Encoding.UTF8.GetString(stringSpan);
                StringCache.TryAdd(hash, str);
                return str;
            }

            return Encoding.UTF8.GetString(stringSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSpanHashCode(ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                foreach (byte b in span)
                    hash = hash * 31 + b;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            EnsureCanRead(4);
            float value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            EnsureCanRead(8);
            double value = BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        // Write methods remain similar but with dynamic buffer growth
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            EnsureCanWrite(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Span[Position..], value);
            Position += 2;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureCanWrite(1);
            _buffer.Span[Position++] = value;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureCanWrite(data.Length);
            data.CopyTo(_buffer.Span[Position..]);
            Position += data.Length;
            UpdateDataLength();
        }

        public void WriteString(string value, int maxLength)
        {
            EnsureCanWrite(maxLength);
            var destSpan = _buffer.Span.Slice(Position, maxLength);
            int bytesWritten = Encoding.UTF8.GetBytes(value.AsSpan(), destSpan);

            if (bytesWritten < maxLength)
                destSpan[bytesWritten..].Clear();

            Position += maxLength;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteDoubleLittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Seek(int position)
        {
            if (position < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(position));
            Position = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SeekToPayload(int payloadOffset)
        {
            if (payloadOffset < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(payloadOffset));
            Position = HEADER_SIZE + payloadOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int amount)
        {
            if (amount < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(amount));
            Position += amount;
        }

        public void FinalizePacket(GamePackets type)
        {
            WriteSeal();
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE));
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], (short)type);
        }

        public void FinalizePacket(short type)
        {
            WriteSeal();
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE));
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], type);
        }

        public void WriteSeal()
        {
            EnsureCanWrite(ServerSignatureBytes.Length);
            ServerSignatureBytes.CopyTo(_buffer.Span[Position..]);
            Position += ServerSignatureBytes.Length;
            UpdateDataLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _buffer.Span.Clear();
            Position = HEADER_SIZE;
            _dataLength = HEADER_SIZE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetFinalizedMemory() => _buffer[.._dataLength];

        public bool TryExtractDHKey(out string dhKey)
        {
            dhKey = string.Empty;
            int originalPosition = Position;

            try
            {
                Seek(11);
                int offset = ReadInt32() + 4 + 11;

                if (offset > 0 && offset < _dataLength)
                {
                    Seek(offset);
                    int keySize = ReadInt32();

                    if (keySize > 0 && keySize < _dataLength - offset)
                    {
                        dhKey = ReadString(keySize);
                        return !string.IsNullOrEmpty(dhKey);
                    }
                }
            }
            catch { }
            finally { Position = originalPosition; }

            return false;
        }

        public int RemainingBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int endOfData = _dataLength;
                if (IsComplete) endOfData -= SIGNATURE_SIZE;
                return Math.Max(0, endOfData - Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsClientPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(ClientSignatureBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsServerPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(ServerSignatureBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanRead(int bytes)
        {
            if (Position + bytes > _dataLength)
                ThrowHelper.ThrowInvalidOperation($"Cannot read {bytes} bytes. Position: {Position}, Available: {_dataLength - Position}");
        }

        // Enhanced write check with automatic buffer growth
        private void EnsureCanWrite(int bytes)
        {
            if (Position + bytes > _buffer.Length)
            {
                if (_memoryOwner != null)
                {
                    // Grow the buffer
                    int newSize = Math.Max(_buffer.Length * 2, Position + bytes);
                    var newOwner = MemoryPool<byte>.Shared.Rent(newSize);
                    var newBuffer = newOwner.Memory;

                    _buffer.Span.CopyTo(newBuffer.Span);
                    _memoryOwner.Dispose();

                    _memoryOwner = newOwner;
                    _buffer = newBuffer;
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperation($"Cannot write {bytes} bytes. Buffer overflow. Position: {Position}, Capacity: {_buffer.Length}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDataLength()
        {
            if (Position > _dataLength)
                _dataLength = Position;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryOwner?.Dispose();
                _disposed = true;
            }
        }

        // Optimized Protobuf methods using ArrayBufferWriter
        public T DeserializeProto<T>()
        {
            int originalPosition = Position;
            try
            {
                int dataLength = Length - 4;
                if (dataLength <= 0)
                    ThrowHelper.ThrowInvalidOperation("No data available to deserialize");

                Seek(4);
                var data = _buffer.Slice(Position, dataLength);

                // Use ReadOnlySequence for zero-copy deserialization
                var sequence = new ReadOnlySequence<byte>(data);
                return Serializer.Deserialize<T>(sequence);
            }
            finally { Position = originalPosition; }
        }

        public void SerializeProto<T>(T message)
        {
            Seek(4);

            // Use ArrayBufferWriter for efficient serialization
            var writer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(writer, message);

            var data = writer.WrittenSpan;
            WriteBytes(data);
        }
    }

    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRange(string paramName)
            => throw new ArgumentOutOfRangeException(paramName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperation(string message)
            => throw new InvalidOperationException(message);
    }
}
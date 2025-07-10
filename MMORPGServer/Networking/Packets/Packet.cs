using MMORPGServer.Common.Enums;
using ProtoBuf;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MMORPGServer.Networking.Packets
{
    /// <summary>
    /// High-performance network packet with unified reading and writing operations.
    /// Uses a single position tracker since packets are either read from OR written to.
    /// This class is internal and should only be used through FluentPacketReader and FluentPacketWriter.
    /// </summary>
    public sealed class Packet : IDisposable
    {
        private const string CLIENT_SIGNATURE = "TQClient";
        private const string SERVER_SIGNATURE = "TQServer";
        private const int SIGNATURE_SIZE = 8;
        private const int HEADER_SIZE = 4;

        internal readonly IMemoryOwner<byte>? _memoryOwner;
        internal Memory<byte> _buffer;
        private int _dataLength;
        private bool _disposed;

        /// <summary>
        /// Gets a ReadOnlySpan of the packet's valid data.
        /// </summary>
        public ReadOnlySpan<byte> Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Span[.._dataLength];
        }

        /// <summary>
        /// Gets the total length of the packet as declared in its header (first 2 bytes).
        /// </summary>
        public short Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataLength >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span) : (short)0;
        }

        /// <summary>
        /// Gets the type of the packet as declared in its header (bytes 2-3).
        /// </summary>
        public GamePackets Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (GamePackets)(_dataLength >= 4 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span[2..]) : 0);
        }

        /// <summary>
        /// Current position in the buffer (for reading or writing operations).
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Checks if the packet appears to be complete with a valid signature.
        /// </summary>
        public bool IsComplete
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                short declaredLength = Length;
                if (declaredLength == 0) return false;

                int totalPacketSize = declaredLength + SIGNATURE_SIZE;
                if (totalPacketSize > _dataLength || declaredLength < HEADER_SIZE) return false;

                // Use SequenceEqual for faster comparison
                var signatureSpan = _buffer.Span.Slice(totalPacketSize - SIGNATURE_SIZE, SIGNATURE_SIZE);
                return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(CLIENT_SIGNATURE)) ||
                       signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(SERVER_SIGNATURE));
            }
        }

        /// <summary>
        /// Constructor for incoming packets (data already exists).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(ReadOnlySpan<byte> data)
        {
            _memoryOwner = null;
            _buffer = data.ToArray();
            _dataLength = data.Length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for empty packet.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet()
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(1024);
            _buffer = _memoryOwner.Memory;
            _buffer.Span.Clear();
            _dataLength = HEADER_SIZE;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for incoming packets from an array segment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(byte[] data, int offset, int length)
        {
            _memoryOwner = null;
            _buffer = new Memory<byte>(data, offset, length);
            _dataLength = length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for incoming packets from a full array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(byte[] data)
        {
            _memoryOwner = null;
            _buffer = data;
            _dataLength = data.Length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for outgoing packets with type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(short type, bool isServerPacket = true, int capacity = 1024)
        {
            int actualCapacity = Math.Max(capacity, HEADER_SIZE + SIGNATURE_SIZE);
            _memoryOwner = MemoryPool<byte>.Shared.Rent(actualCapacity);
            _buffer = _memoryOwner.Memory;

            ref byte spanRef = ref MemoryMarshal.GetReference(_buffer.Span);
            BinaryPrimitives.WriteInt16LittleEndian(MemoryMarshal.CreateSpan(ref spanRef, 2), 0);
            BinaryPrimitives.WriteInt16LittleEndian(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref spanRef, 2), 2), type);

            _dataLength = HEADER_SIZE;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for outgoing packets with GamePackets enum.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(GamePackets type, bool isServerPacket = true, int capacity = 1024)
            : this((short)type, isServerPacket, capacity)
        {
        }

        // --- Read Methods (Optimized with BinaryPrimitives) ---

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

        public string ReadString(int length)
        {
            EnsureCanRead(length);
            var stringSpan = _buffer.Span.Slice(Position, length);
            Position += length;

            int nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex];

            return Encoding.UTF8.GetString(stringSpan);
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

        // --- Write Methods (Optimized with BinaryPrimitives) ---

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
            {
                destSpan[bytesWritten..].Clear();
            }

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

        // --- Position Management ---

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

        // --- Finalization ---

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
            var signatureBytes = Encoding.ASCII.GetBytes(SERVER_SIGNATURE);
            EnsureCanWrite(signatureBytes.Length);
            signatureBytes.CopyTo(_buffer.Span[Position..]);
            Position += signatureBytes.Length;
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

        // --- Special Methods ---

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
            return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(CLIENT_SIGNATURE));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsServerPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(SERVER_SIGNATURE));
        }

        // --- Helper Methods ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanRead(int bytes)
        {
            if (Position + bytes > _dataLength)
                ThrowHelper.ThrowInvalidOperation($"Cannot read {bytes} bytes. Position: {Position}, Available: {_dataLength - Position}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanWrite(int bytes)
        {
            if (Position + bytes > _buffer.Length)
                ThrowHelper.ThrowInvalidOperation($"Cannot write {bytes} bytes. Buffer overflow. Position: {Position}, Capacity: {_buffer.Length}");
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

        // --- Protobuf Methods ---

        public T DeserializeProto<T>()
        {
            int originalPosition = Position;
            try
            {
                int dataLength = Length - 4;
                if (dataLength <= 0)
                    ThrowHelper.ThrowInvalidOperation("No data available to deserialize");

                Seek(4);
                var data = _buffer.Span.Slice(Position, dataLength);

                using var ms = new MemoryStream(dataLength);
                ms.Write(data);
                ms.Position = 0;
                return Serializer.Deserialize<T>(ms);
            }
            finally { Position = originalPosition; }
        }

        public void SerializeProto<T>(T message)
        {
            int originalPosition = Position;
            try
            {
                Seek(4);
                using var ms = new MemoryStream();
                Serializer.Serialize(ms, message);
                var data = ms.GetBuffer().AsSpan(0, (int)ms.Length);
                WriteBytes(data);
            }
            finally { Position = originalPosition; }
        }
    }

    // Helper class for throwing exceptions without inlining
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


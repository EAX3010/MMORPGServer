using MMORPGServer.Common.Enums;
using ProtoBuf;
using System.Buffers;
using System.Buffers.Binary;
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
        private const int HEADER_SIZE = 4; // Length (2 bytes) + Type (2 bytes)

        // Pre-computed signature bytes to avoid repeated encoding
        private static readonly byte[] ClientSignatureBytes = Encoding.ASCII.GetBytes(CLIENT_SIGNATURE);
        private static readonly byte[] ServerSignatureBytes = Encoding.ASCII.GetBytes(SERVER_SIGNATURE);

        internal IMemoryOwner<byte>? _memoryOwner;
        internal Memory<byte> _buffer;
        private int _dataLength;
        private bool _disposed;

        // StringCache is removed as per user's previous request to delete fluent.
        // If caching is desired directly in Packet, it would need to be re-evaluated.

        /// <summary>
        /// Gets the raw data span of the packet.
        /// </summary>
        public ReadOnlySpan<byte> Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Span[.._dataLength];
        }

        /// <summary>
        /// Gets the declared length of the packet payload (excluding signature).
        /// </summary>
        public short Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataLength >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span) : (short)0;
        }

        /// <summary>
        /// Gets the type of the game packet.
        /// </summary>
        public GamePackets Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (GamePackets)(_dataLength >= 4 ? BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span[2..]) : 0);
        }

        /// <summary>
        /// Gets or sets the current read/write position within the packet buffer.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Checks if the packet is complete (i.e., contains a valid length and signature).
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

                // Use pre-computed signature bytes for comparison
                var signatureSpan = _buffer.Span.Slice(totalPacketSize - SIGNATURE_SIZE, SIGNATURE_SIZE);
                return signatureSpan.SequenceEqual(ClientSignatureBytes) ||
                       signatureSpan.SequenceEqual(ServerSignatureBytes);
            }
        }

        /// <summary>
        /// Gets the number of bytes remaining to be read in the packet payload.
        /// </summary>
        public int RemainingBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int endOfData = _dataLength;
                if (IsComplete) endOfData -= SIGNATURE_SIZE; // Exclude signature from remaining bytes
                return Math.Max(0, endOfData - Position);
            }
        }

        /// <summary>
        /// Checks if the packet was sent by a client (based on signature).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsClientPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(ClientSignatureBytes);
        }

        /// <summary>
        /// Checks if the packet was sent by the server (based on signature).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsServerPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(ServerSignatureBytes);
        }

        /// <summary>
        /// Initializes a new instance of the Packet class with an owned buffer.
        /// Used for zero-copy scenarios where the buffer is managed externally.
        /// </summary>
        /// <param name="ownedBuffer">The memory buffer owned by the packet.</param>
        /// <param name="dataLength">The actual length of the data in the buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(Memory<byte> ownedBuffer, int dataLength)
        {
            _memoryOwner = null; // We don't own the IMemoryOwner, just the Memory<byte> slice
            _buffer = ownedBuffer;
            _dataLength = dataLength;
            Position = HEADER_SIZE; // Start reading after length and type
        }

        /// <summary>
        /// Initializes a new instance of the Packet class by copying data from a ReadOnlySpan.
        /// A new buffer is rented from the memory pool.
        /// </summary>
        /// <param name="data">The data to copy into the packet.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(ReadOnlySpan<byte> data)
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(data.Length);
            _buffer = _memoryOwner.Memory.Slice(0, data.Length);
            data.CopyTo(_buffer.Span);
            _dataLength = data.Length;
            Position = HEADER_SIZE; // Start reading after length and type
        }

        /// <summary>
        /// Initializes a new instance of the Packet class for writing, with a specified type and capacity.
        /// A new buffer is rented from the memory pool.
        /// </summary>
        /// <param name="type">The type of the packet.</param>
        /// <param name="capacity">The initial capacity of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(short type, int capacity = 1024)
        {
            int actualCapacity = Math.Max(capacity, HEADER_SIZE + SIGNATURE_SIZE);
            _memoryOwner = MemoryPool<byte>.Shared.Rent(actualCapacity);
            _buffer = _memoryOwner.Memory;
            _buffer.Span.Clear(); // Clear the buffer to ensure no stale data
            Position = 0;
            WriteUInt16(0); // Placeholder for length
            WriteUInt16((ushort)type); // Write packet type
            _dataLength = HEADER_SIZE; // Initial data length is just the header
            Position = HEADER_SIZE; // Set position to start writing payload
        }

        /// <summary>
        /// Initializes a new instance of the Packet class for writing, with a specified GamePackets enum type and capacity.
        /// </summary>
        /// <param name="type">The GamePackets enum type of the packet.</param>
        /// <param name="capacity">The initial capacity of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(GamePackets type, int capacity = 1024)
            : this((short)type, capacity)
        {
        }

        // --- Read Methods ---

        /// <summary>
        /// Reads multiple unsigned 16-bit and 32-bit integers from the packet.
        /// </summary>
        /// <param name="value1">The first unsigned 16-bit integer.</param>
        /// <param name="value2">The second unsigned 32-bit integer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadMultiple(out ushort value1, out uint value2)
        {
            EnsureCanRead(6); // 2 (ushort) + 4 (uint) bytes
            Span<byte> span = _buffer.Span[Position..];
            value1 = BinaryPrimitives.ReadUInt16LittleEndian(span);
            value2 = BinaryPrimitives.ReadUInt32LittleEndian(span[2..]);
            Position += 6;
        }

        /// <summary>
        /// Reads multiple unsigned 16-bit and 32-bit integers from the packet at a specified position.
        /// </summary>
        /// <param name="value1">The first unsigned 16-bit integer.</param>
        /// <param name="value2">The second unsigned 32-bit integer.</param>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        public void ReadMultiple(out ushort value1, out uint value2, int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            ReadMultiple(out value1, out value2);
        }

        /// <summary>
        /// Reads an unsigned 8-bit integer (byte) from the packet.
        /// </summary>
        /// <returns>The byte value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            EnsureCanRead(1);
            return _buffer.Span[Position++];
        }

        /// <summary>
        /// Reads an unsigned 8-bit integer (byte) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The byte value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadByte(); // Call the original ReadByte
        }

        /// <summary>
        /// Reads a signed 8-bit integer (sbyte) from the packet.
        /// </summary>
        /// <returns>The sbyte value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            EnsureCanRead(1);
            return (sbyte)_buffer.Span[Position++];
        }

        /// <summary>
        /// Reads a signed 8-bit integer (sbyte) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The sbyte value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadSByte(); // Call the original ReadSByte
        }

        /// <summary>
        /// Reads an unsigned 16-bit integer (ushort) from the packet.
        /// </summary>
        /// <returns>The ushort value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            EnsureCanRead(2);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span[Position..]);
            Position += 2;
            return value;
        }

        /// <summary>
        /// Reads an unsigned 16-bit integer (ushort) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The ushort value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadUInt16(); // Call the original ReadUInt16
        }

        /// <summary>
        /// Reads a signed 16-bit integer (short) from the packet.
        /// </summary>
        /// <returns>The short value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
        {
            EnsureCanRead(2);
            short value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Span[Position..]);
            Position += 2;
            return value;
        }

        /// <summary>
        /// Reads a signed 16-bit integer (short) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The short value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadInt16(); // Call the original ReadInt16
        }

        /// <summary>
        /// Reads an unsigned 32-bit integer (uint) from the packet.
        /// </summary>
        /// <returns>The uint value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            EnsureCanRead(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads an unsigned 32-bit integer (uint) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The uint value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadUInt32(); // Call the original ReadUInt32
        }

        /// <summary>
        /// Reads a signed 32-bit integer (int) from the packet.
        /// </summary>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            EnsureCanRead(4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads a signed 32-bit integer (int) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The int value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadInt32(); // Call the original ReadInt32
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer (ulong) from the packet.
        /// </summary>
        /// <returns>The ulong value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            EnsureCanRead(8);
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer (ulong) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The ulong value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadUInt64(); // Call the original ReadUInt64
        }

        /// <summary>
        /// Reads a signed 64-bit integer (long) from the packet.
        /// </summary>
        /// <returns>The long value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            EnsureCanRead(8);
            long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        /// <summary>
        /// Reads a signed 64-bit integer (long) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The long value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadInt64(); // Call the original ReadInt64
        }

        /// <summary>
        /// Reads a single-precision floating-point number (float) from the packet.
        /// </summary>
        /// <returns>The float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            EnsureCanRead(4);
            float value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads a single-precision floating-point number (float) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadFloat(); // Call the original ReadFloat
        }

        /// <summary>
        /// Reads a double-precision floating-point number (double) from the packet.
        /// </summary>
        /// <returns>The double value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            EnsureCanRead(8);
            double value = BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        /// <summary>
        /// Reads a double-precision floating-point number (double) from the packet at a specified position.
        /// </summary>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The double value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble(int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadDouble(); // Call the original ReadDouble
        }

        /// <summary>
        /// Reads a specified number of bytes into a destination Span.
        /// </summary>
        /// <param name="destination">The span to write the bytes into.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> destination)
        {
            EnsureCanRead(destination.Length);
            _buffer.Span.Slice(Position, destination.Length).CopyTo(destination);
            Position += destination.Length;
        }

        /// <summary>
        /// Reads a specified number of bytes into a destination Span at a specified position.
        /// </summary>
        /// <param name="destination">The span to write the bytes into.</param>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> destination, int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            ReadBytes(destination); // Call the original ReadBytes
        }

        /// <summary>
        /// Reads a specified number of bytes into a new byte array.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A new byte array containing the read bytes.</returns>
        public byte[] ReadBytes(int count)
        {
            EnsureCanRead(count);
            byte[] result = _buffer.Span.Slice(Position, count).ToArray();
            Position += count;
            return result;
        }

        /// <summary>
        /// Reads a specified number of bytes into a new byte array at a specified position.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>A new byte array containing the read bytes.</returns>
        public byte[] ReadBytes(int count, int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadBytes(count); // Call the original ReadBytes
        }

        /// <summary>
        /// Reads a string of a specified length.
        /// </summary>
        /// <param name="length">The maximum length of the string to read (including null terminator).</param>
        /// <returns>The decoded string.</returns>
        public string ReadString(int length)
        {
            EnsureCanRead(length);
            var stringSpan = _buffer.Span.Slice(Position, length);
            Position += length;

            int nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex]; // Trim at first null terminator

            return Encoding.UTF8.GetString(stringSpan);
        }

        /// <summary>
        /// Reads a string of a specified length at a specified position.
        /// </summary>
        /// <param name="length">The maximum length of the string to read (including null terminator).</param>
        /// <param name="pos">The position to read from. If 0, reads from current position.</param>
        /// <returns>The decoded string.</returns>
        public string ReadString(int length, int pos = 0)
        {
            if (pos != 0)
                Seek(pos);
            return ReadString(length); // Call the original ReadString
        }

        // --- Write Methods (Fluent Style) ---

        /// <summary>
        /// Writes an unsigned 8-bit integer (byte) to the packet.
        /// </summary>
        /// <param name="value">The byte value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteByte(byte value)
        {
            EnsureCanWrite(1);
            _buffer.Span[Position++] = value;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a signed 8-bit integer (sbyte) to the packet.
        /// </summary>
        /// <param name="value">The sbyte value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteSByte(sbyte value)
        {
            EnsureCanWrite(1);
            _buffer.Span[Position++] = (byte)value;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer (ushort) to the packet.
        /// </summary>
        /// <param name="value">The ushort value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteUInt16(ushort value)
        {
            EnsureCanWrite(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Span[Position..], value);
            Position += 2;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a signed 16-bit integer (short) to the packet.
        /// </summary>
        /// <param name="value">The short value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteInt16(short value)
        {
            EnsureCanWrite(2);
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[Position..], value);
            Position += 2;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes an unsigned 32-bit integer (uint) to the packet.
        /// </summary>
        /// <param name="value">The uint value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteUInt32(uint value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a signed 32-bit integer (int) to the packet.
        /// </summary>
        /// <param name="value">The int value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteInt32(int value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes an unsigned 64-bit integer (ulong) to the packet.
        /// </summary>
        /// <param name="value">The ulong value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteUInt64(ulong value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a signed 64-bit integer (long) to the packet.
        /// </summary>
        /// <param name="value">The long value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteInt64(long value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a single-precision floating-point number (float) to the packet.
        /// </summary>
        /// <param name="value">The float value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteFloat(float value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a double-precision floating-point number (double) to the packet.
        /// </summary>
        /// <param name="value">The double value to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteDouble(double value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteDoubleLittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a ReadOnlySpan of bytes to the packet.
        /// </summary>
        /// <param name="data">The byte data to write.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureCanWrite(data.Length);
            data.CopyTo(_buffer.Span[Position..]);
            Position += data.Length;
            UpdateDataLength();
            return this;
        }

        /// <summary>
        /// Writes a string to the packet, padding with nulls up to maxLength.
        /// </summary>
        /// <param name="value">The string to write.</param>
        /// <param name="maxLength">The fixed length of the string field in the packet.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        public Packet WriteString(string value, int maxLength)
        {
            EnsureCanWrite(maxLength);
            var destSpan = _buffer.Span.Slice(Position, maxLength);
            int bytesWritten = Encoding.UTF8.GetBytes(value.AsSpan(), destSpan);

            if (bytesWritten < maxLength)
                destSpan[bytesWritten..].Clear(); // Pad with nulls if string is shorter

            Position += maxLength;
            UpdateDataLength();
            return this;
        }

        // --- Position Management (Fluent Style) ---

        /// <summary>
        /// Sets the current read/write position within the packet.
        /// </summary>
        /// <param name="position">The new position.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet Seek(int position)
        {
            if (position < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(position));
            Position = position;
            return this;
        }

        /// <summary>
        /// Sets the current read/write position to a specific offset within the payload (after header).
        /// </summary>
        /// <param name="payloadOffset">The offset from the start of the payload.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet SeekToPayload(int payloadOffset)
        {
            if (payloadOffset < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(payloadOffset));
            Position = HEADER_SIZE + payloadOffset;
            return this;
        }

        /// <summary>
        /// Advances the current read/write position by a specified amount.
        /// </summary>
        /// <param name="amount">The number of bytes to skip.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet Skip(int amount)
        {
            if (amount < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(amount));
            Position += amount;
            UpdateDataLength(); // Update data length if skipping past current dataLength
            return this;
        }

        // --- Packet Finalization ---

        /// <summary>
        /// Finalizes the packet by writing the server signature and updating the length and type in the header.
        /// </summary>
        /// <param name="type">The GamePackets enum type of the packet.</param>
        public ReadOnlyMemory<byte> Build(GamePackets type)
        {
            WriteSeal(); // Write the server signature
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE)); // Write actual payload length
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], (short)type); // Write packet type
            return GetFinalizedMemory();
        }
        /// <summary>
        /// Finalizes the packet by writing the server signature and updating the length and type in the header.
        /// Uses the packet's internal Type property.
        /// </summary>
        public ReadOnlyMemory<byte> Build()
        {
            WriteSeal(); // Write the server signature
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE)); // Write actual payload length
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], (short)this.Type); // Write packet type
            return GetFinalizedMemory();
        }

        /// <summary>
        /// Finalizes the packet by writing the server signature and updating the length and type in the header.
        /// </summary>
        /// <param name="type">The short integer type of the packet.</param>
        public ReadOnlyMemory<byte> Build(short type)
        {
            WriteSeal(); // Write the server signature
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE)); // Write actual payload length
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], type); // Write packet type
            return GetFinalizedMemory();
        }

        /// <summary>
        /// Writes the server signature (seal) at the current position.
        /// </summary>
        public void WriteSeal()
        {
            EnsureCanWrite(ServerSignatureBytes.Length);
            ServerSignatureBytes.CopyTo(_buffer.Span[Position..]);
            Position += ServerSignatureBytes.Length;
            UpdateDataLength();
        }

        /// <summary>
        /// Resets the packet's position and data length, effectively clearing it for reuse.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _buffer.Span.Clear(); // Clear the underlying buffer
            Position = HEADER_SIZE; // Reset position to start of payload
            _dataLength = HEADER_SIZE; // Reset data length to just the header
        }

        /// <summary>
        /// Gets a ReadOnlyMemory slice of the finalized packet data.
        /// </summary>
        /// <returns>A ReadOnlyMemory representing the complete packet.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetFinalizedMemory()
        {
            return _buffer[.._dataLength];
        }

        // --- Protocol-Specific Methods ---

        /// <summary>
        /// Attempts to extract the Diffie-Hellman public key from the packet.
        /// </summary>
        /// <param name="dhKey">The extracted DH key as a string.</param>
        /// <returns>True if the key was successfully extracted, false otherwise.</returns>
        public bool TryExtractDHKey(out string dhKey)
        {
            dhKey = string.Empty;
            int originalPosition = Position; // Save original position

            try
            {
                Seek(11); // Move to specific offset for DH key info
                int offset = ReadInt32() + 4 + 11; // Calculate actual key offset

                if (offset > 0 && offset < _dataLength)
                {
                    Seek(offset); // Move to key data
                    int keySize = ReadInt32(); // Read key size

                    if (keySize > 0 && keySize < _dataLength - offset)
                    {
                        dhKey = ReadString(keySize); // Read the key string
                        return !string.IsNullOrEmpty(dhKey);
                    }
                }
            }
            catch { /* Ignore exceptions during extraction, return false */ }
            finally { Position = originalPosition; } // Restore original position

            return false;
        }

        /// <summary>
        /// Deserializes a Protobuf message from the current packet payload.
        /// </summary>
        /// <typeparam name="T">The type of the Protobuf message.</typeparam>
        /// <returns>The deserialized message.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no data is available for deserialization.</exception>
        public T DeserializeProto<T>()
        {

            // Length in header is payload length, so total packet length is Length + SIGNATURE_SIZE.
            // The Protobuf data starts after the 4-byte header (Length + Type).
            int protoDataLength = Length - HEADER_SIZE; // Calculate remaining payload length from current position

            if (protoDataLength <= 0)
                ThrowHelper.ThrowInvalidOperation("No data available to deserialize Protobuf message.");

            // Create a ReadOnlySequence from the current position to the end of the protobuf data
            var dataSlice = _buffer.Slice(Position, protoDataLength);
            var sequence = new ReadOnlySequence<byte>(dataSlice);

            // Deserialize using ProtoBuf.Net
            T deserializedMessage = Serializer.Deserialize<T>(sequence);
            Position += protoDataLength; // Advance position after reading
            return deserializedMessage;

        }

        /// <summary>
        /// Serializes a Protobuf message into the packet at the current position.
        /// </summary>
        /// <typeparam name="T">The type of the Protobuf message.</typeparam>
        /// <param name="message">The message to serialize.</param>
        /// <returns>The current Packet instance for fluent chaining.</returns>
        public Packet SerializeProto<T>(T message)
        {
            Seek(4); // Seek to payload start (after header)

            // Use ArrayBufferWriter for efficient serialization
            var writer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(writer, message);

            var data = writer.WrittenSpan;
            WriteBytes(data); // Write the serialized bytes to the packet
            return this;
        }

        // --- Internal Helper Methods ---

        /// <summary>
        /// Ensures there are enough bytes to read at the current position.
        /// </summary>
        /// <param name="bytes">The number of bytes to read.</param>
        /// <exception cref="InvalidOperationException">Thrown if there are not enough bytes available.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanRead(int bytes)
        {
            if (Position + bytes > _dataLength)
                ThrowHelper.ThrowInvalidOperation($"Cannot read {bytes} bytes. Position: {Position}, Available: {_dataLength - Position}");
        }

        /// <summary>
        /// Ensures there is enough capacity to write the specified number of bytes,
        /// automatically growing the buffer if necessary.
        /// </summary>
        /// <param name="bytes">The number of bytes to write.</param>
        /// <exception cref="InvalidOperationException">Thrown if the buffer cannot be grown (e.g., if not owned by MemoryPool).</exception>
        private void EnsureCanWrite(int bytes)
        {
            if (Position + bytes > _buffer.Length)
            {
                if (_memoryOwner != null)
                {
                    // Grow the buffer by doubling its size or to fit the new data, whichever is larger
                    int newSize = Math.Max(_buffer.Length * 2, Position + bytes);
                    var newOwner = MemoryPool<byte>.Shared.Rent(newSize);
                    var newBuffer = newOwner.Memory;

                    // Copy existing data to the new buffer
                    _buffer.Span.CopyTo(newBuffer.Span);
                    _memoryOwner.Dispose(); // Dispose the old memory owner

                    _memoryOwner = newOwner;
                    _buffer = newBuffer;
                }
                else
                {
                    // If the buffer is not owned by MemoryPool, we cannot grow it
                    ThrowHelper.ThrowInvalidOperation($"Cannot write {bytes} bytes. Buffer overflow. Position: {Position}, Capacity: {_buffer.Length}");
                }
            }
        }

        /// <summary>
        /// Updates the packet's total data length if the current position has extended beyond it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDataLength()
        {
            if (Position > _dataLength)
                _dataLength = Position;
        }

        // --- IDisposable Implementation ---

        /// <summary>
        /// Disposes the packet, returning its rented memory to the pool.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryOwner?.Dispose(); // Return the rented memory to the pool
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Helper class for throwing common exceptions.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRange(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperation(string message)
        {
            throw new InvalidOperationException(message);
        }
    }
}
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

        // IMemoryOwner allows renting memory from a pool and ensures it's returned.
        // This helps reduce GC pressure by reusing large byte arrays.
        internal readonly IMemoryOwner<byte>? _memoryOwner;
        internal Memory<byte> _buffer; // The underlying memory buffer for the packet
        private int _dataLength;       // The actual length of valid data in the buffer
        private bool _disposed;        // Flag to track if the object has been disposed

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

                // Total packet size includes the declared length + signature
                int totalPacketSize = declaredLength + SIGNATURE_SIZE;
                // Check if the total size is within the received data length and header is present
                if (totalPacketSize > _dataLength || declaredLength < HEADER_SIZE) return false;

                // Verify the signature bytes at the end of the packet
                var signatureSpan = _buffer.Span.Slice(totalPacketSize - SIGNATURE_SIZE, SIGNATURE_SIZE);
                return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(CLIENT_SIGNATURE)) ||
                       signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(SERVER_SIGNATURE));
            }
        }

        /// <summary>
        /// Constructor for incoming packets where data is provided as a ReadOnlySpan.
        /// Rents a buffer from ArrayPool to own the data, reducing heap allocations.
        /// </summary>
        /// <param name="data">The incoming packet data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(ReadOnlySpan<byte> data)
        {
            // Rent a buffer from the shared ArrayPool to hold the incoming data.
            // This avoids a new allocation for every incoming packet.
            _memoryOwner = MemoryPool<byte>.Shared.Rent(data.Length);
            _buffer = _memoryOwner.Memory.Slice(0, data.Length); // Slice to the exact data length
            data.CopyTo(_buffer.Span); // Copy the incoming data into the rented buffer

            _dataLength = data.Length;
            Position = HEADER_SIZE; // Start reading after the header
        }

        /// <summary>
        /// Constructor for creating an empty packet, typically for writing.
        /// Rents a default-sized buffer from ArrayPool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet()
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(1024); // Rent a default 1KB buffer
            _buffer = _memoryOwner.Memory;
            _buffer.Span.Clear(); // Clear the buffer (optional, as data will be written over it)
            _dataLength = HEADER_SIZE; // Initial data length is just the header size
            Position = HEADER_SIZE;    // Start writing after the header
        }

        /// <summary>
        /// Constructor for incoming packets from an array segment.
        /// This constructor does not rent a new buffer, it uses the provided array directly.
        /// </summary>
        /// <param name="data">The byte array containing the packet data.</param>
        /// <param name="offset">The offset within the array where the packet data begins.</param>
        /// <param name="length">The length of the packet data in the array.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(byte[] data, int offset, int length)
        {
            _memoryOwner = null; // No ownership of a rented buffer
            _buffer = new Memory<byte>(data, offset, length); // Create a Memory<byte> slice
            _dataLength = length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for incoming packets from a full byte array.
        /// This constructor does not rent a new buffer, it uses the provided array directly.
        /// </summary>
        /// <param name="data">The byte array containing the packet data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(byte[] data)
        {
            _memoryOwner = null; // No ownership of a rented buffer
            _buffer = data;      // Use the provided array directly
            _dataLength = data.Length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for outgoing packets with a specified type and initial capacity.
        /// Rents a buffer from ArrayPool and pre-writes the packet type.
        /// </summary>
        /// <param name="type">The type of the game packet (as a short).</param>
        /// <param name="capacity">The initial capacity of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(short type, int capacity = 1024)
        {
            // Ensure capacity is at least enough for header and signature
            int actualCapacity = Math.Max(capacity, HEADER_SIZE + SIGNATURE_SIZE);
            _memoryOwner = MemoryPool<byte>.Shared.Rent(actualCapacity);
            _buffer = _memoryOwner.Memory;

            // Get a reference to the start of the buffer span for efficient writing
            ref byte spanRef = ref MemoryMarshal.GetReference(_buffer.Span);
            // Write a placeholder for the packet length (will be updated in FinalizePacket)
            BinaryPrimitives.WriteInt16LittleEndian(MemoryMarshal.CreateSpan(ref spanRef, 2), 0);
            // Write the packet type
            BinaryPrimitives.WriteInt16LittleEndian(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref spanRef, 2), 2), type);

            _dataLength = HEADER_SIZE; // Initial data length is just the header
            Position = HEADER_SIZE;    // Start writing payload after the header
        }

        /// <summary>
        /// Constructor for outgoing packets with a specified GamePackets enum type and initial capacity.
        /// Rents a buffer from ArrayPool and pre-writes the packet type.
        /// </summary>
        /// <param name="type">The GamePackets enum value representing the packet type.</param>
        /// <param name="capacity">The initial capacity of the packet buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet(GamePackets type, int capacity = 1024)
            : this((short)type, capacity) // Calls the short-based constructor
        {
        }

        // --- Read Methods (Optimized with BinaryPrimitives) ---

        /// <summary>
        /// Reads a UInt16 (2 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            EnsureCanRead(2); // Ensure enough bytes are available
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span[Position..]);
            Position += 2;
            return value;
        }

        /// <summary>
        /// Reads a UInt32 (4 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            EnsureCanRead(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads an Int32 (4 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            EnsureCanRead(4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads a UInt64 (8 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            EnsureCanRead(8);
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        /// <summary>
        /// Reads a single byte from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            EnsureCanRead(1);
            return _buffer.Span[Position++];
        }

        /// <summary>
        /// Reads a specified number of bytes into a destination Span and advances the position.
        /// </summary>
        /// <param name="destination">The span to write the read bytes into.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> destination)
        {
            EnsureCanRead(destination.Length);
            _buffer.Span.Slice(Position, destination.Length).CopyTo(destination);
            Position += destination.Length;
        }

        /// <summary>
        /// Reads a specified number of bytes and returns them as a new byte array.
        /// Note: This allocates a new array. Use ReadBytes(Span<byte>) when possible to avoid allocations.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A new byte array containing the read bytes.</returns>
        public byte[] ReadBytes(int count)
        {
            EnsureCanRead(count);
            byte[] result = _buffer.Span.Slice(Position, count).ToArray(); // Allocates a new array
            Position += count;
            return result;
        }

        /// <summary>
        /// Reads a string of a specified length from the current position and advances the position.
        /// Null terminators are handled.
        /// </summary>
        /// <param name="length">The maximum length of the string t o read.</param>
        /// <returns>The decoded string.</returns>
        public string ReadString(int length)
        {
            EnsureCanRead(length);
            var stringSpan = _buffer.Span.Slice(Position, length);
            Position += length;

            // Find the null terminator and slice the span if found
            int nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex];

            return Encoding.UTF8.GetString(stringSpan); // Allocates a new string
        }

        /// <summary>
        /// Reads a Float (4 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            EnsureCanRead(4);
            float value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        /// <summary>
        /// Reads a Double (8 bytes) from the current position and advances the position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            EnsureCanRead(8);
            double value = BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        // --- Write Methods (Optimized with BinaryPrimitives) ---

        /// <summary>
        /// Writes a UInt16 (2 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The UInt16 value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            EnsureCanWrite(2); // Ensure enough space is available in the buffer
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Span[Position..], value);
            Position += 2;
            UpdateDataLength(); // Update the actual data length if position moved past it
        }

        /// <summary>
        /// Writes a UInt32 (4 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The UInt32 value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes an Int32 (4 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The Int32 value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a UInt64 (8 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The UInt64 value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a single byte to the current position and advances the position.
        /// </summary>
        /// <param name="value">The byte value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureCanWrite(1);
            _buffer.Span[Position++] = value;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a sequence of bytes from a ReadOnlySpan to the current position and advances the position.
        /// </summary>
        /// <param name="data">The ReadOnlySpan containing the bytes to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureCanWrite(data.Length);
            data.CopyTo(_buffer.Span[Position..]);
            Position += data.Length;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a string to the current position with a specified maximum length, padding with zeros if shorter.
        /// Advances the position.
        /// </summary>
        /// <param name="value">The string to write.</param>
        /// <param name="maxLength">The maximum number of bytes to write (padding with zeros if shorter).</param>
        public void WriteString(string value, int maxLength)
        {
            EnsureCanWrite(maxLength);
            var destSpan = _buffer.Span.Slice(Position, maxLength);

            // Encode the string into the destination span
            int bytesWritten = Encoding.UTF8.GetBytes(value.AsSpan(), destSpan);

            // If the string is shorter than maxLength, clear the remaining bytes with zeros (padding)
            if (bytesWritten < maxLength)
            {
                destSpan[bytesWritten..].Clear();
            }

            Position += maxLength;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a Float (4 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The Float value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            EnsureCanWrite(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        /// <summary>
        /// Writes a Double (8 bytes) to the current position and advances the position.
        /// </summary>
        /// <param name="value">The Double value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            EnsureCanWrite(8);
            BinaryPrimitives.WriteDoubleLittleEndian(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        // --- Position Management ---

        /// <summary>
        /// Sets the current read/write position within the buffer.
        /// </summary>
        /// <param name="position">The new position to set.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the position is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Seek(int position)
        {
            if (position < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(position));
            Position = position;
        }

        /// <summary>
        /// Sets the current read/write position to the start of the payload (after the header) plus an offset.
        /// </summary>
        /// <param name="payloadOffset">The offset from the start of the payload.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the payloadOffset is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SeekToPayload(int payloadOffset)
        {
            if (payloadOffset < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(payloadOffset));
            Position = HEADER_SIZE + payloadOffset;
        }

        /// <summary>
        /// Advances the current read/write position by a specified amount.
        /// </summary>
        /// <param name="amount">The number of bytes to skip.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the amount is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int amount)
        {
            if (amount < 0)
                ThrowHelper.ThrowArgumentOutOfRange(nameof(amount));
            Position += amount;
        }

        // --- Finalization ---

        /// <summary>
        /// Finalizes the packet by writing the server signature and updating the packet length in the header.
        /// </summary>
        /// <param name="type">The GamePackets enum value representing the packet type.</param>
        public void FinalizePacket(GamePackets type)
        {
            WriteSeal(); // Write the server signature
            // Write the actual payload length (total data length - signature size) to the header
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE));
            // Write the packet type to the header (overwriting the placeholder)
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], (short)type);
        }

        /// <summary>
        /// Finalizes the packet by writing the server signature and updating the packet length in the header.
        /// </summary>
        /// <param name="type">The packet type as a short.</param>
        public void FinalizePacket(short type)
        {
            WriteSeal(); // Write the server signature
            // Write the actual payload length (total data length - signature size) to the header
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span, (short)(_dataLength - SIGNATURE_SIZE));
            // Write the packet type to the header (overwriting the placeholder)
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.Span[2..], type);
        }

        /// <summary>
        /// Writes the server signature (seal) to the end of the packet.
        /// </summary>
        public void WriteSeal()
        {
            var signatureBytes = Encoding.ASCII.GetBytes(SERVER_SIGNATURE);
            EnsureCanWrite(signatureBytes.Length);
            signatureBytes.CopyTo(_buffer.Span[Position..]);
            Position += signatureBytes.Length;
            UpdateDataLength();
        }

        /// <summary>
        /// Resets the packet's internal state, clearing its buffer and resetting position and data length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _buffer.Span.Clear(); // Clear all bytes in the buffer
            Position = HEADER_SIZE; // Reset position to after the header
            _dataLength = HEADER_SIZE; // Reset data length to just the header size
        }

        /// <summary>
        /// Gets the final ReadOnlyMemory<byte> representing the complete packet data.
        /// </summary>
        /// <returns>A ReadOnlyMemory<byte> containing the packet data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetFinalizedMemory() => _buffer[.._dataLength];

        // --- Special Methods ---

        /// <summary>
        /// Attempts to extract the Diffie-Hellman public key from a key exchange packet.
        /// </summary>
        /// <param name="dhKey">Output: The extracted DH key string if successful.</param>
        /// <returns>True if the DH key was successfully extracted, false otherwise.</returns>
        public bool TryExtractDHKey(out string dhKey)
        {
            dhKey = string.Empty;
            int originalPosition = Position; // Store original position to restore later

            try
            {
                Seek(11); // Seek to a specific offset where key data typically starts
                int offset = ReadInt32() + 4 + 11; // Calculate the actual offset for the key

                if (offset > 0 && offset < _dataLength)
                {
                    Seek(offset); // Seek to the calculated key offset
                    int keySize = ReadInt32(); // Read the size of the key

                    if (keySize > 0 && keySize < _dataLength - offset)
                    {
                        dhKey = ReadString(keySize); // Read the key string
                        return !string.IsNullOrEmpty(dhKey); // Return true if key is not empty
                    }
                }
            }
            catch { /* Ignore exceptions during key extraction to return false */ }
            finally { Position = originalPosition; } // Always restore original position

            return false;
        }

        /// <summary>
        /// Gets the number of remaining readable bytes in the packet's payload.
        /// </summary>
        public int RemainingBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Calculate the effective end of data (excluding signature if complete)
                int endOfData = _dataLength;
                if (IsComplete) endOfData -= SIGNATURE_SIZE;
                // Return the maximum of 0 or the difference between end of data and current position
                return Math.Max(0, endOfData - Position);
            }
        }

        /// <summary>
        /// Checks if the packet has a client signature.
        /// </summary>
        /// <returns>True if the packet is complete and has a client signature, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsClientPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(CLIENT_SIGNATURE));
        }

        /// <summary>
        /// Checks if the packet has a server signature.
        /// </summary>
        /// <returns>True if the packet is complete and has a server signature, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsServerPacket()
        {
            if (!IsComplete) return false;
            var signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            return signatureSpan.SequenceEqual(Encoding.ASCII.GetBytes(SERVER_SIGNATURE));
        }

        // --- Helper Methods ---

        /// <summary>
        /// Ensures that there are enough bytes available to read from the current position.
        /// </summary>
        /// <param name="bytes">The number of bytes to check for.</param>
        /// <exception cref="InvalidOperationException">Thrown if not enough bytes are available.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanRead(int bytes)
        {
            if (Position + bytes > _dataLength)
                ThrowHelper.ThrowInvalidOperation($"Cannot read {bytes} bytes. Position: {Position}, Available: {_dataLength - Position}");
        }

        /// <summary>
        /// Ensures that there is enough space available to write to the current position.
        /// </summary>
        /// <param name="bytes">The number of bytes to check for space.</param>
        /// <exception cref="InvalidOperationException">Thrown if not enough space is available (buffer overflow).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCanWrite(int bytes)
        {
            if (Position + bytes > _buffer.Length)
                ThrowHelper.ThrowInvalidOperation($"Cannot write {bytes} bytes. Buffer overflow. Position: {Position}, Capacity: {_buffer.Length}");
        }

        /// <summary>
        /// Updates the internal data length if the current position has extended beyond the previously recorded length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDataLength()
        {
            if (Position > _dataLength)
                _dataLength = Position;
        }

        /// <summary>
        /// Disposes the packet, returning its rented memory to the pool if applicable.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryOwner?.Dispose(); // Return rented memory to the pool
                _disposed = true;
            }
        }

        // --- Protobuf Methods ---

        /// <summary>
        /// Deserializes the packet's payload into a Protobuf message of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the Protobuf message.</typeparam>
        /// <returns>The deserialized Protobuf message.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no data is available for deserialization.</exception>
        public T DeserializeProto<T>()
        {
            int originalPosition = Position; // Store original position
            try
            {
                // Calculate the length of the actual protobuf payload (total length - header size)
                int dataLength = Length - 4;
                if (dataLength <= 0)
                    ThrowHelper.ThrowInvalidOperation("No data available to deserialize");

                Seek(4); // Seek to the start of the payload (after header)
                var data = _buffer.Span.Slice(Position, dataLength); // Get a span of the payload data

                // Use a MemoryStream to wrap the data for Protobuf-net deserialization
                // Note: MemoryStream's constructor with Span<byte> is efficient.
                using var ms = new MemoryStream();
                ms.Write(data); // Write the payload data into the MemoryStream
                ms.Position = 0; // Reset stream position to the beginning
                return Serializer.Deserialize<T>(ms); // Deserialize the object
            }
            finally { Position = originalPosition; } // Restore original position
        }

        /// <summary>
        /// Serializes a Protobuf message into the packet's payload.
        /// </summary>
        /// <typeparam name="T">The type of the Protobuf message.</typeparam>
        /// <param name="message">The Protobuf message to serialize.</param>
        public void SerializeProto<T>(T message)
        {
            int originalPosition = Position; // Store original position
            try
            {
                Seek(4); // Seek to the start of the payload (after header)
                using var ms = new MemoryStream(); // Create a MemoryStream for serialization
                Serializer.Serialize(ms, message); // Serialize the message into the stream
                var data = ms.GetBuffer().AsSpan(0, (int)ms.Length); // Get the serialized data as a span
                WriteBytes(data); // Write the serialized data into the packet's buffer
            }
            finally { Position = originalPosition; } // Restore original position
        }
    }

    // Helper class for throwing exceptions without inlining, to keep the main methods lean.
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

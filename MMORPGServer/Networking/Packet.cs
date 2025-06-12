


namespace MMORPGServer.Networking
{
    /// <summary>
    /// Represents a network packet with unified reading and writing operations.
    /// Uses a single position tracker since packets are either read from OR written to.
    /// </summary>
    public sealed class Packet : IDisposable, IPacket
    {
        private const string CLIENT_SIGNATURE = "TQClient";
        private const string SERVER_SIGNATURE = "TQServer";
        private const int SIGNATURE_SIZE = 8; // "TQClient" or "TQServer"
        private const int HEADER_SIZE = 4;    // Packet Length (ushort) + Packet Type (ushort)

        internal readonly IMemoryOwner<byte> _memoryOwner;
        internal Memory<byte> _buffer;
        private int _dataLength;

        /// <summary>
        /// Gets a ReadOnlySpan of the packet's valid data.
        /// </summary>
        public ReadOnlySpan<byte> Data => _buffer.Span[.._dataLength];

        /// <summary>
        /// Gets the total length of the packet as declared in its header (first 2 bytes).
        /// This is the length WITHOUT the signature.
        /// </summary>
        public ushort Length => _dataLength >= 2 ? BitConverter.ToUInt16(_buffer.Span[..2]) : (ushort)0;

        /// <summary>
        /// Gets the type of the packet as declared in its header (bytes 2-3).
        /// </summary>
        public GamePackets Type => (GamePackets)(_dataLength >= 4 ? BitConverter.ToUInt16(_buffer.Span[2..4]) : (ushort)0);

        /// <summary>
        /// Current position in the buffer (for reading or writing operations).
        /// </summary>
        public int Position { get; private set; } = 0;
        public IPacketReader GetReader()
        {
            return new Fluent.FluentPacketReader(this);
        }

        public IPacketWriter GetWriter()
        {
            return new Fluent.FluentPacketWriter(Type);
        }

        /// <summary>
        /// Checks if the packet appears to be complete with a valid signature.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                ushort declaredLength = Length;
                if (declaredLength == 0) return false;

                // Total packet size = declared length + signature size
                int totalPacketSize = declaredLength + SIGNATURE_SIZE;
                if (totalPacketSize > _dataLength) return false;
                if (declaredLength < HEADER_SIZE) return false;

                // Check signature at the end of total packet
                Span<byte> signatureSpan = _buffer.Span.Slice(totalPacketSize - SIGNATURE_SIZE, SIGNATURE_SIZE);
                string signature = Encoding.ASCII.GetString(signatureSpan);
                return signature == CLIENT_SIGNATURE || signature == SERVER_SIGNATURE;
            }
        }

        /// <summary>
        /// Constructor for incoming packets (data already exists).
        /// Position starts after the header for reading payload data.
        /// </summary>
        public Packet(ReadOnlySpan<byte> data)
        {
            _memoryOwner = null;
            _buffer = data.ToArray();
            _dataLength = data.Length;
            Position = HEADER_SIZE; // Start reading after header
        }

        /// <summary>
        /// Constructor for incoming packets from an array segment.
        /// </summary>
        public Packet(byte[] data, int offset, int length)
        {
            _memoryOwner = null;
            byte[] packetData = new byte[length];
            Array.Copy(data, offset, packetData, 0, length);
            _buffer = packetData;
            _dataLength = length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for incoming packets from a full array.
        /// </summary>
        public Packet(byte[] data)
        {
            _memoryOwner = null;
            _buffer = (byte[])data.Clone();
            _dataLength = data.Length;
            Position = HEADER_SIZE;
        }

        /// <summary>
        /// Constructor for outgoing packets. Creates buffer and writes header.
        /// Position starts after the header for writing payload data.
        /// </summary>
        public Packet(ushort type, bool isServerPacket = true, int capacity = 1024)
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(Math.Max(capacity, HEADER_SIZE + SIGNATURE_SIZE));
            _buffer = _memoryOwner.Memory;
            _buffer.Span.Clear();

            // Write header: placeholder length (0) and packet type
            _ = BitConverter.TryWriteBytes(_buffer.Span[0..2], (ushort)0);
            _ = BitConverter.TryWriteBytes(_buffer.Span[2..4], type);
            _dataLength = HEADER_SIZE;
            Position = HEADER_SIZE; // Start writing after header
        }



        public ushort ReadUInt16()
        {
            EnsureCanRead(2);
            ushort value = BitConverter.ToUInt16(_buffer.Span[Position..]);
            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            EnsureCanRead(4);
            uint value = BitConverter.ToUInt32(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        public int ReadInt32()
        {
            EnsureCanRead(4);
            int value = BitConverter.ToInt32(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            EnsureCanRead(8);
            ulong value = BitConverter.ToUInt64(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        public byte ReadByte()
        {
            EnsureCanRead(1);
            byte value = _buffer.Span[Position];
            Position++;
            return value;
        }

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
            Span<byte> stringSpan = _buffer.Span.Slice(Position, length);
            Position += length;

            int nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex];

            return Encoding.UTF8.GetString(stringSpan);
        }

        public float ReadFloat()
        {
            EnsureCanRead(4);
            float value = BitConverter.ToSingle(_buffer.Span[Position..]);
            Position += 4;
            return value;
        }

        public double ReadDouble()
        {
            EnsureCanRead(8);
            double value = BitConverter.ToDouble(_buffer.Span[Position..]);
            Position += 8;
            return value;
        }

        // --- Write Methods ---

        public void WriteUInt16(ushort value)
        {
            EnsureCanWrite(2);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 2;
            UpdateDataLength();
        }

        public void WriteUInt32(uint value)
        {
            EnsureCanWrite(4);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        public void WriteInt32(int value)
        {
            EnsureCanWrite(4);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        public void WriteUInt64(ulong value)
        {
            EnsureCanWrite(8);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        public void WriteByte(byte value)
        {
            EnsureCanWrite(1);
            _buffer.Span[Position] = value;
            Position++;
            UpdateDataLength();
        }

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
            ReadOnlySpan<char> valueSpan = value.AsSpan();
            ReadOnlySpan<char> valueToEncode = valueSpan.Length > maxLength ? valueSpan[..maxLength] : valueSpan;

            int bytesWritten = Encoding.UTF8.GetBytes(valueToEncode, _buffer.Span.Slice(Position, maxLength));

            if (bytesWritten < maxLength)
            {
                _buffer.Span.Slice(Position + bytesWritten, maxLength - bytesWritten).Clear();
            }
            Position += maxLength;
            UpdateDataLength();
        }

        public void WriteFloat(float value)
        {
            EnsureCanWrite(4);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 4;
            UpdateDataLength();
        }

        public void WriteDouble(double value)
        {
            EnsureCanWrite(8);
            BitConverter.TryWriteBytes(_buffer.Span[Position..], value);
            Position += 8;
            UpdateDataLength();
        }

        // --- Position Management ---

        /// <summary>
        /// Sets the position to a specific location in the packet.
        /// </summary>
        public void Seek(int position)
        {
            if (position < 0)
                throw new ArgumentOutOfRangeException(nameof(position), "Position cannot be negative.");

            Position = position;
        }

        /// <summary>
        /// Sets the position relative to the start of the payload (after header).
        /// </summary>
        public void SeekToPayload(int payloadOffset)
        {
            if (payloadOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(payloadOffset), "Payload offset cannot be negative.");

            Position = HEADER_SIZE + payloadOffset;
        }

        /// <summary>
        /// Moves the position forward by the specified amount.
        /// </summary>
        public void Skip(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

            Position += amount;
        }

        // --- Finalization ---

        /// <summary>
        /// Finalizes an outgoing packet by appending the signature and updating the length header.
        /// </summary>
        public void FinalizePacket(GamePackets Type)
        {
            WriteSeal();
            _ = BitConverter.TryWriteBytes(_buffer.Span[0..2], (ushort)(_dataLength - SIGNATURE_SIZE));
            _ = BitConverter.TryWriteBytes(_buffer.Span[2..4], (ushort)Type);

        }
        public void FinalizePacket(ushort Type)
        {
            WriteSeal();
            _ = BitConverter.TryWriteBytes(_buffer.Span[0..2], (ushort)(_dataLength - SIGNATURE_SIZE));
            _ = BitConverter.TryWriteBytes(_buffer.Span[2..4], Type);

        }
        /// <summary>
        /// Writes only the signature without updating the length header.
        /// Used for special packet construction like DH key exchange.
        /// </summary>
        public void WriteSeal()
        {
            byte[] signatureBytes = Encoding.ASCII.GetBytes(SERVER_SIGNATURE);
            EnsureCanWrite(signatureBytes.Length);

            signatureBytes.CopyTo(_buffer.Span[Position..]);
            Position += signatureBytes.Length;
            UpdateDataLength();
        }
        /// <summary>
        /// Clears the packet contents and resets the position for reuse.
        /// If it's an outgoing packet, you can optionally provide a new type.
        /// </summary>
        public void Reset()
        {
            _buffer.Span.Clear();
            Position = HEADER_SIZE;
            _dataLength = HEADER_SIZE;
        }
        /// <summary>
        /// Gets the finalized memory ready for transmission.
        /// </summary>
        public ReadOnlyMemory<byte> GetFinalizedMemory()
        {
            return _buffer[.._dataLength];
        }

        // --- Special Methods ---

        /// <summary>
        /// Attempts to extract the Diffie-Hellman public key from this packet.
        /// </summary>
        public bool TryExtractDHKey(out string dhKey)
        {
            dhKey = string.Empty;
            int originalPosition = Position;

            try
            {
                // Seek to position 11 to read the offset
                Seek(11);
                int offset = ReadInt32() + 4 + 11;

                if (offset > 0 && offset < _dataLength)
                {
                    // Seek to the calculated offset where the key information is stored
                    Seek(offset);

                    // Read the size of the key string
                    int keySize = ReadInt32();

                    if (keySize > 0 && keySize < _dataLength - offset)
                    {
                        // Read the key itself
                        dhKey = ReadString(keySize);
                        return !string.IsNullOrEmpty(dhKey);
                    }
                }
            }
            catch (Exception) { /* Parsing error */ }
            finally { Position = originalPosition; }

            return false;

        }

        /// <summary>
        /// Gets the number of remaining bytes available for reading.
        /// </summary>
        public int RemainingBytes
        {
            get
            {
                int endOfData = _dataLength;
                if (IsComplete)
                    endOfData -= SIGNATURE_SIZE;

                return Math.Max(0, endOfData - Position);
            }
        }

        /// <summary>
        /// Checks if this is a client packet based on its signature.
        /// </summary>
        public bool IsClientPacket()
        {
            if (!IsComplete) return false;
            // Signature is at the end of the actual data, not at Length position
            Span<byte> signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            string signature = Encoding.ASCII.GetString(signatureSpan);
            return signature == CLIENT_SIGNATURE;
        }

        /// <summary>
        /// Checks if this is a server packet based on its signature.
        /// </summary>
        public bool IsServerPacket()
        {
            if (!IsComplete) return false;
            // Signature is at the end of the actual data, not at Length position
            Span<byte> signatureSpan = _buffer.Span.Slice(_dataLength - SIGNATURE_SIZE, SIGNATURE_SIZE);
            string signature = Encoding.ASCII.GetString(signatureSpan);
            return signature == SERVER_SIGNATURE;
        }

        // --- Helper Methods ---

        private void EnsureCanRead(int bytes)
        {
            if (Position + bytes > _dataLength)
                throw new InvalidOperationException($"Cannot read {bytes} bytes. Position: {Position}, Available: {_dataLength - Position}");
        }

        private void EnsureCanWrite(int bytes)
        {
            if (Position + bytes > _buffer.Length)
                throw new InvalidOperationException($"Cannot write {bytes} bytes. Buffer overflow. Position: {Position}, Capacity: {_buffer.Length}");
        }

        private void UpdateDataLength()
        {
            _dataLength = Math.Max(_dataLength, Position);
        }

        public void Dispose()
        {
            _memoryOwner?.Dispose();
        }

        /// <summary>
        /// Deserializes a protobuf message from the packet's data.
        /// </summary>
        /// <typeparam name="T">The type of protobuf message to deserialize</typeparam>
        /// <returns>The deserialized protobuf message</returns>
        /// <exception cref="InvalidOperationException">Thrown if the data cannot be read</exception>
        public T DeserializeProto<T>()
        {
            int originalPosition = Position;
            try
            {
                // Calculate the actual data to read
                int dataLength = Length;
                dataLength = dataLength - 4;
                if (dataLength <= 0)
                    throw new InvalidOperationException("No data available to deserialize");

                // Move to the start position
                Seek(4);

                // Read the data
                byte[] data = ReadBytes(dataLength);

                // Deserialize the protobuf message
                using MemoryStream ms = new MemoryStream(data);
                return Serializer.Deserialize<T>(ms);
            }
            finally
            {
                // Restore the original position
                Position = originalPosition;
            }
        }

        /// <summary>
        /// Serializes a protobuf message into the packet.
        /// </summary>
        /// <typeparam name="T">The type of protobuf message to serialize</typeparam>
        /// <param name="message">The protobuf message to serialize</param>
        /// <exception cref="InvalidOperationException">Thrown if the data cannot be written</exception>
        public void SerializeProto<T>(T message)
        {
            int originalPosition = Position;
            try
            {
                // Move to the start position
                Seek(4);

                // Serialize the protobuf message
                using MemoryStream ms = new MemoryStream();
                Serializer.Serialize(ms, message);
                byte[] data = ms.ToArray();

                // Write the data
                WriteBytes(data);
            }
            finally
            {
                // Restore the original position
                Position = originalPosition;
            }
        }
    }
}
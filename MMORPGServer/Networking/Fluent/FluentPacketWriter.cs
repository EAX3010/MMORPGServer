using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Packets;
using System.Runtime.CompilerServices;

namespace MMORPGServer.Networking.Fluent
{
    /// <summary>
    /// High-performance fluent API for writing network packets.
    /// </summary>
    public sealed class FluentPacketWriter : IDisposable
    {
        private readonly Packet _packet;
        private readonly GamePackets _packetType;
        private bool _disposed;
        private bool _finalized;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter(GamePackets type, int initialCapacity = 1024)
        {
            _packetType = type;
            _packet = new Packet((short)type, initialCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter(short type, int initialCapacity = 1024)
        {
            _packetType = (GamePackets)type;
            _packet = new Packet(type, initialCapacity);
        }

        // Properties
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.Position;
        }

        public GamePackets PacketType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packetType;
        }

        // Write methods with aggressive inlining
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteUInt16(ushort value)
        {
            ThrowIfFinalized();
            _packet.WriteUInt16(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteUInt32(uint value)
        {
            ThrowIfFinalized();
            _packet.WriteUInt32(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteInt32(int value)
        {
            ThrowIfFinalized();
            _packet.WriteInt32(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteUInt64(ulong value)
        {
            ThrowIfFinalized();
            _packet.WriteUInt64(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteByte(byte value)
        {
            ThrowIfFinalized();
            _packet.WriteByte(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteBytes(ReadOnlySpan<byte> data)
        {
            ThrowIfFinalized();
            _packet.WriteBytes(data);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteString(string value, int maxLength)
        {
            ThrowIfFinalized();
            _packet.WriteString(value, maxLength);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteFloat(float value)
        {
            ThrowIfFinalized();
            _packet.WriteFloat(value);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteDouble(double value)
        {
            ThrowIfFinalized();
            _packet.WriteDouble(value);
            return this;
        }

        // Optimized array writing for primitive types
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteUInt32Array(ReadOnlySpan<uint> values)
        {
            ThrowIfFinalized();
            foreach (var value in values)
            {
                _packet.WriteUInt32(value);
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteInt32Array(ReadOnlySpan<int> values)
        {
            ThrowIfFinalized();
            foreach (var value in values)
            {
                _packet.WriteInt32(value);
            }
            return this;
        }

        public FluentPacketWriter WriteArray<T>(IEnumerable<T> items, Action<FluentPacketWriter, T> writeAction)
        {
            ThrowIfFinalized();
            foreach (T item in items)
            {
                writeAction(this, item);
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteConditional(bool condition, Action<FluentPacketWriter> writeAction)
        {
            if (condition)
            {
                ThrowIfFinalized();
                writeAction(this);
            }
            return this;
        }

        // Position management
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter Seek(int position)
        {
            ThrowIfFinalized();
            _packet.Seek(position);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter SeekToPayload(int payloadOffset)
        {
            ThrowIfFinalized();
            _packet.SeekToPayload(payloadOffset);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter Skip(int bytes)
        {
            ThrowIfFinalized();
            _packet.Skip(bytes);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter Align(int boundary)
        {
            ThrowIfFinalized();
            int currentPos = _packet.Position;
            int remainder = currentPos % boundary;
            if (remainder != 0)
            {
                int padding = boundary - remainder;
                for (int i = 0; i < padding; i++)
                {
                    _packet.WriteByte(0);
                }
            }
            return this;
        }

        // Performance helper: Write multiple values at once
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteVector3(float x, float y, float z)
        {
            ThrowIfFinalized();
            _packet.WriteFloat(x);
            _packet.WriteFloat(y);
            _packet.WriteFloat(z);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteVector2(float x, float y)
        {
            ThrowIfFinalized();
            _packet.WriteFloat(x);
            _packet.WriteFloat(y);
            return this;
        }

        // Protobuf serialization
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter SerializeProto<T>(T message)
        {
            ThrowIfFinalized();
            _packet.SerializeProto(message);
            return this;
        }

        // Debug method (not inlined for performance)
        [MethodImpl(MethodImplOptions.NoInlining)]
        public FluentPacketWriter Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PacketWriter] {message} | Type: {_packetType}, Position: {_packet.Position}");
            return this;
        }

        // Building methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Packet Build()
        {
            if (_finalized)
                throw new InvalidOperationException("Packet has already been finalized");

            _packet.FinalizePacket(_packetType);
            _finalized = true;
            return _packet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> BuildAndFinalize()
        {
            if (_finalized)
                throw new InvalidOperationException("Packet has already been finalized");

            _packet.FinalizePacket(_packetType);
            _finalized = true;
            return _packet.GetFinalizedMemory();
        }

        // Helper to ensure we don't write to finalized packets
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfFinalized()
        {
            if (_finalized)
                ThrowFinalizedError();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowFinalizedError()
        {
            throw new InvalidOperationException("Cannot modify a finalized packet");
        }

        // Reserve space for later writing
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter ReserveSpace(int bytes, out int reservedPosition)
        {
            ThrowIfFinalized();
            reservedPosition = _packet.Position;
            _packet.Skip(bytes);
            return this;
        }

        // Write at a previously reserved position
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketWriter WriteAtReserved(int reservedPosition, Action<FluentPacketWriter> writeAction)
        {
            ThrowIfFinalized();
            int currentPos = _packet.Position;
            _packet.Seek(reservedPosition);
            writeAction(this);
            _packet.Seek(currentPos);
            return this;
        }

        // Batch write for performance
        public FluentPacketWriter WriteBatch(Action<BatchWriter> batchAction)
        {
            ThrowIfFinalized();
            var batch = new BatchWriter(this);
            batchAction(batch);
            return this;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _packet?.Dispose();
                _disposed = true;
            }
        }

        // Inner class for batch operations
        public sealed class BatchWriter
        {
            private readonly FluentPacketWriter _writer;

            internal BatchWriter(FluentPacketWriter writer)
            {
                _writer = writer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter UInt16(ushort value)
            {
                _writer._packet.WriteUInt16(value);
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter UInt32(uint value)
            {
                _writer._packet.WriteUInt32(value);
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter Int32(int value)
            {
                _writer._packet.WriteInt32(value);
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter Byte(byte value)
            {
                _writer._packet.WriteByte(value);
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter Float(float value)
            {
                _writer._packet.WriteFloat(value);
                return this;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BatchWriter String(string value, int maxLength)
            {
                _writer._packet.WriteString(value, maxLength);
                return this;
            }
        }
    }
}
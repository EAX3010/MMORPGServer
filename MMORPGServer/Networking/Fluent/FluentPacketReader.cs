using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Packets.Core;
using System.Runtime.CompilerServices;

namespace MMORPGServer.Networking.Fluent
{
    /// <summary>
    /// High-performance fluent API for reading network packets.
    /// </summary>
    public sealed class FluentPacketReader : IDisposable
    {
        private readonly Packet _packet;
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal FluentPacketReader(Packet packet)
        {
            _packet = packet ?? throw new ArgumentNullException(nameof(packet));
        }


        // Properties with aggressive inlining for performance
        public short Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.Length;
        }

        public GamePackets Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.Type;
        }

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.Position;
        }

        public int RemainingBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.RemainingBytes;
        }

        public bool IsComplete
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.IsComplete;
        }

        public bool IsClientPacket
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.IsClientPacket();
        }

        public bool IsServerPacket
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _packet.IsServerPacket();
        }

        // Read methods with aggressive inlining
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadUInt16(out ushort value)
        {
            value = _packet.ReadUInt16();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadUInt32(out uint value)
        {
            value = _packet.ReadUInt32();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadInt32(out int value)
        {
            value = _packet.ReadInt32();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadUInt64(out ulong value)
        {
            value = _packet.ReadUInt64();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadByte(out byte value)
        {
            value = _packet.ReadByte();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadBytes(int count, out byte[] data)
        {
            data = _packet.ReadBytes(count);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadBytes(Span<byte> destination)
        {
            _packet.ReadBytes(destination);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadString(int length, out string value)
        {
            value = _packet.ReadString(length);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadFloat(out float value)
        {
            value = _packet.ReadFloat();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadDouble(out double value)
        {
            value = _packet.ReadDouble();
            return this;
        }

        // Array reading with delegate for performance
        public FluentPacketReader ReadArray<T>(int count, Func<FluentPacketReader, T> readFunc, out T[] items)
        {
            items = new T[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = readFunc(this);
            }
            return this;
        }

        // Optimized array reading for primitive types
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadUInt32Array(int count, out uint[] items)
        {
            items = new uint[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = _packet.ReadUInt32();
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadInt32Array(int count, out int[] items)
        {
            items = new int[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = _packet.ReadInt32();
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadConditional(bool condition, Action<FluentPacketReader> readAction)
        {
            if (condition)
            {
                readAction(this);
            }
            return this;
        }

        // Position management
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader Seek(int position)
        {
            _packet.Seek(position);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader SeekToPayload(int payloadOffset)
        {
            _packet.SeekToPayload(payloadOffset);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader Skip(int bytes)
        {
            _packet.Skip(bytes);
            return this;
        }

        // Special methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DeserializeProto<T>()
        {
            return _packet.DeserializeProto<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryExtractDHKey(out string dhKey)
        {
            return _packet.TryExtractDHKey(out dhKey);
        }

        // Debug method (not inlined for performance)
        [MethodImpl(MethodImplOptions.NoInlining)]
        public FluentPacketReader Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PacketReader] {message} | Position: {_packet.Position}, Remaining: {RemainingBytes}");
            return this;
        }

        // Performance helper: Read multiple values at once
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadVector3(out float x, out float y, out float z)
        {
            x = _packet.ReadFloat();
            y = _packet.ReadFloat();
            z = _packet.ReadFloat();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader ReadVector2(out float x, out float y)
        {
            x = _packet.ReadFloat();
            y = _packet.ReadFloat();
            return this;
        }

        // Validation helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluentPacketReader EnsureType(GamePackets expectedType)
        {
            if (Type != expectedType)
                throw new InvalidOperationException($"Expected packet type {expectedType}, but got {Type}");
            return this;
        }

        // Get underlying data for advanced scenarios
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetRawData()
        {
            return _packet.Data;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _packet?.Dispose();
                _disposed = true;
            }
        }
    }
}
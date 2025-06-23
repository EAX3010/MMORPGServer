using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Infrastructure.Networking.Packets;

namespace MMORPGServer.Infrastructure.Networking.Fluent
{
    internal class FluentPacketWriter : IPacketWriter
    {
        private readonly Packet _packet;
        private readonly GamePackets _packetType;

        public FluentPacketWriter(GamePackets type)
        {
            _packetType = type;
            _packet = new Packet((short)type);
        }

        public FluentPacketWriter(short type)
        {
            _packetType = (GamePackets)type;
            _packet = new Packet(type);
        }

        public IPacketWriter WriteUInt16(ushort value)
        {
            _packet.WriteUInt16(value);
            return this;
        }

        public IPacketWriter WriteUInt32(uint value)
        {
            _packet.WriteUInt32(value);
            return this;
        }

        public IPacketWriter WriteInt32(int value)
        {
            _packet.WriteInt32(value);
            return this;
        }

        public IPacketWriter WriteUInt64(ulong value)
        {
            _packet.WriteUInt64(value);
            return this;
        }

        public IPacketWriter WriteByte(byte value)
        {
            _packet.WriteByte(value);
            return this;
        }

        public IPacketWriter WriteBytes(ReadOnlySpan<byte> data)
        {
            _packet.WriteBytes(data);
            return this;
        }

        public IPacketWriter WriteString(string value, int maxLength)
        {
            _packet.WriteString(value, maxLength);
            return this;
        }

        public IPacketWriter WriteFloat(float value)
        {
            _packet.WriteFloat(value);
            return this;
        }

        public IPacketWriter WriteDouble(double value)
        {
            _packet.WriteDouble(value);
            return this;
        }
        public IPacketWriter WriteArray<T>(IEnumerable<T> items, Action<IPacketWriter, T> writeAction)
        {
            foreach (T item in items)
            {
                writeAction(this, item);
            }
            return this;
        }

        public IPacketWriter WriteConditional(bool condition, Action<IPacketWriter> writeAction)
        {
            if (condition)
            {
                writeAction(this);
            }
            return this;
        }

        public IPacketWriter Seek(int position)
        {
            _packet.Seek(position);
            return this;
        }

        public IPacketWriter Skip(int bytes)
        {
            _packet.Skip(bytes);
            return this;
        }

        public IPacketWriter Align(int boundary)
        {
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

        public IPacketWriter Debug(string message)
        {
            // Could log to console or debug output
            System.Diagnostics.Debug.WriteLine($"PacketBuilder Debug: {message} (Position: {_packet.Position})");
            return this;
        }

        public IPacket Build()
        {
            _packet.FinalizePacket(_packetType);
            return _packet;
        }

        public ReadOnlyMemory<byte> BuildAndFinalize()
        {
            _packet.FinalizePacket(_packetType);
            return _packet.GetFinalizedMemory();
        }
    }
}
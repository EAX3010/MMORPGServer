namespace MMORPGServer.Network.Fluent
{
    internal class FluentPacketBuilder : IPacketBuilder
    {
        private readonly Packet _packet;
        private readonly GamePackets _packetType;

        public FluentPacketBuilder(GamePackets type)
        {
            _packetType = type;
            _packet = new Packet((ushort)type);
        }

        public FluentPacketBuilder(ushort type)
        {
            _packetType = (GamePackets)type;
            _packet = new Packet(type);
        }

        public IPacketBuilder WriteUInt16(ushort value)
        {
            _packet.WriteUInt16(value);
            return this;
        }

        public IPacketBuilder WriteUInt32(uint value)
        {
            _packet.WriteUInt32(value);
            return this;
        }

        public IPacketBuilder WriteInt32(int value)
        {
            _packet.WriteInt32(value);
            return this;
        }

        public IPacketBuilder WriteUInt64(ulong value)
        {
            _packet.WriteUInt64(value);
            return this;
        }

        public IPacketBuilder WriteByte(byte value)
        {
            _packet.WriteByte(value);
            return this;
        }

        public IPacketBuilder WriteBytes(ReadOnlySpan<byte> data)
        {
            _packet.WriteBytes(data);
            return this;
        }

        public IPacketBuilder WriteString(string value, int maxLength)
        {
            _packet.WriteString(value, maxLength);
            return this;
        }

        public IPacketBuilder WriteFloat(float value)
        {
            _packet.WriteFloat(value);
            return this;
        }

        public IPacketBuilder WriteDouble(double value)
        {
            _packet.WriteDouble(value);
            return this;
        }

        public IPacketBuilder WriteData<T>(T data) where T : IPacketSerializable
        {
            data.Serialize(_packet);
            return this;
        }

        public IPacketBuilder WriteEncrypted(uint[] data, TransferCipher cipher)
        {
            var encrypted = cipher.Encrypt(data);
            foreach (var value in encrypted)
            {
                _packet.WriteUInt32(value);
            }
            return this;
        }

        public IPacketBuilder WriteArray<T>(IEnumerable<T> items, Action<IPacketBuilder, T> writeAction)
        {
            foreach (var item in items)
            {
                writeAction(this, item);
            }
            return this;
        }

        public IPacketBuilder WriteConditional(bool condition, Action<IPacketBuilder> writeAction)
        {
            if (condition)
            {
                writeAction(this);
            }
            return this;
        }

        public IPacketBuilder Seek(int position)
        {
            _packet.Seek(position);
            return this;
        }

        public IPacketBuilder Skip(int bytes)
        {
            _packet.Skip(bytes);
            return this;
        }

        public IPacketBuilder Align(int boundary)
        {
            var currentPos = _packet.Position;
            var remainder = currentPos % boundary;
            if (remainder != 0)
            {
                var padding = boundary - remainder;
                for (int i = 0; i < padding; i++)
                {
                    _packet.WriteByte(0);
                }
            }
            return this;
        }

        public IPacketBuilder Debug(string message)
        {
            // Could log to console or debug output
            System.Diagnostics.Debug.WriteLine($"PacketBuilder Debug: {message} (Position: {_packet.Position})");
            return this;
        }

        public Packet Build()
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
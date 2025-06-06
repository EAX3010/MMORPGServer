namespace MMORPGServer.Network.Fluent
{
    internal class FluentPacketReader : IPacketReader
    {
        private readonly Packet _packet;

        public FluentPacketReader(Packet packet)
        {
            _packet = packet;
        }

        public IPacketReader ReadUInt16(out ushort value)
        {
            value = _packet.ReadUInt16();
            return this;
        }

        public IPacketReader ReadUInt32(out uint value)
        {
            value = _packet.ReadUInt32();
            return this;
        }

        public IPacketReader ReadInt32(out int value)
        {
            value = _packet.ReadInt32();
            return this;
        }

        public IPacketReader ReadUInt64(out ulong value)
        {
            value = _packet.ReadUInt64();
            return this;
        }

        public IPacketReader ReadByte(out byte value)
        {
            value = _packet.ReadByte();
            return this;
        }

        public IPacketReader ReadBytes(int count, out byte[] data)
        {
            data = _packet.ReadBytes(count);
            return this;
        }

        public IPacketReader ReadString(int length, out string value)
        {
            value = _packet.ReadString(length);
            return this;
        }

        public IPacketReader ReadFloat(out float value)
        {
            value = _packet.ReadFloat();
            return this;
        }

        public IPacketReader ReadDouble(out double value)
        {
            value = _packet.ReadDouble();
            return this;
        }



        public IPacketReader ReadEncrypted(TransferCipher cipher, out uint[] decrypted)
        {
            var encrypted = new uint[] { _packet.ReadUInt32(), _packet.ReadUInt32() };
            decrypted = cipher.Decrypt(encrypted);
            return this;
        }

        public IPacketReader ReadArray<T>(int count, Func<IPacketReader, T> readFunc, out T[] items)
        {
            items = new T[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = readFunc(this);
            }
            return this;
        }

        public IPacketReader ReadConditional(bool condition, Action<IPacketReader> readAction)
        {
            if (condition)
            {
                readAction(this);
            }
            return this;
        }

        public IPacketReader Seek(int position)
        {
            _packet.Seek(position);
            return this;
        }

        public IPacketReader Skip(int bytes)
        {
            _packet.Skip(bytes);
            return this;
        }

        public IPacketReader Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"PacketReader Debug: {message} (Position: {_packet.Position})");
            return this;
        }


    }
}

namespace MMORPGServer.Network
{
    public ref struct ConquerPacketReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        internal ConquerPacketReader(ConquerPacket packet)
        {
            _data = packet.Data;
            _position = 4; // Skip length and type
        }

        public ushort ReadUInt16()
        {
            var value = BitConverter.ToUInt16(_data[_position..]);
            _position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            var value = BitConverter.ToUInt32(_data[_position..]);
            _position += 4;
            return value;
        }

        public byte ReadByte()
        {
            var value = _data[_position];
            _position++;
            return value;
        }

        public void ReadBytes(Span<byte> destination)
        {
            _data.Slice(_position, destination.Length).CopyTo(destination);
            _position += destination.Length;
        }

        public string ReadString(int length)
        {
            var stringSpan = _data.Slice(_position, length);
            _position += length;

            var nullIndex = stringSpan.IndexOf((byte)0);
            if (nullIndex >= 0)
                stringSpan = stringSpan[..nullIndex];

            return Encoding.UTF8.GetString(stringSpan);
        }

        public bool HasMoreData => _position < _data.Length - 8; // Account for signature
        public int RemainingBytes => Math.Max(0, _data.Length - _position - 8);
    }
}

// Network/ConquerPacketWriter.cs
namespace MMORPGServer.Network
{
    public ref struct ConquerPacketWriter
    {
        private readonly Span<byte> _buffer;
        private int _position;

        internal ConquerPacketWriter(ConquerPacket packet)
        {
            _buffer = packet._memoryOwner.Memory.Span;
            _position = 4; // Skip length and type
        }

        public void WriteUInt16(ushort value)
        {
            BitConverter.TryWriteBytes(_buffer[_position..], value);
            _position += 2;
        }

        public void WriteUInt32(uint value)
        {
            BitConverter.TryWriteBytes(_buffer[_position..], value);
            _position += 4;
        }

        public void WriteString(ReadOnlySpan<char> value, int maxLength)
        {
            var valueToEncode = value[..Math.Min(value.Length, maxLength)];
            var bytesWritten = Encoding.UTF8.GetBytes(valueToEncode, _buffer[_position..(_position + maxLength)]);

            if (bytesWritten < maxLength)
            {
                _buffer.Slice(_position + bytesWritten, maxLength - bytesWritten).Clear();
            }

            _position += maxLength;
        }

        public void WriteString(string value, int maxLength)
        {
            WriteString(value.AsSpan(), maxLength);
        }

        public void WriteByte(byte value)
        {
            _buffer[_position] = value;
            _position++;
        }

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            data.CopyTo(_buffer[_position..]);
            _position += data.Length;
        }

        public ReadOnlyMemory<byte> ToPacket(bool isServer = true)
        {
            // Write signature
            var signature = Encoding.ASCII.GetBytes(isServer ? "TQServer" : "TQClient");
            signature.CopyTo(_buffer[_position..]);
            _position += 8;

            // Update length
            BitConverter.TryWriteBytes(_buffer[0..2], (ushort)_position);
            return new ReadOnlyMemory<byte>(_buffer[.._position].ToArray());
        }
    }
}

public interface IPacketBuilder
{
    // Basic data types
    IPacketBuilder WriteUInt16(ushort value);
    IPacketBuilder WriteUInt32(uint value);
    IPacketBuilder WriteInt32(int value);
    IPacketBuilder WriteUInt64(ulong value);
    IPacketBuilder WriteByte(byte value);
    IPacketBuilder WriteBytes(ReadOnlySpan<byte> data);
    IPacketBuilder WriteString(string value, int maxLength);
    IPacketBuilder WriteFloat(float value);
    IPacketBuilder WriteDouble(double value);

    // Advanced methods
    IPacketBuilder WriteData<T>(T data) where T : IPacketSerializable;
    IPacketBuilder WriteEncrypted(uint[] data, TransferCipher cipher);
    IPacketBuilder WriteArray<T>(IEnumerable<T> items, Action<IPacketBuilder, T> writeAction);
    IPacketBuilder WriteConditional(bool condition, Action<IPacketBuilder> writeAction);

    // Position management
    IPacketBuilder Seek(int position);
    IPacketBuilder Skip(int bytes);
    IPacketBuilder Align(int boundary); // Align to 4-byte boundary, etc.

    // Finalization
    Packet Build();
    ReadOnlyMemory<byte> BuildAndFinalize();

    // Debugging
    IPacketBuilder Debug(string message); // For logging during build
}
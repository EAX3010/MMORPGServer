public interface IPacketWriter
{
    // Basic data types
    IPacketWriter WriteUInt16(ushort value);
    IPacketWriter WriteUInt32(uint value);
    IPacketWriter WriteInt32(int value);
    IPacketWriter WriteUInt64(ulong value);
    IPacketWriter WriteByte(byte value);
    IPacketWriter WriteBytes(ReadOnlySpan<byte> data);
    IPacketWriter WriteString(string value, int maxLength);
    IPacketWriter WriteFloat(float value);
    IPacketWriter WriteDouble(double value);

    // Advanced methods
    IPacketWriter WriteEncrypted(uint[] data, TransferCipher cipher);
    IPacketWriter WriteArray<T>(IEnumerable<T> items, Action<IPacketWriter, T> writeAction);
    IPacketWriter WriteConditional(bool condition, Action<IPacketWriter> writeAction);

    // Position management
    IPacketWriter Seek(int position);
    IPacketWriter Skip(int bytes);
    IPacketWriter Align(int boundary); // Align to 4-byte boundary, etc.

    // Finalization
    Packet Build();
    ReadOnlyMemory<byte> BuildAndFinalize();

    // Debugging
    IPacketWriter Debug(string message); // For logging during build
}
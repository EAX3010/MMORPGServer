public interface IPacketReader
{
    IPacketReader ReadUInt16(out ushort value);
    IPacketReader ReadUInt32(out uint value);
    IPacketReader ReadInt32(out int value);
    IPacketReader ReadUInt64(out ulong value);
    IPacketReader ReadByte(out byte value);
    IPacketReader ReadBytes(int count, out byte[] data);
    IPacketReader ReadString(int length, out string value);
    IPacketReader ReadFloat(out float value);
    IPacketReader ReadDouble(out double value);

    IPacketReader ReadArray<T>(int count, Func<IPacketReader, T> readFunc, out T[] items);
    IPacketReader ReadConditional(bool condition, Action<IPacketReader> readAction);

    IPacketReader Seek(int position);
    IPacketReader Skip(int bytes);
    IPacketReader Debug(string message);

}
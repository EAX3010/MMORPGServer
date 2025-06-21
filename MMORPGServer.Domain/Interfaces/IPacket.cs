using MMORPGServer.Domain.Enums;

namespace MMORPGServer.Domain.Interfaces
{
    public interface IPacket
    {
        ReadOnlySpan<byte> Data { get; }
        bool IsComplete { get; }
        short Length { get; }
        int Position { get; }
        int RemainingBytes { get; }
        GamePackets Type { get; }

        T DeserializeProto<T>();
        void FinalizePacket(GamePackets Type);
        void FinalizePacket(short Type);
        ReadOnlyMemory<byte> GetFinalizedMemory();
        IPacketReader GetReader();
        IPacketWriter GetWriter();
        bool IsClientPacket();
        bool IsServerPacket();
        byte ReadByte();
        byte[] ReadBytes(int count);
        void ReadBytes(Span<byte> destination);
        double ReadDouble();
        float ReadFloat();
        int ReadInt32();
        string ReadString(int length);
        ushort ReadUInt16();
        uint ReadUInt32();
        ulong ReadUInt64();
        void Reset();
        void Seek(int position);
        void SeekToPayload(int payloadOffset);
        void SerializeProto<T>(T message);
        void Skip(int amount);
        bool TryExtractDHKey(out string dhKey);
        void WriteByte(byte value);
        void WriteBytes(ReadOnlySpan<byte> data);
        void WriteDouble(double value);
        void WriteFloat(float value);
        void WriteInt32(int value);
        void WriteSeal();
        void WriteString(string value, int maxLength);
        void WriteUInt16(ushort value);
        void WriteUInt32(uint value);
        void WriteUInt64(ulong value);
    }
}
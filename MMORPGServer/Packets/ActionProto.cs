using MMORPGServer.Enums;
using ProtoBuf;
using System.Collections.Generic;

namespace MMORPGServer.Packets
{
    [ProtoContract]
    public class ActionProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint UID;
        [ProtoMember(2, IsRequired = false)]
        public uint AttackUID;
        [ProtoMember(3, IsRequired = true)]
        public ulong dwParam;
        [ProtoMember(4, IsRequired = false)]
        public uint unknown3;
        [ProtoMember(5, IsRequired = false)]
        public ulong dwParamRespond;
        [ProtoMember(6, IsRequired = false)]
        public uint unknown5;
        [ProtoMember(7, IsRequired = true)]
        public ushort dwParam_Lo;
        [ProtoMember(8, IsRequired = true)]
        public ushort dwParam_Hi;
        [ProtoMember(9, IsRequired = true)]
        public int Timestamp;
        [ProtoMember(10, IsRequired = false)]
        public uint BetID;
        [ProtoMember(11, IsRequired = false)]
        public uint unknown7;
        [ProtoMember(12, IsRequired = true)]
        public ActionType Type;
        [ProtoMember(13, IsRequired = true)]
        public ushort Facing;
        [ProtoMember(14, IsRequired = true)]
        public ushort wParam1;
        [ProtoMember(15, IsRequired = true)]
        public ushort wParam2;
        [ProtoMember(16, IsRequired = false)]
        public uint unknown8;
        [ProtoMember(17, IsRequired = true)]
        public uint dwParam3;
        [ProtoMember(22, IsRequired = false)]
        public long MsgTimeLeft;
        [ProtoMember(23, IsRequired = true)]
        public long DwParamX;
        [ProtoMember(20, IsRequired = true)]
        public long UnknownX;
        [ProtoMember(24, IsRequired = false)]
        public List<byte[]> Strings;

    }
}

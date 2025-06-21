using MMORPGServer.Domain.Enums;
using ProtoBuf;

namespace MMORPGServer.Domain.PacketsProto
{
    [ProtoContract]
    public class ActionProto
    {
        [ProtoMember(1, IsRequired = true)]
        public int UID;
        [ProtoMember(2, IsRequired = false)]
        public int AttackUID;
        [ProtoMember(3, IsRequired = true)]
        public long dwParam;
        [ProtoMember(4, IsRequired = false)]
        public int unknown3;
        [ProtoMember(5, IsRequired = false)]
        public long dwParamRespond;
        [ProtoMember(6, IsRequired = false)]
        public int unknown5;
        [ProtoMember(7, IsRequired = true)]
        public short dwParam_Lo;
        [ProtoMember(8, IsRequired = true)]
        public short dwParam_Hi;
        [ProtoMember(9, IsRequired = true)]
        public int Timestamp;
        [ProtoMember(10, IsRequired = false)]
        public int BetID;
        [ProtoMember(11, IsRequired = false)]
        public int unknown7;
        [ProtoMember(12, IsRequired = true)]
        public ActionType Type;
        [ProtoMember(13, IsRequired = true)]
        public short Facing;
        [ProtoMember(14, IsRequired = true)]
        public short wParam1;
        [ProtoMember(15, IsRequired = true)]
        public short wParam2;
        [ProtoMember(16, IsRequired = false)]
        public int unknown8;
        [ProtoMember(17, IsRequired = true)]
        public int dwParam3;
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

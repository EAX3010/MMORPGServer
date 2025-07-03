using MMORPGServer.Common.Enums;
using ProtoBuf;

namespace MMORPGServer.Networking.Packets.PacketsProto
{
    [ProtoContract]
    public class TalkProto
    {
        [ProtoMember(1, IsRequired = true)]
        public int TimeStamp;
        [ProtoMember(2, IsRequired = true)]
        public ChatType ChatType;
        [ProtoMember(3, IsRequired = true)]
        public int MessageUID1;
        [ProtoMember(4, IsRequired = true)]
        public int MessageUID2;
        [ProtoMember(5, IsRequired = true)]
        public int Unknown1;
        [ProtoMember(6, IsRequired = true)]
        public int Mesh;
        [ProtoMember(7, IsRequired = true)]
        public int Unknown2;
        [ProtoMember(8, IsRequired = true)]
        public int Unknown3;
        [ProtoMember(9, IsRequired = true)]
        public int MsgTab;
        [ProtoMember(10, IsRequired = true)]
        public int Unknown5;
        [ProtoMember(11, IsRequired = true)]
        public int Unknown6;
        [ProtoMember(12, IsRequired = true)]
        public int ToServer;
        [ProtoMember(13, IsRequired = true)]
        public int Unknown13;
        [ProtoMember(14, IsRequired = false)]
        public List<string>? Strings;
    }
}
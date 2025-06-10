using MMORPGServer.Domain.Enums;
using ProtoBuf;
using System.Collections.Generic;

namespace MMORPGServer.Network.Packets
{
    [ProtoContract]
    public class TalkProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint TimeStamp;
        [ProtoMember(2, IsRequired = true)]
        public ChatType ChatType;
        [ProtoMember(3, IsRequired = true)]
        public uint MessageUID1;
        [ProtoMember(4, IsRequired = true)]
        public uint MessageUID2;
        [ProtoMember(5, IsRequired = true)]
        public uint Unknown1;
        [ProtoMember(6, IsRequired = true)]
        public uint Mesh;
        [ProtoMember(7, IsRequired = true)]
        public uint Unknown2;
        [ProtoMember(8, IsRequired = true)]
        public uint Unknown3;
        [ProtoMember(9, IsRequired = true)]
        public uint MsgTab;
        [ProtoMember(10, IsRequired = true)]
        public uint Unknown5;
        [ProtoMember(11, IsRequired = true)]
        public uint Unknown6;
        [ProtoMember(12, IsRequired = true)]
        public uint ToServer;
        [ProtoMember(13, IsRequired = true)]
        public uint Unknown13;
        [ProtoMember(14, IsRequired = true)]
        public List<string> Strings;
    }
}
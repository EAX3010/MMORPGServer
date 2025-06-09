using ProtoBuf;

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
        public uint Unknowen1;
        [ProtoMember(6, IsRequired = true)]
        public uint Mesh;
        [ProtoMember(7, IsRequired = true)]
        public uint Unknowen2;
        [ProtoMember(8, IsRequired = true)]
        public uint Unknowen3;
        [ProtoMember(9, IsRequired = true)]
        public uint MsgTab;
        [ProtoMember(10, IsRequired = true)]
        public uint Unknowen5;
        [ProtoMember(11, IsRequired = true)]
        public uint Unknowen6;
        [ProtoMember(12, IsRequired = true)]
        public uint ToServer;
        [ProtoMember(13, IsRequired = true)]
        public uint Unknowen13;
        [ProtoMember(14, IsRequired = true)]
        public List<string>? Strings;
    }
}
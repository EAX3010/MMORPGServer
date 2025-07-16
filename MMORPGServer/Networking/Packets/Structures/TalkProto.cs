using MMORPGServer.Common.Enums;
using ProtoBuf;

namespace MMORPGServer.Networking.Packets.Structures
{
    [ProtoContract]
    public class TalkProto
    {
        [ProtoMember(1, IsRequired = true)]
        public int TimeStamp { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public ChatType ChatType { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public int MessageUID1 { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public int MessageUID2 { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public int Unknown1 { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public int Mesh { get; set; }

        [ProtoMember(7, IsRequired = true)]
        public int Unknown2 { get; set; }

        [ProtoMember(8, IsRequired = true)]
        public int Unknown3 { get; set; }

        [ProtoMember(9, IsRequired = true)]
        public int MsgTab { get; set; }

        [ProtoMember(10, IsRequired = true)]
        public int Unknown5 { get; set; }

        [ProtoMember(11, IsRequired = true)]
        public int Unknown6 { get; set; }

        [ProtoMember(12, IsRequired = true)]
        public int ToServer { get; set; }

        [ProtoMember(13, IsRequired = true)]
        public int Unknown13 { get; set; }

        [ProtoMember(14, IsRequired = false)]
        public List<string>? Strings { get; set; }

        /// <summary>
        /// Creates a TalkProto for chat messages
        /// </summary>
        public static TalkProto Create(string from, string to, string suffix, string message, ChatType chatType, int mesh)
        {
            return new TalkProto
            {
                TimeStamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ChatType = chatType,
                Mesh = mesh,
                Strings = new List<string> { from, to, "", message, "", suffix, "" },
                // Set other fields to defaults
                MessageUID1 = 0,
                MessageUID2 = 0,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0,
                MsgTab = 0,
                Unknown5 = 0,
                Unknown6 = 0,
                ToServer = 0,
                Unknown13 = 0
            };
        }
    }
}
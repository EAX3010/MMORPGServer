using MMORPGServer.Common.Enums;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Fluent;
using MMORPGServer.Networking.Packets.Structures;

namespace MMORPGServer.Networking.Packets.Core
{
    public class PacketFactory
    {
        private static ReadOnlyMemory<byte> CreateProtoPacket<T>(GamePackets packetType, T protoData)
        {
            return new FluentPacketWriter(packetType)
               .Seek(4).SerializeProto(protoData)
               .BuildAndFinalize();
        }

        public static ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, int mesh)
        {
            var talkPacket = TalkProto.Create(from, to, suffix, message, chatType, mesh);
            return CreateProtoPacket(GamePackets.CMsgTalk, talkPacket);
        }

        public static ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player)
        {
            var proto = HeroInfoProto.FromPlayer(player);
            return CreateProtoPacket(GamePackets.CMsgUserInfo, proto);
        }
    }
}
using MMORPGServer.Common.Enums;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Fluent;
using MMORPGServer.Networking.Packets.Structures;
using ProtoBuf;

namespace MMORPGServer.Networking.Packets.Core
{
    public class PacketFactory
    {
        private static ReadOnlyMemory<byte> CreateProtoPacket(GamePackets packetType, byte[] protoData)
        {
            return new FluentPacketWriter(packetType)
               .Seek(4).WriteBytes(protoData)
               .BuildAndFinalize();
        }

        public static ReadOnlyMemory<byte> CreateLoginGamaEnglish()
        {
            return new FluentPacketWriter(GamePackets.LoginGamaEnglish)
           .WriteUInt32(10002)
           .WriteUInt32(0)
           .Debug("CMsgLoginGame simple response")
           .BuildAndFinalize();
        }

        public static ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, int mesh)
        {
            var talkPacket = TalkProto.Create(from, to, suffix, message, chatType, mesh);
            return CreateProtoPacket(GamePackets.CMsgTalk, SerializeProto(talkPacket));
        }

        public static ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player)
        {
            var proto = HeroInfoProto.FromPlayer(player);
            return CreateProtoPacket(GamePackets.CMsgUserInfo, SerializeProto(proto));
        }

        public static ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto)
        {
            return CreateProtoPacket(GamePackets.CMsgAction, SerializeProto(proto));
        }

        private static byte[] SerializeProto<T>(T proto)
        {
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, proto);
            return memoryStream.ToArray();
        }
    }
}
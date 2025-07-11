using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.PacketsProto;
using ProtoBuf;

namespace MMORPGServer.Infrastructure.Networking.Packets
{
    public class PacketFactory : IPacketFactory
    {
        private ReadOnlyMemory<byte> CreateProtoPacket(GamePackets packetType, byte[] protoData)
        {
            return new Packet(packetType).GetWriter()
               .Seek(4).WriteBytes(protoData)
               .BuildAndFinalize();
        }
        public ReadOnlyMemory<byte> CreateLoginGamaEnglish()
        {
            return new Packet(GamePackets.LoginGamaEnglish).GetWriter()
           .WriteUInt32(10002)
           .WriteUInt32(0)
           .Debug("CMsgLoginGame simple response")
           .BuildAndFinalize();
        }
        public ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, int mesh)
        {
            TalkProto talkPacket = new TalkProto
            {
                ChatType = chatType,
                Mesh = mesh,
                Strings = new List<string> { from, to, "", message, "", suffix, "" }
            };

            // Serialize our object to a byte array using Protobuf
            using MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, talkPacket);
            byte[] payload = memoryStream.ToArray();

            // Use the new PacketBuilder to construct the final packet for sending
            return CreateProtoPacket(GamePackets.CMsgTalk, payload);
        }
        public ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player)
        {
            HeroInfoProto proto = new HeroInfoProto
            {
                Id = player.Id,
                Name = "Hero",
                Level = player.Level,
                Strength = player.Strength,
                Agility = player.Agility,
                Vitality = player.Vitality,
                Spirit = player.Spirit,
                // Map other fields as needed, set defaults for missing ones
                Mesh = 0,
                Hair = 0,
                Money = 0,
                ConquerPoints = 0,
                Experience = 0,
                ServerInfo = 0,
                SetLocationType = 0,
                SpecialTitleID = 0,
                SpecialWingID = 0,
                HeavenBlessing = 0,
                Atributes = 0,
                HitPoints = player.CurrentHealth,
                Mana = player.MaxMana,
                PKPoints = 0,
                FullClass = 0,
                FirstFullClass = 0,
                SecondFullClass = 0,
                NobilityRank = 0,
                Reborn = 0,
                u27 = 0,
                QuizPoints = 0,
                MainFlag = 0,
                Enilghten = 0,
                EnlightenReceive = 0,
                u32 = 0,
                u33 = 0,
                VipLevel = 0,
                MyTitle = 0,
                BoundConquerPoints = 0,
                SubClass = 0,
                ActiveSublass = 0,
                RacePoints = 0,
                CountryID = 0,
                u41 = 0,
                u42 = 0,
                SacredClassEXP = 0,
                u44 = 0,
                u45 = 0,
                SpouseName = "",
                u48 = 0,
                u49 = 0,
                u50 = 0,
                u51 = 0,
                u52 = 0,
                u53 = 0
            };
            using MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, proto);
            byte[] payload = memoryStream.ToArray();
            return CreateProtoPacket(GamePackets.CMsgUserInfo, payload);
        }
        public ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto)
        {
            using MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, proto);
            byte[] payload = memoryStream.ToArray();
            return CreateProtoPacket(GamePackets.CMsgAction, payload);
        }
    }
}
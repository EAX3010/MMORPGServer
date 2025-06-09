using MMORPGServer.Network.Packets;
using ProtoBuf;

namespace MMORPGServer.Core
{
    public partial class PacketFactory
    {
        public static ReadOnlyMemory<byte> CreateProtoPacket(GamePackets packetType, byte[] protoData)
        {
            return PacketBuilder.Create(packetType)
               .WriteBytes(protoData)
               .BuildAndFinalize();
        }
        public static ReadOnlyMemory<byte> CreateLoginGamaEnglish()
        {
            return PacketBuilder.Create(GamePackets.LoginGamaEnglish)
           .WriteUInt32(10002)
           .WriteUInt32(0)
           .Debug("CMsgLoginGame simple response")
           .BuildAndFinalize();
        }
        public static ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, uint mesh)
        {
            var talkPacket = new TalkProto
            {
                ChatType = chatType,
                Mesh = mesh,
                Strings = new List<string> { from, to, "", message, "", suffix, "" }
            };

            // Serialize our object to a byte array using Protobuf
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, talkPacket);
            var payload = memoryStream.ToArray();

            // Use the new PacketBuilder to construct the final packet for sending
            return CreateProtoPacket(GamePackets.CMsgTalk, payload);
        }
        public static ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player)
        {
            var proto = new HeroInfoProto
            {
                UID = player.ObjectId,
                Name = "Hero",
                Level = (uint)player.Level,
                Strength = (uint)player.Strength,
                Agility = (uint)player.Agility,
                Vitality = (uint)player.Vitality,
                Spirit = (uint)player.Spirit,
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
                HitPoints = player.MaxHealth,
                Mana = (uint)player.MaxMana,
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
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, proto);
            var payload = ms.ToArray();
            return CreateProtoPacket(GamePackets.CMsgUserInfo, payload);
        }
        public static ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto)
        {
            using var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, proto);
            var payload = memoryStream.ToArray();
            return CreateProtoPacket(GamePackets.CMsgAction, payload);
        }
    }
}
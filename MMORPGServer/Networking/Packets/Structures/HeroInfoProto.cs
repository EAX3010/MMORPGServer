using MMORPGServer.Entities;
using ProtoBuf;

namespace MMORPGServer.Networking.Packets.Structures
{
    [ProtoContract]
    public class HeroInfoProto
    {
        [ProtoMember(1, IsRequired = true)]
        public int Id;
        [ProtoMember(2, IsRequired = true)]
        public int AparenceType;
        [ProtoMember(3, IsRequired = true)]
        public int Mesh;
        [ProtoMember(4, IsRequired = true)]
        public int Hair;
        [ProtoMember(5, IsRequired = true)]
        public long Gold;
        [ProtoMember(6, IsRequired = true)]
        public int ConquerPoints;
        [ProtoMember(7, IsRequired = true)]
        public long Experience;
        [ProtoMember(8, IsRequired = true)]
        public int ServerInfo;
        [ProtoMember(9, IsRequired = true)]
        public int SetLocationType;
        [ProtoMember(10, IsRequired = true)]
        public int SpecialTitleID;
        [ProtoMember(11, IsRequired = true)]
        public int SpecialWingID;
        [ProtoMember(12, IsRequired = true)]
        public int HeavenBlessing;
        [ProtoMember(13, IsRequired = true)]
        public int Strength;
        [ProtoMember(14, IsRequired = true)]
        public int Agility;
        [ProtoMember(15, IsRequired = true)]
        public int Vitality;
        [ProtoMember(16, IsRequired = true)]
        public int Spirit;
        [ProtoMember(17, IsRequired = true)]
        public int Atributes;
        [ProtoMember(18, IsRequired = true)]
        public int HitPoints;
        [ProtoMember(19, IsRequired = true)]
        public int Mana;
        [ProtoMember(20, IsRequired = true)]
        public int PKPoints;
        [ProtoMember(21, IsRequired = true)]
        public int Level;
        [ProtoMember(22, IsRequired = true)]
        public int FullClass;
        [ProtoMember(23, IsRequired = true)]
        public int FirstFullClass;
        [ProtoMember(24, IsRequired = true)]
        public int SecondFullClass;
        [ProtoMember(25, IsRequired = true)]
        public int NobilityRank;
        [ProtoMember(26, IsRequired = true)]
        public int Reborn;
        [ProtoMember(27, IsRequired = true)]
        public int u27;
        [ProtoMember(28, IsRequired = true)]
        public int QuizPoints;
        [ProtoMember(29, IsRequired = true)]
        public int MainFlag;
        [ProtoMember(30, IsRequired = true)]
        public int Enilghten;
        [ProtoMember(31, IsRequired = true)]
        public int EnlightenReceive;
        [ProtoMember(32, IsRequired = true)]
        public int u32;
        [ProtoMember(33, IsRequired = true)]
        public int u33;
        [ProtoMember(34, IsRequired = true)]
        public int VipLevel;
        [ProtoMember(35, IsRequired = true)]
        public int MyTitle;
        [ProtoMember(36, IsRequired = true)]
        public int BoundConquerPoints;
        [ProtoMember(37, IsRequired = true)]
        public int SubClass;
        [ProtoMember(38, IsRequired = true)]
        public int ActiveSublass;
        [ProtoMember(39, IsRequired = true)]
        public int RacePoints;
        [ProtoMember(40, IsRequired = true)]
        public int CountryID;
        [ProtoMember(41, IsRequired = true)]
        public int u41;
        [ProtoMember(42, IsRequired = true)]
        public int u42;
        [ProtoMember(43, IsRequired = true)]
        public int SacredClassEXP;
        [ProtoMember(44, IsRequired = true)]
        public int u44;
        [ProtoMember(45, IsRequired = true)]
        public int u45;
        [ProtoMember(46, IsRequired = true)]
        public string Name;
        [ProtoMember(47, IsRequired = true)]
        public string SpouseName;
        [ProtoMember(48, IsRequired = true)]
        public int u48;
        [ProtoMember(49, IsRequired = true)]
        public int u49;
        [ProtoMember(50, IsRequired = true)]
        public int u50;
        [ProtoMember(51, IsRequired = true)]
        public int u51;
        [ProtoMember(52, IsRequired = true)]
        public int u52;
        [ProtoMember(53, IsRequired = true)]
        public int u53;
        public static HeroInfoProto FromPlayer(Player player)
        {
            return new HeroInfoProto
            {
                Id = player.Id,
                Name = player.Name,
                Level = player.Level,
                Strength = player.Strength,
                Agility = player.Agility,
                Vitality = player.Vitality,
                Spirit = player.Spirit,
                Mesh = player.Mesh,
                Hair = player.Hair,
                Gold = player.Gold,
                ConquerPoints = player.ConquerPoints,
                Experience = player.Experience,
                ServerInfo = 0,
                SetLocationType = 0,
                SpecialTitleID = 0,
                SpecialWingID = 0,
                HeavenBlessing = 0,
                Atributes = 0,
                HitPoints = player.CurrentHealth,
                Mana = player.MaxMana,
                PKPoints = 0,
                FullClass = (int)player.Class + player.ClassLevel,
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
                BoundConquerPoints = player.BoundConquerPoints,
                SubClass = 0,
                ActiveSublass = 0,
                RacePoints = 0,
                CountryID = 0,
                u41 = 0,
                u42 = 0,
                SacredClassEXP = 0,
                u44 = 0,
                u45 = 0,
                SpouseName = string.Empty,
                u48 = 0,
                u49 = 0,
                u50 = 0,
                u51 = 0,
                u52 = 0,
                u53 = 0
            };
        }
    }

}
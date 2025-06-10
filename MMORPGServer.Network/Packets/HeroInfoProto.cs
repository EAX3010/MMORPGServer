using ProtoBuf;

namespace MMORPGServer.Network.Packets
{
    [ProtoContract]
    public class HeroInfoProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint UID;
        [ProtoMember(2, IsRequired = true)]
        public uint AparenceType;
        [ProtoMember(3, IsRequired = true)]
        public uint Mesh;
        [ProtoMember(4, IsRequired = true)]
        public uint Hair;
        [ProtoMember(5, IsRequired = true)]
        public long Money;
        [ProtoMember(6, IsRequired = true)]
        public uint ConquerPoints;
        [ProtoMember(7, IsRequired = true)]
        public ulong Experience;
        [ProtoMember(8, IsRequired = true)]
        public uint ServerInfo;
        [ProtoMember(9, IsRequired = true)]
        public uint SetLocationType;
        [ProtoMember(10, IsRequired = true)]
        public uint SpecialTitleID;
        [ProtoMember(11, IsRequired = true)]
        public uint SpecialWingID;
        [ProtoMember(12, IsRequired = true)]
        public int HeavenBlessing;
        [ProtoMember(13, IsRequired = true)]
        public uint Strength;
        [ProtoMember(14, IsRequired = true)]
        public uint Agility;
        [ProtoMember(15, IsRequired = true)]
        public uint Vitality;
        [ProtoMember(16, IsRequired = true)]
        public uint Spirit;
        [ProtoMember(17, IsRequired = true)]
        public uint Atributes;
        [ProtoMember(18, IsRequired = true)]
        public int HitPoints;
        [ProtoMember(19, IsRequired = true)]
        public uint Mana;
        [ProtoMember(20, IsRequired = true)]
        public uint PKPoints;
        [ProtoMember(21, IsRequired = true)]
        public uint Level;
        [ProtoMember(22, IsRequired = true)]
        public uint FullClass;
        [ProtoMember(23, IsRequired = true)]
        public uint FirstFullClass;
        [ProtoMember(24, IsRequired = true)]
        public uint SecondFullClass;
        [ProtoMember(25, IsRequired = true)]
        public uint NobilityRank;
        [ProtoMember(26, IsRequired = true)]
        public uint Reborn;
        [ProtoMember(27, IsRequired = true)]
        public uint u27;
        [ProtoMember(28, IsRequired = true)]
        public uint QuizPoints;
        [ProtoMember(29, IsRequired = true)]
        public uint MainFlag;
        [ProtoMember(30, IsRequired = true)]
        public uint Enilghten;
        [ProtoMember(31, IsRequired = true)]
        public uint EnlightenReceive;
        [ProtoMember(32, IsRequired = true)]
        public uint u32;
        [ProtoMember(33, IsRequired = true)]
        public uint u33;
        [ProtoMember(34, IsRequired = true)]
        public uint VipLevel;
        [ProtoMember(35, IsRequired = true)]
        public uint MyTitle;
        [ProtoMember(36, IsRequired = true)]
        public uint BoundConquerPoints;
        [ProtoMember(37, IsRequired = true)]
        public uint SubClass;
        [ProtoMember(38, IsRequired = true)]
        public uint ActiveSublass;
        [ProtoMember(39, IsRequired = true)]
        public uint RacePoints;
        [ProtoMember(40, IsRequired = true)]
        public uint CountryID;
        [ProtoMember(41, IsRequired = true)]
        public uint u41;
        [ProtoMember(42, IsRequired = true)]
        public uint u42;
        [ProtoMember(43, IsRequired = true)]
        public uint SacredClassEXP;
        [ProtoMember(44, IsRequired = true)]
        public uint u44;
        [ProtoMember(45, IsRequired = true)]
        public uint u45;
        [ProtoMember(46, IsRequired = true)]
        public string Name;
        [ProtoMember(47, IsRequired = true)]
        public string SpouseName;
        [ProtoMember(48, IsRequired = true)]
        public uint u48;
        [ProtoMember(49, IsRequired = true)]
        public uint u49;
        [ProtoMember(50, IsRequired = true)]
        public uint u50;
        [ProtoMember(51, IsRequired = true)]
        public uint u51;
        [ProtoMember(52, IsRequired = true)]
        public uint u52;
        [ProtoMember(53, IsRequired = true)]
        public int u53;
    }


}
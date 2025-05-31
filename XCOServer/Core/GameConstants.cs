namespace MMORPGServer.Core
{
    public static class GameConstants
    {
        public const int DEFAULT_PORT = 10033;
        public const int GAME_TICK_RATE = 100;
        public const int MAX_CLIENTS = 1000;

        public static class PacketTypes
        {
            // Authentication
            public const ushort LOGIN_REQUEST = 1001;
            public const ushort LOGIN_RESPONSE = 1002;
            public const ushort LOGOUT_REQUEST = 1003;

            // Character Management
            public const ushort CHARACTER_LIST_REQUEST = 1010;
            public const ushort CHARACTER_LIST_RESPONSE = 1011;
            public const ushort CHARACTER_CREATE = 1001;
            public const ushort CHARACTER_SELECT = 1004;
            public const ushort CHARACTER_DELETE = 1020;

            // Movement
            public const ushort MOVEMENT_REQUEST = 1005;
            public const ushort MOVEMENT_UPDATE = 1006;
            public const ushort JUMP_REQUEST = 1007;

            // Chat
            public const ushort CHAT_MESSAGE = 1015;
            public const ushort CHAT_BROADCAST = 1016;
            public const ushort WHISPER_MESSAGE = 1017;

            // Combat
            public const ushort ATTACK_REQUEST = 1022;
            public const ushort DAMAGE_UPDATE = 1023;
            public const ushort MAGIC_ATTACK = 1024;
            public const ushort SKILL_USE = 1025;

            // Items
            public const ushort ITEM_USE = 1009;
            public const ushort ITEM_DROP = 1012;
            public const ushort ITEM_PICKUP = 1013;
            public const ushort INVENTORY_UPDATE = 1014;
            public const ushort EQUIPMENT_WEAR = 1008;

            // Trading
            public const ushort TRADE_REQUEST = 1056;
            public const ushort TRADE_ACCEPT = 1057;
            public const ushort TRADE_CANCEL = 1058;

            // Guild
            public const ushort GUILD_CREATE = 1070;
            public const ushort GUILD_INVITE = 1071;
            public const ushort GUILD_ACCEPT = 1072;
            public const ushort GUILD_KICK = 1073;

            // System
            public const ushort HEARTBEAT = 1052;
            public const ushort SERVER_MESSAGE = 1004;
            public const ushort DH_HANDSHAKE = 0x052C;
        }

        public static class CharacterClasses
        {
            public const ushort WARRIOR = 1;
            public const ushort ARCHER = 2;
            public const ushort MAGE = 3;
            public const ushort ASSASSIN = 4;
            public const ushort MONK = 5;
            public const ushort PIRATE = 6;
        }

        public static class Maps
        {
            public const uint TWIN_CITY = 1002;
            public const uint PHOENIX_CASTLE = 1000;
            public const uint APES_CITY = 1020;
            public const uint DESERT_CITY = 1000;
            public const uint BIRD_ISLAND = 1015;
            public const uint MARKET = 1036;
        }
    }
}

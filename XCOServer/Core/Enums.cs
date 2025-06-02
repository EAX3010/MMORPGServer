namespace MMORPGServer.Core
{
    public enum PlayerState : byte
    {
        Offline = 0,
        Online = 1,
        InCombat = 2,
        Trading = 3,
        InTeam = 4,
        Away = 5
    }

    public enum Direction : byte
    {
        North = 0,
        Northeast = 1,
        East = 2,
        Southeast = 3,
        South = 4,
        Southwest = 5,
        West = 6,
        Northwest = 7
    }

    public enum ChatType : byte
    {
        Talk = 0,
        Whisper = 1,
        Action = 2,
        Team = 3,
        Guild = 4,
        System = 5,
        Broadcast = 6
    }

    public enum ItemType : byte
    {
        Weapon = 1,
        Armor = 2,
        Accessory = 3,
        Consumable = 4,
        Quest = 5,
        Material = 6
    }
    public enum ClientState
    {
        Connecting,
        WaitingForDummyPacket,
        DhKeyExchange,
        Connected,
        Disconnected
    }
}
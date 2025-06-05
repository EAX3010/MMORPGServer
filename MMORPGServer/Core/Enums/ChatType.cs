namespace MMORPGServer.Core.Enums
{
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
}
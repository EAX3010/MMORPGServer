namespace MMORPGServer.Domain.Enums
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
}
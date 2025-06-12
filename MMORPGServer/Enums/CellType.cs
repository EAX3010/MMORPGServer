namespace MMORPGServer.Enums
{
    [Flags]
    public enum CellType
    {
        Open = 0,
        Blocked = 1,
        Portal = 2,
        Entity = 3,
        StaticObj = 4,
        BlockedObj = 5,
    }
}
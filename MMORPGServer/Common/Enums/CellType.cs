namespace MMORPGServer.Common.Enums
{
    [Flags]
    public enum CellType
    {
        None = 0,
        Blocked = 1,
        Open = 2,
        StaticObj = 4,
        Entity = 8,
        Gate = 16,
        BlockedObj = 32,
    }
}
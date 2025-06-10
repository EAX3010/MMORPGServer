namespace MMORPGServer.Domain.Enums
{
    [Flags]
    public enum CellType : byte
    {
        None = 0x0,
        Open = 0x1,
        Portal = 0x2,
        Item = 0x4,
        Npc = 0x8,
        Monster = 0x10,
        Terrain = 0x20
    }
}
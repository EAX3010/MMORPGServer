namespace MMORPGServer.Interfaces
{
    public interface IPlayer
    {
        uint CharacterId { get; }
        string Name { get; }
        ushort Level { get; }
        ushort Class { get; }
        uint MapId { get; }
        ushort X { get; }
        ushort Y { get; }
        byte Direction { get; }
        uint HP { get; }
        uint MaxHP { get; }
        uint MP { get; }
        uint MaxMP { get; }

        ValueTask UpdateAsync();
        ValueTask MoveToAsync(ushort x, ushort y, byte direction);
        ValueTask TeleportToAsync(uint mapId, ushort x, ushort y);
        ValueTask TakeDamageAsync(uint damage);
        ValueTask HealAsync(uint amount);
    }
}
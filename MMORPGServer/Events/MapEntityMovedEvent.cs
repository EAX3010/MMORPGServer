using MMORPGServer.ValueObjects;

namespace MMORPGServer.Events
{
    public record MapEntityMovedEvent(ushort MapId, uint EntityId, Position OldPosition, Position NewPosition);

}

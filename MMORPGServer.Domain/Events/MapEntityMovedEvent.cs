namespace MMORPGServer.Domain.Events
{
    public record MapEntityMovedEvent(ushort MapId, uint EntityId, Position OldPosition, Position NewPosition);

}

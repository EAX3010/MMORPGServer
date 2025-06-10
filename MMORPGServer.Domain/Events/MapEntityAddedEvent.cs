namespace MMORPGServer.Domain.Events
{
    public record MapEntityAddedEvent(ushort MapId, uint EntityId, Position Position);

}

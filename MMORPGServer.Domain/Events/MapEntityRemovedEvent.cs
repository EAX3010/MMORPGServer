namespace MMORPGServer.Domain.Events
{
    public record MapEntityRemovedEvent(ushort MapId, uint EntityId);

}

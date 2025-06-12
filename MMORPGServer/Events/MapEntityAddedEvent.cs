using MMORPGServer.ValueObjects;

namespace MMORPGServer.Events
{
    public record MapEntityAddedEvent(ushort MapId, uint EntityId, Position Position);

}

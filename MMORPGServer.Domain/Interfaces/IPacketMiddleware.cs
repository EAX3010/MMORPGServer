namespace MMORPGServer.Domain.Interfaces
{
    public interface IPacketMiddleware
    {
        ValueTask<bool> InvokeAsync(IGameClient client, IPacket packet, Func<ValueTask> next);
    }
}

namespace MMORPGServer.Domain.Common.Interfaces
{
    public interface IPacketMiddleware
    {
        ValueTask<bool> InvokeAsync(IGameClient client, IPacket packet, Func<ValueTask> next);
    }
}

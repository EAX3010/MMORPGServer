using MMORPGServer.Networking.Clients;

namespace MMORPGServer.Common.Interfaces
{
    /// <summary>
    /// Base interface for all packet handlers
    /// </summary>
    public interface IPacketHandler
    {
        ValueTask ProcessAsync(GameClient client);
    }

    /// <summary>
    /// Generic interface for packet handlers that read specific data types
    /// </summary>
    public interface IPacketHandler<T> : IPacketHandler where T : class
    {
        T? Read();
    }
}
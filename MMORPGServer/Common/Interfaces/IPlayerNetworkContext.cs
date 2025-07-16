// File: MMORPGServer.Common.Interfaces/IPlayerNetworkContext.cs
namespace MMORPGServer.Common.Interfaces
{
    /// <summary>
    /// Defines the minimal set of player properties required by the GameClient
    /// for network-related operations and logging, without creating a
    /// direct dependency on the concrete Player class for all client logic.
    /// </summary>
    public interface IPlayerNetworkContext
    {
        /// <summary>
        /// Gets the unique identifier of the player.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the name of the player.
        /// </summary>
        string Name { get; }

        // Add any other player properties here that GameClient *absolutely needs*
        // for its direct network responsibilities (e.g., if you log player's level
        // directly from GameClient for all client messages, you might add:
        // int Level { get; }
        // But typically, GameClient only needs basic identity for logging and passing
        // the Player object to a manager for actual game logic.
    }
}
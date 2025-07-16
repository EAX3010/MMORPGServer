using MMORPGServer.Networking.Clients;
using Serilog;

namespace MMORPGServer.Networking.Packets.Core
{
    /// <summary>
    /// Base class for all packet handlers with generic, flexible design
    /// </summary>
    public abstract class PacketBaseHandler
    {
        public Packet Packet { get; init; }

        protected PacketBaseHandler(Packet packet)
        {
            Packet = packet ?? throw new ArgumentNullException(nameof(packet));
        }

        /// <summary>
        /// Main handler entry point
        /// </summary>
        public async ValueTask HandleAsync(GameClient client)
        {
            try
            {
                if (!await ValidateAsync(client))
                {
                    return;
                }

                await ProcessAsync(client);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing {HandlerName} for client {ClientId} (Player: {PlayerName})",
                    GetType().Name, client.ClientId, client.Player?.Name ?? "N/A");

                await HandleErrorAsync(client, ex);
            }
        }

        /// <summary>
        /// Validates the client and packet data before processing
        /// </summary>
        public virtual async ValueTask<bool> ValidateAsync(GameClient client)
        {
            if (client == null)
            {
                Log.Warning("{HandlerName} received null client", GetType().Name);
                return false;
            }

            return await Task.FromResult(true);
        }

        /// <summary>
        /// Main processing method - implement your packet logic here
        /// </summary>
        public abstract ValueTask ProcessAsync(GameClient client);

        /// <summary>
        /// Handle errors that occur during processing
        /// </summary>
        public virtual async ValueTask HandleErrorAsync(GameClient client, Exception ex)
        {
            // Default: just log, derived classes can implement specific error handling
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Base class for handlers that require an authenticated player
    /// </summary>
    public abstract class AuthenticatedHandler : PacketBaseHandler
    {
        protected AuthenticatedHandler(Packet packet) : base(packet) { }

        public override async ValueTask<bool> ValidateAsync(GameClient client)
        {
            if (!await base.ValidateAsync(client))
                return false;

            if (client.Player == null)
            {
                Log.Warning("{HandlerName} requires authenticated player (client {ClientId})",
                    GetType().Name, client.ClientId);
                return false;
            }

            return true;
        }
    }
}
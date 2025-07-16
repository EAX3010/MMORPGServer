using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Packets.Structures;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol using attribute-based routing.
    /// </summary>
    [PacketHandler(GamePackets.CMsgAction)]
    public sealed class Action : AuthenticatedHandler, IPacketHandler<ActionProto>
    {
        private readonly ActionHandlerRegistry _handlerRegistry;

        public Action(Packet packet) : base(packet)
        {
            _handlerRegistry = ActionHandlerRegistry.Instance;
        }

        public async override ValueTask ProcessAsync(GameClient client)
        {
            // Read and deserialize the action packet data
            var actionProto = Read();
            if (actionProto == null)
            {
                Log.Warning("Failed to read action packet for client {ClientId} (Player: {PlayerName})",
                    client.ClientId, client.Player?.Name ?? "Unknown");
                return;
            }

            Log.Debug("Processing action {ActionType} for player {PlayerName} (ID: {PlayerId})",
                actionProto.Type, client.Player?.Name ?? "Unknown", client.Player?.Id ?? 0);

            await ProcessActionAsync(client, actionProto);
        }

        /// <summary>
        /// Reads and deserializes the action packet data
        /// </summary>
        public ActionProto? Read()
        {
            try
            {
                var actionProto = Packet.DeserializeProto<ActionProto>();

                // Validate the deserialized data
                if (actionProto == null)
                {
                    Log.Warning("Failed to deserialize ActionProto from packet");
                    return null;
                }

                // Additional validation
                if (!Enum.IsDefined(typeof(ActionType), actionProto.Type))
                {
                    Log.Warning("Invalid ActionType {ActionType} received", actionProto.Type);
                    return null; // Return null for invalid action types
                }

                return actionProto;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deserializing ActionProto from packet");
                return null;
            }
        }

        /// <summary>
        /// Processes actions using the attribute-based handler registry.
        /// </summary>
        private async ValueTask ProcessActionAsync(GameClient client, ActionProto action)
        {
            // Get handler from registry
            var handler = _handlerRegistry.GetHandler(action.Type);

            if (handler == null)
            {
                Log.Warning("No handler registered for action type {ActionType} received from player {PlayerName} (ID: {PlayerId})",
                    action.Type, client.Player?.Name ?? "Unknown", client.Player?.Id ?? 0);
                return;
            }

            try
            {
                var success = await handler.HandleAsync(client, action);
                if (!success)
                {
                    Log.Warning("Handler for action type {ActionType} failed for player {PlayerName} (ID: {PlayerId})",
                        action.Type, client.Player?.Name ?? "Unknown", client.Player?.Id ?? 0);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing action {ActionType} for player {PlayerId}",
                    action.Type, client.Player?.Id ?? 0);
            }
        }
    }
}
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
    /// Handles action-related packets in the game protocol.
    /// </summary>
    [PacketHandler(GamePackets.CMsgAction)]
    public sealed class Action : AuthenticatedHandler, IPacketHandler<ActionProto>
    {
        public Action(Packet packet) : base(packet)
        {

        }


        public async override ValueTask ProcessAsync(GameClient client)
        {
            // Read and deserialize the action packet data
            var actionProto = Read();
            if (actionProto == null)
            {
                Log.Warning("Failed to read action packet for client {ClientId} (Player: {PlayerName})",
                    client.ClientId, client.Player.Name);
                return;
            }

            Log.Debug("Processing action {ActionType} for player {PlayerName} (ID: {PlayerId})",
                actionProto.Type, client.Player.Name, client.Player.Id);

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

                // Additional validation can be added here
                if (!Enum.IsDefined(typeof(ActionType), actionProto.Type))
                {
                    Log.Warning("Invalid ActionType {ActionType} received", actionProto.Type);
                    return actionProto;
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
        /// Processes different types of actions.
        /// </summary>
        private async ValueTask ProcessActionAsync(GameClient client, ActionProto action)
        {
            switch (action.Type)
            {
                case ActionType.SetLocation:
                    Log.Debug("Handling SetLocation for player {PlayerId}", client.Player.Id);
                    await HandleSetLocationAsync(client, action);
                    break;

                // Add more action types here as needed
                // case ActionType.Attack:
                //     await HandleAttackAsync(client, action);
                //     break;

                default:
                    Log.Warning("Unhandled action type {ActionType} received from player {PlayerName} (ID: {PlayerId})",
                        action.Type, client.Player.Name, client.Player.Id);
                    break;
            }
        }

        /// <summary>
        /// Handles the set location action.
        /// </summary>
        private async ValueTask HandleSetLocationAsync(GameClient client, ActionProto action)
        {
            try
            {
                // Validate player state before processing
                if (client.Player.Position == null)
                {
                    Log.Warning("Player {PlayerId} has no position data for SetLocation action", client.Player.Id);
                    return;
                }

                // Create response packet
                var responseAction = new ActionProto
                {
                    UID = client.Player.Id,
                    Type = ActionType.SetLocation,
                    dwParam = client.Player.MapId,
                    wParam1 = client.Player.Position.X,
                    wParam2 = client.Player.Position.Y,
                };

                await client.SendPacketAsync(PacketFactory.CreateActionPacket(responseAction));

                Log.Debug("SetLocation response sent to player {PlayerId} - Map: {MapId}, Position: ({X}, {Y})",
                    client.Player.Id, client.Player.MapId, client.Player.Position.X, client.Player.Position.Y);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling SetLocation action for player {PlayerId}", client.Player.Id);
            }
        }


    }
}
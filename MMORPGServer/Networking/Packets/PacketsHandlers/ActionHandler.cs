using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.PacketsProto;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol.
    /// </summary>
    public sealed class ActionHandler
    {
        [PacketHandler(GamePackets.CMsgAction)]
        public static async ValueTask HandleAsync(GameClient client, Packet packet)
        {
            if (client.Player == null)
            {
                Log.Warning("Action packet received from client {ClientId} with no associated player.", client.ClientId);
                return;
            }

            try
            {
                ActionProto actionProto = packet.DeserializeProto<ActionProto>();
                Log.Debug("Processing action {ActionType} for player {PlayerName} (ID: {PlayerId})", actionProto.Type, client.Player.Name, client.Player.Id);
                await ProcessActionAsync(client, actionProto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing action packet for client {ClientId} (Player: {PlayerName})", client.ClientId, client.Player?.Name ?? "N/A");
            }
        }

        /// <summary>
        /// Processes different types of actions.
        /// </summary>
        private static async ValueTask ProcessActionAsync(GameClient client, ActionProto action)
        {
            switch (action.Type)
            {
                case ActionType.SetLocation:
                    Log.Debug("Handling SetLocation for player {PlayerId}", client.Player.Id);
                    await HandleSetLocationAsync(client, action);
                    break;

                default:
                    Log.Warning("Unhandled action type {ActionType} received from player {PlayerName} (ID: {PlayerId})", action.Type, client.Player.Name, client.Player.Id);
                    break;
            }
        }

        /// <summary>
        /// Handles the set location action.
        /// </summary>
        private static async ValueTask HandleSetLocationAsync(GameClient client, ActionProto action)
        {
            await client.SendPacketAsync(PacketFactory.CreateActionPacket(new ActionProto
            {
                UID = client.Player.Id,
                Type = ActionType.SetLocation,
                dwParam = client.Player.MapId,
                wParam1 = client.Player.Position.X,
                wParam2 = client.Player.Position.Y,
            }));
        }
    }
}

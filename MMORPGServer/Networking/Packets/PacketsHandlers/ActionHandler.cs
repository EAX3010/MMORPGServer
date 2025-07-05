using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.PacketsProto;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol.
    /// </summary>
    public sealed class ActionHandler
    {
        [PacketHandler(GamePackets.CMsgAction)]
        public static async ValueTask HandleAsync(GameClient client, IPacket packet)
        {
            if (client.ClientId is 0)
            {
                return;
            }

            try
            {
                ActionProto actionProto = packet.DeserializeProto<ActionProto>();
                await ProcessActionAsync(client, actionProto);
            }
            catch (Exception ex)
            {
                // Log the error and handle it appropriately
                Console.WriteLine($"Error processing action packet: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Processes different types of actions.
        /// </summary>
        private static async ValueTask ProcessActionAsync(GameClient client, ActionProto action)
        {
            switch (action.Type)
            {
                case ActionType.SetLocation:
                    await HandleSetLocationAsync(client, action);
                    break;

                default:
                    // Log unknown action type
                    Console.WriteLine($"Unknown action type: {action.Type}");
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

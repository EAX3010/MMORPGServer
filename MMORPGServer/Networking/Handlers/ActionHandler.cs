using MMORPGServer.Attributes;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Networking.Packets;

namespace MMORPGServer.Networking.Handlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol.
    /// </summary>
    public sealed class ActionHandler : IPacketProcessor
    {
        /// <summary>
        /// Processes action packets from clients.
        /// </summary>
        [PacketHandler(GamePackets.CMsgAction)]
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            if (client.Player == null)
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
        }

        /// <summary>
        /// Processes different types of actions.
        /// </summary>
        private async ValueTask ProcessActionAsync(IGameClient client, ActionProto action)
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
        private async ValueTask HandleSetLocationAsync(IGameClient client, ActionProto action)
        {
            await client.SendPacketAsync(PacketFactory.CreateActionPacket(new ActionProto
            {
                UID = client.Player.ObjectId,
                Type = ActionType.SetLocation,
                dwParam = client.Player.MapId,
                wParam1 = (ushort)client.Player.Position.X,
                wParam2 = (ushort)client.Player.Position.Y,
            }));
        }
    }
}

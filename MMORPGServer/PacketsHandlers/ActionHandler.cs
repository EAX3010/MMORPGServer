using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.PacketsProto;

namespace MMORPGServer.PacketsHandlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol.
    /// </summary>
    public sealed class ActionHandler(IPacketFactory PacketFactory) : IPacketProcessor<GamePackets>
    {
        public GamePackets PacketType => GamePackets.CMsgAction;
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
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
                UID = client.Player.Id,
                Type = ActionType.SetLocation,
                dwParam = client.Player.MapId,
                wParam1 = client.Player.Position.X,
                wParam2 = client.Player.Position.Y,
            }));
        }
    }


}

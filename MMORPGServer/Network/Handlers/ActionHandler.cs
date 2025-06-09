using MMORPGServer.Core;
using MMORPGServer.Core.Enums;
using MMORPGServer.Game.Maps;
using MMORPGServer.Network.Packets;
using ProtoBuf;
using System.Buffers;

namespace MMORPGServer.Network.Handlers
{
    /// <summary>
    /// Handles action-related packets in the game protocol.
    /// </summary>
    public class ActionHandler : IPacketHandler
    {
        private readonly ILogger<ActionHandler> _logger;
        private const ActionType ACTION_TYPE_SET_LOCATION = ActionType.SetLocation;

        public ActionHandler(ILogger<ActionHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles incoming action packets from clients.
        /// </summary>
        public async ValueTask HandlePacketAsync(IGameClient client, Packet packet)
        {
            try
            {
                // Validate packet size
                if (packet.Data.Length < 12)
                {
                    _logger.LogWarning("Action packet too small from client {ClientId}", client.ClientId);
                    return;
                }

                // Process the action
                await ProcessActionAsync(client, packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing action packet from client {ClientId}", client.ClientId);
            }
        }

        /// <summary>
        /// Processes different types of actions.
        /// </summary>
        private async Task ProcessActionAsync(IGameClient client, Packet packet)
        {
            // Calculate the actual payload size
            int payloadSize = packet.Data.Length - 12;
            if (payloadSize <= 0)
            {
                _logger.LogWarning("Invalid payload size in action packet from client {ClientId}", client.ClientId);
                return;
            }

            // Rent a buffer for the payload
            byte[]? payloadBuffer = null;
            try
            {
                payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadSize);
                var payloadSpan = new Span<byte>(payloadBuffer, 0, payloadSize);

                // Copy the payload data
                packet.Data.Slice(4, payloadSize).CopyTo(payloadSpan);

                // Deserialize the action
                using var ms = new MemoryStream(payloadBuffer, 0, payloadSize);
                var action = Serializer.Deserialize<ActionProto>(ms);

                // Handle the action based on its type
                switch (action.Type)
                {
                    case ACTION_TYPE_SET_LOCATION:
                        await HandleSetLocationAsync(client, action);
                        break;
                    default:
                        _logger.LogWarning("Unknown action type {ActionType} from client {ClientId}", action.Type, client.ClientId);
                        break;
                }
            }
            finally
            {
                if (payloadBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(payloadBuffer);
                }
            }
        }

        /// <summary>
        /// Handles the set location action.
        /// </summary>
        private async Task HandleSetLocationAsync(IGameClient client, ActionProto action)
        {
            if (client.Player == null)
            {
                _logger.LogWarning("Player is null in HandleSetLocationAsync for client {ClientId}", client.ClientId);
                return;
            }

            // Update player position
            client.Player.Position = new Position((short)action.wParam1, (short)action.wParam2);

            // Send response packet
            var responsePacket = new ActionProto
            {
                dwParam = client.Player.MapId,
                wParam1 = (ushort)client.Player.Position.X,
                wParam2 = (ushort)client.Player.Position.Y,
                Type = ACTION_TYPE_SET_LOCATION
            };

            await client.SendPacketAsync(PacketFactory.CreateActionPacket(responsePacket));
        }
    }
}
